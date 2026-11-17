// =====================================================================================
//  SHIFTERS - MusicPlayer.cs   [owned by the AUDIO agent]
//  Two looping 2D sources with equal-power crossfades, an AudioLowPassFilter each for the
//  switcher/pause muffle (22 kHz -> 900 Hz, plus a slight duck) and pitch (1 -> 0.85) for slow-mo.
//  Where audio filters are unsupported (WebGL) the muffle falls back to a volume duck (to 40%)
//  + the same pitch drop, and no filter components are created. Stinger
//  ducking, lazy background rendering of tracks (MusicSynth on a worker thread) and a
//  small LRU cache (max 3 rendered tracks in memory).
// =====================================================================================
using System.Collections.Generic;
using UnityEngine;

namespace Shifters
{
    public sealed class MusicPlayer
    {
        public const int MaxCached = 3;
        /// <summary>PCM cap of the music cache (MB float32): beyond it the least-recently-used track is evicted even below
        /// MaxCached. Never evicted: the playing / fading / pending track and the most recently rendered or preloaded one
        /// (so PreloadMusic(next) always survives). Keeps a chapter's PCM inside the 45 MB target (CAMPAIGN 21).</summary>
        public static float MaxCachedMB = 12.5f;
        public float CachedMB => CachedSamples * 4f / (1024f * 1024f);
        public int Evictions { get; private set; }
        /// <summary>Music bus gain so the soundtrack sits under SFX and voices.</summary>
        public const float BusGain = 0.62f;
        public const float MuffledCutoff = 900f, OpenCutoff = 22000f, SlowMoPitch = 0.85f;
        /// <summary>Music gain at full muffle: with the low-pass filter / without it (WebGL fallback).</summary>
        public const float FilterMuffleGain = 0.85f, FallbackMuffleGain = 0.4f;

        /// <summary>Tests: new players behave like WebGL (no AudioLowPassFilter, volume-duck muffle).</summary>
        public static bool ForceNoFilters;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { ForceNoFilters = false; MaxCachedMB = 12.5f; }

        /// <summary>False on WebGL (AudioLowPassFilter is ignored there).</summary>
        public static bool FiltersSupported => Application.platform != RuntimePlatform.WebGLPlayer;

        /// <summary>Music gain for a muffle amount (0..1).</summary>
        public static float MuffleGain(float muffle01, bool filters) => Mathf.Lerp(1f, filters ? FilterMuffleGain : FallbackMuffleGain, Mathf.Clamp01(muffle01));

        readonly AudioSource[] _src = new AudioSource[2];
        readonly AudioLowPassFilter[] _lp = new AudioLowPassFilter[2];
        readonly float[] _fade = new float[2], _target = new float[2], _speed = new float[2];
        readonly MusicId[] _srcId = new MusicId[2];
        readonly ClipFactory _factory;
        readonly Dictionary<MusicId, AudioClip> _clips = new Dictionary<MusicId, AudioClip>();
        readonly List<MusicId> _lru = new List<MusicId>();
        readonly HashSet<MusicId> _rendering = new HashSet<MusicId>();
        readonly bool _filters;
        int _active;
        MusicId _current = MusicId.None, _pending = MusicId.None;
        float _pendingFade = 1f;
        float _duck = 1f, _duckLevel = 1f, _duckUntil = -1f;

        public MusicId Current => _current;
        public MusicId Pending => _pending;
        public int Renders { get; private set; }
        public float Cutoff { get; private set; } = OpenCutoff;
        public float Pitch { get; private set; } = 1f;
        public float DuckValue => _duck;
        public int CachedTracks => _clips.Count;
        public long CachedSamples { get { long n = 0; foreach (var kv in _clips) if (kv.Value != null) n += (long)kv.Value.samples * kv.Value.channels; return n; } }
        public AudioSource ActiveSource => _src[_active];
        /// <summary>Crossfade gain (0..1) of source i.</summary>
        public float SourceFade(int i) => _fade[i & 1];
        public MusicId SourceTrack(int i) => _srcId[i & 1];
        public bool IsRendering(MusicId id) => _rendering.Contains(id);
        public bool IsCached(MusicId id) => _clips.ContainsKey(id);
        /// <summary>True when the low-pass muffle is used (false = volume-duck fallback).</summary>
        public bool UsesFilters => _filters;
        /// <summary>Current muffle gain applied to the music sources.</summary>
        public float MuffleGainNow { get; private set; } = 1f;

        public MusicPlayer(Transform parent, ClipFactory factory)
        {
            _factory = factory;
            _filters = FiltersSupported && !ForceNoFilters;
            for (int i = 0; i < 2; i++)
            {
                var go = new GameObject("Music_" + i);
                go.SetActive(false); // [integrator] configure before OnEnable: playOnAwake on a clip-less source logs
                                     // "Only custom filters can be played..." at every session start
                go.transform.SetParent(parent, false);
                var s = go.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.loop = true;
                s.spatialBlend = 0f;
                s.priority = 0;
                s.ignoreListenerPause = true;
                s.bypassReverbZones = true;
                s.dopplerLevel = 0f;
                s.volume = 0f;
                _src[i] = s;
                if (_filters)
                {
                    var lp = go.AddComponent<AudioLowPassFilter>();
                    lp.cutoffFrequency = OpenCutoff;
                    lp.lowpassResonanceQ = 1f;
                    lp.enabled = false;
                    _lp[i] = lp;
                }
                go.SetActive(true);
            }
        }

        /// <summary>Starts background rendering of a track without playing it.</summary>
        public void Preload(MusicId id)
        {
            if (id == MusicId.None || _clips.ContainsKey(id) || _rendering.Contains(id) || _factory == null) return;
            var spec = MusicSongs.Build(id);
            if (spec == null) return;
            _rendering.Add(id);
            Renders++;
            var md = new MusicData();
            _factory.Submit(new ClipFactory.Job
            {
                Name = "music_" + id,
                Channels = 2,
                Priority = true,                              // synchronous mode: before any queued SFX
                Steps = MusicSynth.RenderSteps(spec, md),     // incremental: time-sliced without threads
                Render = () => md.Interleaved,
                OnReady = clip => OnRendered(id, clip)
            });
        }

        void OnRendered(MusicId id, AudioClip clip)
        {
            _rendering.Remove(id);
            if (clip == null) { if (_pending == id) _pending = MusicId.None; return; }
            if (_clips.TryGetValue(id, out var old) && old != null) Object.Destroy(clip);
            else _clips[id] = clip;
            Touch(id);
            if (_pending == id) StartCrossfade();
            Evict();
        }

        public void Play(MusicId id, float fade)
        {
            if (id == MusicId.None) { Stop(fade); return; }
            ClearDuck();
            fade = Mathf.Max(0.02f, fade);
            if (id == _current && _pending == MusicId.None)
            {
                _target[_active] = 1f;
                _speed[_active] = 1f / fade;
                return;
            }
            _pending = id;
            _pendingFade = fade;
            if (_clips.ContainsKey(id)) StartCrossfade();
            else Preload(id);
        }

        void StartCrossfade()
        {
            var id = _pending;
            _pending = MusicId.None;
            if (!_clips.TryGetValue(id, out var clip) || clip == null) return;
            int next = 1 - _active;
            var s = _src[next];
            s.Stop();
            s.clip = clip;
            s.loop = true;
            s.time = 0f;
            s.Play();
            _fade[next] = 0f; _target[next] = 1f; _speed[next] = 1f / _pendingFade;
            _target[_active] = 0f; _speed[_active] = 1f / _pendingFade;
            _srcId[next] = id;
            _active = next;
            _current = id;
            Touch(id);
        }

        public void Stop(float fade)
        {
            _pending = MusicId.None;
            _current = MusicId.None;
            float sp = 1f / Mathf.Max(0.02f, fade);
            for (int i = 0; i < 2; i++) { _target[i] = 0f; _speed[i] = sp; }
        }

        /// <summary>Duck the music to level for seconds (seconds &lt; 0 => until the next Play/ClearDuck).</summary>
        public void Duck(float level, float seconds, float now)
        {
            _duckLevel = Mathf.Clamp01(level);
            _duckUntil = seconds < 0f ? float.MaxValue : now + seconds;
        }

        public void ClearDuck() { _duckUntil = -1f; _duckLevel = 1f; }

        public void Tick(float now, float dt, float muffle01, float slowMo01, float musicGain)
        {
            float duckTarget = now < _duckUntil ? _duckLevel : 1f;
            _duck = Mathf.MoveTowards(_duck, duckTarget, dt * (duckTarget < _duck ? 6f : 1.5f));
            Pitch = Mathf.Lerp(1f, SlowMoPitch, Mathf.Clamp01(slowMo01));
            float m = Mathf.Clamp01(muffle01);
            Cutoff = OpenCutoff * Mathf.Pow(MuffledCutoff / OpenCutoff, m);
            bool muffle = m > 0.005f && _filters;
            MuffleGainNow = MuffleGain(m, _filters);
            for (int i = 0; i < 2; i++)
            {
                _fade[i] = Mathf.MoveTowards(_fade[i], _target[i], _speed[i] * dt);
                var s = _src[i];
                if (_fade[i] <= 0f && _target[i] <= 0f)
                {
                    if (s.clip != null) { s.Stop(); s.clip = null; _srcId[i] = MusicId.None; }
                    continue;
                }
                s.volume = Mathf.Sin(_fade[i] * Mathf.PI * 0.5f) * musicGain * BusGain * _duck * MuffleGainNow;
                s.pitch = Pitch;
                var lp = _lp[i];
                if (lp == null) continue;
                if (lp.enabled != muffle) lp.enabled = muffle;
                if (muffle) lp.cutoffFrequency = Cutoff;
            }
        }

        void Touch(MusicId id) { _lru.Remove(id); _lru.Add(id); }

        void Evict()
        {
            int guard = 8;
            while ((_clips.Count > MaxCached || (_clips.Count > 1 && CachedMB > MaxCachedMB)) && guard-- > 0)
            {
                MusicId victim = MusicId.None;
                MusicId newest = _lru.Count > 0 ? _lru[_lru.Count - 1] : MusicId.None;
                foreach (var id in _lru)
                {
                    if (id == _current || id == _pending || id == _srcId[0] || id == _srcId[1] || id == newest) continue;
                    victim = id;
                    break;
                }
                if (victim == MusicId.None) break;
                if (_clips.TryGetValue(victim, out var c) && c != null) Object.Destroy(c);
                _clips.Remove(victim);
                _lru.Remove(victim);
                Evictions++;
            }
        }

        public void Dispose()
        {
            foreach (var kv in _clips) if (kv.Value != null) Object.Destroy(kv.Value);
            _clips.Clear();
            _lru.Clear();
        }
    }
}
