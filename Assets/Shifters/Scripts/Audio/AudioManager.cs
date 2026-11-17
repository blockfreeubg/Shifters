// =====================================================================================
//  SHIFTERS - AudioManager.cs   [CONTRACT API - implementation owned by the AUDIO agent]
//  Persistent (created by GameBootstrap). ALL audio is procedurally synthesized at runtime
//  (no audio files needed): SFX library, gibberish character voices and per-theme music loops.
//  Use the static Sfx helper from gameplay code (null-safe).
//
//  Plays AUTOMATICALLY (do not double-play these from gameplay/UI):
//    GameEvents.PuzzleSolved      -> SfxId.PuzzleSolved jingle (+ short music duck)
//    GameEvents.CheckpointReached -> SfxId.Checkpoint jingle (+ duck)
//    GameEvents.LevelCompleted    -> SfxId.LevelComplete fanfare, then crossfades to MusicId.Victory
//    GameEvents.TeamWiped         -> sad trombone + music ducked until the next PlayMusic / LevelStarted
//    GameEvents.SwitcherOpenChanged -> SfxId.SwitcherOpen / SwitcherClose + music muffle/pitch ramp
//    GameEvents.PauseChanged      -> pauses 3D world sounds, muffles music (UI sounds keep playing)
//    GameEvents.SettingsChanged   -> re-reads GameSession.Settings volumes
//    Scene loaded and nobody called PlayMusic within 1 s -> plays the scene's music (MainMenu -> Menu,
//    level scenes -> LevelCatalog music).
//  Never blocks on rendering: a sound whose clip is not rendered yet (WebGL renders the bank
//  time-sliced during the first seconds) is requested with top priority and started as soon
//  as it is ready - within PendingMaxWait (stingers PendingStingerWait, loops: no limit) - or
//  skipped. Loop handles are valid (and IsLoopPlaying is true) while their clip renders.
// =====================================================================================
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shifters
{
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Instance = null;

        public const int Pool3DSize = 24, Pool2DSize = 6;
        /// <summary>3D world SFX pitch at full slow-mo (drama); UI sounds are unaffected.</summary>
        public const float SlowMoWorldPitch = 0.82f;
        public const double PumpBudgetMs = 1.5;
        /// <summary>Repeats of one SfxId inside its cooldown closer than this (m) merge into one voice.</summary>
        public const float CollapseRadius = 3f;
        /// <summary>Max starts of one SfxId per frame (distant repeats included).</summary>
        public const int MaxStartsPerFrame = 3;
        const VoiceLine ChatterLine = (VoiceLine)99;
        /// <summary>Sounds waiting for their clip: max entries; longest wait before a one-shot is skipped (s).</summary>
        public const int MaxPending = 32;
        public const float PendingMaxWait = 0.35f, PendingStingerWait = 2.5f;

        public ClipFactory Factory { get; private set; }
        public SfxBank Bank { get; private set; }
        public VoiceBank Voices { get; private set; }
        public MusicPlayer Music { get; private set; }
        public AudioVoicePool Pool { get; private set; }
        /// <summary>Campaign preset voices (Max, Percy, ghosts, PA, robots) - see PlayPresetVoice / SpeakLine.</summary>
        public PresetVoiceBank PresetVoices { get; private set; }
        /// <summary>When true (default) every GameEvents.StoryLineShown is voiced automatically (SpeakLine) and
        /// StoryLineHidden stops the babble. Story code should then NOT play voices for its lines itself.</summary>
        public bool AutoStoryVoices = true;
        /// <summary>Lazy SFX groups follow the scene's theme on load (Bank -> Heist, Night/Spooky -> Scary).</summary>
        public bool AutoCampaignGroups = true;

        // ------------------------------------------------------------------ debug counters (tests / overlay)
        public int PlayedCount { get; private set; }
        public int CollapsedCount { get; private set; }
        public int RejectedCount { get; private set; }
        public int VoiceCount { get; private set; }
        public int StingerCount { get; private set; }
        /// <summary>Requests that had to wait for their clip / that then started / that were skipped (not ready in time).</summary>
        public int DeferredCount { get; private set; }
        public int DeferredStarted { get; private set; }
        public int DeferredSkipped { get; private set; }
        /// <summary>Longest wait (s, unscaled) of a deferred request that started.</summary>
        public float MaxDeferWait { get; private set; }
        public int PendingCount => _pendingCount;
        public float MasterVolume => _master;
        public float MusicVolume => _music;
        public float SfxVolume => _sfx;
        public float SlowMoAmount => _slowMo;
        public bool Paused => _paused;
        public MusicId CurrentMusic => Music != null ? Music.Current : MusicId.None;
        /// <summary>How many times id actually started playing (collapsed/rejected requests excluded).</summary>
        public int StartCount(SfxId id) { int i = (int)id; return _starts != null && i > 0 && i < _starts.Length ? _starts[i] : 0; }

        float _master = 0.8f, _music = 0.55f, _sfx = 0.8f;
        float _slowMo, _slowMoTarget;
        bool _paused, _musicRequested;
        float _victoryAt = -1f, _autoMusicAt = -1f, _nextListenerCheck;
        MusicId _autoMusic = MusicId.None;
        int _noListenerChecks;
        float[] _lastStart;
        int[] _frameOf, _framedStarts, _starts;
        AudioRng _rng = new AudioRng(777);
        Transform _root;
        AudioListener _fallbackListener;

        /// <summary>A request whose clip was not rendered yet (see header).</summary>
        struct PendingSfx
        {
            public SfxId Id;
            public int Special;            // >= 0: SfxSpecial stinger
            public bool Spatial, Loop, HasFollow;
            public Vector3 Pos, Offset;
            public Transform Follow;
            public float Volume, Pitch, Since, Deadline;
            public int Handle;
        }
        readonly PendingSfx[] _pending = new PendingSfx[MaxPending];
        int _pendingCount;

        protected virtual void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            AudioListener.pause = false;
            _root = new GameObject("[Audio]").transform;
            _root.SetParent(transform, false);
            Factory = new ClipFactory();
            Bank = new SfxBank(Factory);
            Voices = new VoiceBank(Factory);
            Pool = new AudioVoicePool(_root, Pool3DSize, Pool2DSize);
            Music = new MusicPlayer(_root, Factory);
            PresetVoices = new PresetVoiceBank(Factory);
            _lastStart = new float[SfxTable.Count];
            _frameOf = new int[SfxTable.Count];
            _framedStarts = new int[SfxTable.Count];
            _starts = new int[SfxTable.Count];
            for (int i = 0; i < _lastStart.Length; i++) { _lastStart[i] = -999f; _frameOf[i] = -1; }
            Bank.StartPrewarm();
        }

        void OnEnable()
        {
            if (Instance != this) return;
            GameEvents.PuzzleSolved += OnPuzzleSolved;
            GameEvents.CheckpointReached += OnCheckpoint;
            GameEvents.LevelCompleted += OnLevelCompleted;
            GameEvents.TeamWiped += OnTeamWiped;
            GameEvents.SwitcherOpenChanged += OnSwitcherOpen;
            GameEvents.PauseChanged += OnPause;
            GameEvents.SettingsChanged += ReadSettings;
            GameEvents.LevelStarted += OnLevelStarted;
            SceneManager.sceneLoaded += OnSceneLoaded;
            GameEvents.StoryLineShown += OnStoryLineShown;
            GameEvents.StoryLineHidden += OnStoryLineHidden;
        }

        void OnDisable()
        {
            GameEvents.PuzzleSolved -= OnPuzzleSolved;
            GameEvents.CheckpointReached -= OnCheckpoint;
            GameEvents.LevelCompleted -= OnLevelCompleted;
            GameEvents.TeamWiped -= OnTeamWiped;
            GameEvents.SwitcherOpenChanged -= OnSwitcherOpen;
            GameEvents.PauseChanged -= OnPause;
            GameEvents.SettingsChanged -= ReadSettings;
            GameEvents.LevelStarted -= OnLevelStarted;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            GameEvents.StoryLineShown -= OnStoryLineShown;
            GameEvents.StoryLineHidden -= OnStoryLineHidden;
        }

        void Start()
        {
            ReadSettings();
            _nextListenerCheck = Time.unscaledTime + 1.5f;
        }

        protected virtual void OnDestroy()
        {
            if (Instance != this) return;
            Instance = null;
            AudioListener.pause = false;
            for (int k = 0; k < _pendingCount; k++) _pending[k] = default;
            _pendingCount = 0;
            Factory?.Dispose();
            Pool?.StopAll();
            Bank?.Dispose();
            Voices?.Dispose();
            Music?.Dispose();
            PresetVoices?.Dispose();
        }

        // ------------------------------------------------------------------ contract API

        /// <summary>2D (non-positional) sound.</summary>
        public virtual void Play(SfxId id, float volume = 1f, float pitch = 1f) => Emit(id, false, Vector3.zero, null, volume, pitch, false);

        /// <summary>3D positional sound.</summary>
        public virtual void PlayAt(SfxId id, Vector3 position, float volume = 1f, float pitch = 1f) => Emit(id, true, position, null, volume, pitch, false);

        /// <summary>Gibberish voice line in the character's voice (pitch from profile).</summary>
        public virtual void PlayVoice(CharacterProfile who, VoiceLine line, Vector3 position) => PlayVoiceAt(who, line, position, null, Vector3.zero);

        public virtual void PlayMusic(MusicId id, float fadeSeconds = 1f)
        {
            if ((id == MusicId.Boss || id == MusicId.FinalBoss) && Bank != null) Bank.PrewarmGroup(SfxGroup.Boss);
            _musicRequested = true;
            _victoryAt = -1f;
            Music?.Play(id, fadeSeconds);
        }

        public virtual void StopMusic(float fadeSeconds = 1f)
        {
            _musicRequested = true;
            _victoryAt = -1f;
            Music?.Stop(fadeSeconds);
        }

        /// <summary>0..1: muffles/pitches music while the switcher is open (also bends 3D SFX pitch slightly).</summary>
        public virtual void SetSlowMoAmount(float t01) { _slowMo = _slowMoTarget = Mathf.Clamp01(t01); }

        public virtual void ApplyVolumes(float master, float music, float sfx)
        {
            _master = Mathf.Clamp01(master);
            _music = Mathf.Clamp01(music);
            _sfx = Mathf.Clamp01(sfx);
        }

        // ------------------------------------------------------------------ extended API

        /// <summary>3D sound that follows a transform (e.g. a falling character, a moving platform).</summary>
        public AudioVoice PlayAttached(SfxId id, Transform follow, float volume = 1f, float pitch = 1f, Vector3 offset = default)
        {
            var v = Emit(id, true, follow != null ? follow.position + offset : Vector3.zero, follow, volume, pitch, false, 0, offset);
            if (v != null) v.FollowOffset = offset;
            return v;
        }

        /// <summary>Starts a looping sound (made for ElevatorHum, Conveyor, Siren, Alarm; works with any id).
        /// Returns a handle for StopLoop/SetLoopVolume (0 = could not start). follow may be null.</summary>
        public int PlayLoop(SfxId id, Vector3 position, float volume = 1f, Transform follow = null, bool spatial = true)
        {
            if (Pool == null || Bank == null) return 0;
            int h = Pool.NewHandle();
            var v = Emit(id, spatial, position, follow, volume, 1f, true, h);
            if (v != null) { BeginLoop(v, h); return h; }
            return PendingIndex(h) >= 0 ? h : 0;   // clip still rendering: the loop starts as soon as it is ready
        }

        static void BeginLoop(AudioVoice v, int handle)
        {
            v.Handle = handle;
            v.FadeFrom = 0f; v.FadeTo = 1f; v.FadeT = 0f; v.FadeDur = 0.15f; v.Fade = 0f;
        }

        public void StopLoop(int handle, float fadeSeconds = 0.25f)
        {
            if (Pool == null) return;
            int k = PendingIndex(handle);
            if (k >= 0) { RemovePendingAt(k); return; }
            var v = Pool.FindHandle(handle);
            if (v != null) Pool.FadeOut(v, fadeSeconds);
        }

        public void SetLoopVolume(int handle, float volume)
        {
            int k = PendingIndex(handle);
            if (k >= 0) { _pending[k].Volume = Mathf.Clamp01(volume); return; }
            var v = Pool != null ? Pool.FindHandle(handle) : null;
            if (v != null) v.BaseVolume = Mathf.Clamp01(volume) * SfxTable.Get((SfxId)Mathf.Max(0, v.Id)).Volume;
        }

        /// <summary>True while the loop plays - or waits for its clip to finish rendering (it will start by itself).</summary>
        public bool IsLoopPlaying(int handle) => Pool != null && (Pool.FindHandle(handle) != null || PendingIndex(handle) >= 0);

        /// <summary>Voice line, optionally following a transform (offset = mouth height above it).</summary>
        public AudioVoice PlayVoiceAt(CharacterProfile who, VoiceLine line, Vector3 position, Transform follow, Vector3 offset, float volume = 1f)
        {
            if (who == null || Voices == null || Pool == null) return null;
            int pri = LinePriority(line);
            if (!ClaimOwner(who, pri, line)) return null;
            var clip = Voices.GetLine(who, line);
            return StartVoiceClip(who, clip, pri, line, position, follow, offset, volume);
        }

        /// <summary>Speech-bubble gibberish: syllable count follows the text length (max 1.5 s).</summary>
        public AudioVoice PlayChatter(CharacterProfile who, string text, Vector3 position, Transform follow, Vector3 offset, float volume = 1f)
        {
            if (who == null || Voices == null || Pool == null) return null;
            const int pri = 3;
            if (!ClaimOwner(who, pri, ChatterLine)) return null;
            var clip = Voices.GetChatter(who, text);
            return StartVoiceClip(who, clip, pri, ChatterLine, position, follow, offset, volume);
        }

        /// <summary>Starts background rendering of a music track so PlayMusic starts instantly later.</summary>
        public void PreloadMusic(MusicId id) => Music?.Preload(id);

        public void PlaySpecial(SfxSpecial s, float volume = 0.7f)
        {
            if (Bank == null || Pool == null) return;
            var clip = Bank.GetSpecial(s);
            if (clip == null) { Defer(SfxId.None, (int)s, false, Vector3.zero, null, Vector3.zero, volume, 1f, false, 0); return; }
            var v = Pool.Acquire(false, 10);
            if (v == null) return;
            Begin(v, clip, -2, 10, volume, 1f, Vector3.zero, null, false, true);
            StingerCount++;
        }

        /// <summary>Approximate PCM memory of all cached clips (MB, float32).</summary>
        public float CachedMemoryMB =>
            (Bank != null ? Bank.MemoryMB : 0f) + (Voices != null ? Voices.MemoryMB : 0f) + (Music != null ? Music.CachedSamples * 4f / (1024f * 1024f) : 0f)
            + (PresetVoices != null ? PresetVoices.MemoryMB : 0f);

        // ------------------------------------------------------------------ internals

        static int LinePriority(VoiceLine l)
        {
            switch (l)
            {
                case VoiceLine.Lost: return 9;
                case VoiceLine.Scream: return 8;
                case VoiceLine.KO: return 7;
                case VoiceLine.Thrown: return 6;
                case VoiceLine.Hurt: case VoiceLine.Victory: return 5;
                case VoiceLine.Denied: case VoiceLine.Laugh: case VoiceLine.Strain: case VoiceLine.Taunt: return 4;
                case VoiceLine.Jump: return 2;
                case VoiceLine.Idle: return 1;
                default: return 3;
            }
        }

        /// <summary>One voice per character: a new line replaces the current one unless the current one matters more.</summary>
        bool ClaimOwner(object owner, int pri, VoiceLine line)
        {
            var cur = Pool.FindOwner(owner);
            if (cur == null) return true;
            if (pri < cur.OwnerPriority) return false;
            if (cur.Line == line && Time.unscaledTime - cur.Start < 0.2f) return false;
            Pool.Release(cur);
            return true;
        }

        AudioVoice StartVoiceClip(CharacterProfile who, AudioClip clip, int pri, VoiceLine line, Vector3 position, Transform follow, Vector3 offset, float volume)
        {
            if (clip == null) return null;
            var v = Pool.Acquire(true, 7);
            if (v == null) { RejectedCount++; return null; }
            Begin(v, clip, -1, 7, 0.85f * volume, 1f + _rng.Range(-0.02f, 0.02f), follow != null ? follow.position + offset : position, follow, false, false);
            v.FollowOffset = offset;
            v.Owner = who;
            v.OwnerPriority = pri;
            v.Line = line;
            VoiceCount++;
            return v;
        }

        AudioVoice Emit(SfxId id, bool spatial, Vector3 pos, Transform follow, float volume, float pitch, bool loop, int handle = 0, Vector3 offset = default)
        {
            if (id == SfxId.None || Bank == null || Pool == null) return null;
            int i = (int)id;
            if (i <= 0 || i >= _lastStart.Length) return null;
            var info = SfxTable.Get(id);
            float now = Time.unscaledTime;
            float vol = Mathf.Clamp(volume, 0f, 2f) * info.Volume;
            if (info.Group == SfxGroup.Scary || id == SfxId.Whisper)
            {
                // CAMPAIGN 10.1 rule 7 (SPOOKY: MILD): no whispers/breathing, softer stingers.
                if (SpookyMild) { if (id == SfxId.Whisper) { RejectedCount++; return null; } if (id == SfxId.ScareStinger) vol *= 0.7f; }
                if (id == SfxId.ScareStinger) vol = Mathf.Min(vol, MaxStingerVolume);   // rule 4: never above the SFX bus
            }
            int frame = Time.frameCount;
            if (_frameOf[i] != frame) { _frameOf[i] = frame; _framedStarts[i] = 0; }
            if (!loop && now - _lastStart[i] < info.Cooldown)
            {
                // Spam collapse: an identical sound started a moment ago NEAR this spot (or 2D) - or this id already
                // started MaxStartsPerFrame times this frame (10 pellet impacts) - is merged into the newest instance,
                // which just becomes as loud as the loudest request. Distant repeats (3 characters' footsteps) play.
                var newest = Pool.FindById(i, true);
                bool near = !spatial || newest == null || !newest.Is3D || (newest.Tr.position - pos).sqrMagnitude < CollapseRadius * CollapseRadius;
                if (near || _framedStarts[i] >= MaxStartsPerFrame)
                {
                    if (newest != null && vol > newest.BaseVolume) newest.BaseVolume = Mathf.Min(1.5f, vol);
                    CollapsedCount++;
                    return null;
                }
            }
            var clip = Bank.Get(id);   // never renders here: null = still rendering (requested with top priority)
            if (clip == null)
            {
                Defer(id, -1, spatial, pos, follow, offset, volume, pitch, loop, handle);
                return null;
            }
            if (Pool.CountActive(i) >= info.MaxInstances) Pool.Release(Pool.FindById(i, false));
            var v = Pool.Acquire(spatial, info.Importance);
            if (v == null) { RejectedCount++; return null; }
            _lastStart[i] = now;
            _framedStarts[i]++;
            _starts[i]++;
            float jit = info.Jitter > 0f ? 1f + _rng.Range(-info.Jitter, info.Jitter) : 1f;
            Begin(v, clip, i, info.Importance, vol, pitch * jit, pos, follow, loop, info.UI);
            if (id == SfxId.PuzzleSolved || id == SfxId.Checkpoint || id == SfxId.LevelComplete) StingerCount++;
            return v;
        }

        void Begin(AudioVoice v, AudioClip clip, int id, int importance, float vol, float pitch, Vector3 pos, Transform follow, bool loop, bool ui)
        {
            float now = Time.unscaledTime;
            v.Busy = true; v.Id = id; v.Importance = importance; v.BaseVolume = vol;
            v.BasePitch = Mathf.Clamp(pitch, 0.1f, 3f); v.Loop = loop; v.UI = ui || !v.Is3D;
            v.Follow = follow; v.FollowOffset = Vector3.zero; v.Owner = null; v.OwnerPriority = 0; v.Handle = 0;
            v.Fade = 1f; v.FadeDur = 0f; v.StopAfterFade = false; v.Start = now;
            float world = v.UI ? 1f : Mathf.Lerp(1f, SlowMoWorldPitch, _slowMo);
            float effPitch = v.BasePitch * world;
            v.End = loop ? float.MaxValue : now + clip.length / Mathf.Max(0.1f, v.BasePitch * (v.UI ? 1f : SlowMoWorldPitch)) + 0.05f;
            if (v.Is3D) v.Tr.position = pos;
            var s = v.Src;
            s.clip = clip;
            s.loop = loop;
            s.volume = vol * v.Fade * _sfx * _master;
            s.pitch = effPitch;
            s.priority = Mathf.Clamp(128 - importance * 12, 0, 256);
            s.Play();
            PlayedCount++;
        }

        // ------------------------------------------------------------------ sounds waiting for their clip

        /// <summary>Remembers a request until its clip is rendered. Spam while it renders merges into one entry
        /// (same id, 2D or within CollapseRadius), like the cooldown collapse does for playing sounds.</summary>
        void Defer(SfxId id, int special, bool spatial, Vector3 pos, Transform follow, Vector3 offset, float volume, float pitch, bool loop, int handle)
        {
            float now = Time.unscaledTime;
            if (!loop)
                for (int k = 0; k < _pendingCount; k++)
                {
                    ref var p = ref _pending[k];
                    if (p.Loop || p.Id != id || p.Special != special) continue;
                    if (spatial && p.Spatial && (p.Pos - pos).sqrMagnitude >= CollapseRadius * CollapseRadius) continue;
                    if (volume > p.Volume) p.Volume = volume;
                    CollapsedCount++;
                    return;
                }
            if (_pendingCount >= MaxPending) { RejectedCount++; return; }
            bool stinger = special >= 0 || SfxTable.Get(id).Importance >= 10;
            _pending[_pendingCount++] = new PendingSfx
            {
                Id = id, Special = special, Spatial = spatial, Loop = loop, Pos = pos, Offset = offset,
                Follow = follow, HasFollow = follow != null, Volume = volume, Pitch = pitch, Handle = handle,
                Since = now, Deadline = loop ? float.MaxValue : now + (stinger ? PendingStingerWait : PendingMaxWait)
            };
            DeferredCount++;
        }

        int PendingIndex(int handle)
        {
            if (handle <= 0) return -1;
            for (int k = 0; k < _pendingCount; k++) if (_pending[k].Handle == handle) return k;
            return -1;
        }

        void RemovePendingAt(int k)
        {
            _pending[k] = _pending[--_pendingCount];
            _pending[_pendingCount] = default;
        }

        /// <summary>Once per frame after the factory pump: starts requests whose clip is ready, drops expired ones.</summary>
        void ServicePending(float now)
        {
            for (int k = 0; k < _pendingCount;)
            {
                var p = _pending[k];
                bool ready = p.Special >= 0 ? Bank.IsSpecialReady((SfxSpecial)p.Special) : Bank.HasReady(p.Id);
                bool orphan = p.Loop && p.HasFollow && p.Follow == null;   // its owner is gone (should have stopped it)
                if (!ready && !orphan && now <= p.Deadline) { k++; continue; }
                RemovePendingAt(k);
                if (!ready || orphan) { DeferredSkipped++; continue; }
                DeferredStarted++;
                if (now - p.Since > MaxDeferWait) MaxDeferWait = now - p.Since;
                if (p.Special >= 0) { PlaySpecial((SfxSpecial)p.Special, p.Volume); continue; }
                Transform f = p.HasFollow && p.Follow != null ? p.Follow : null;
                var v = Emit(p.Id, p.Spatial, f != null ? f.position + p.Offset : p.Pos, f, p.Volume, p.Pitch, p.Loop, p.Handle, p.Offset);
                if (v == null) continue;
                if (f != null) v.FollowOffset = p.Offset;
                if (p.Loop) BeginLoop(v, p.Handle);
            }
        }

        void ReadSettings()
        {
            var gs = GameSession.Instance;
            if (gs != null && gs.Settings != null) ApplyVolumes(gs.Settings.MasterVolume, gs.Settings.MusicVolume, gs.Settings.SfxVolume);
        }

        void OnPuzzleSolved(string _) { Play(SfxId.PuzzleSolved); Music?.Duck(0.5f, 1.3f, Time.unscaledTime); }
        void OnCheckpoint(int _) { Play(SfxId.Checkpoint); Music?.Duck(0.55f, 1.2f, Time.unscaledTime); }

        void OnLevelCompleted(LevelResult _)
        {
            Play(SfxId.LevelComplete);
            Music?.Duck(0.2f, 2.6f, Time.unscaledTime);
            _victoryAt = Time.unscaledTime + 2.2f;
            Music?.Preload(MusicId.Victory);
        }

        void OnTeamWiped() { PlaySpecial(SfxSpecial.SadTrombone); Music?.Duck(0.25f, -1f, Time.unscaledTime); }
        void OnLevelStarted() { Music?.ClearDuck(); }

        void OnSwitcherOpen(bool open)
        {
            Play(open ? SfxId.SwitcherOpen : SfxId.SwitcherClose);
            _slowMoTarget = open ? 1f : 0f;
        }

        void OnPause(bool paused)
        {
            _paused = paused;
            AudioListener.pause = paused;
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single) return;
            _musicRequested = false;
            _autoMusic = AutoMusicFor(scene.name);
            _autoMusicAt = _autoMusic != MusicId.None ? Time.unscaledTime + 1f : -1f;
            if (_autoMusic != MusicId.None) Music?.Preload(_autoMusic);
            _nextListenerCheck = Time.unscaledTime + 1.5f;
            _noListenerChecks = 0;
            if (AutoCampaignGroups) ApplyCampaignGroups(scene.name);
            StopPresetVoice(0.05f);
            _babblePending = false;
        }

        static MusicId AutoMusicFor(string scene)
        {
            if (string.IsNullOrEmpty(scene)) return MusicId.None;
            if (scene == LevelCatalog.MainMenuScene) return MusicId.Menu;
            var info = LevelCatalog.Get(LevelCatalog.IndexOfScene(scene));
            return info != null ? info.Music : MusicId.None;
        }

        void Update()
        {
            if (Instance != this || Factory == null) return;
            float now = Time.unscaledTime, dt = Time.unscaledDeltaTime;
            Factory.Pump(PumpBudgetMs);
            if (_pendingCount > 0) ServicePending(now);
            if (_slowMo != _slowMoTarget) _slowMo = Mathf.MoveTowards(_slowMo, _slowMoTarget, dt / Mathf.Max(0.01f, Tuning.SwitcherRampTime));
            Pool.Tick(now, dt, _paused, Mathf.Lerp(1f, SlowMoWorldPitch, _slowMo), _sfx * _master);
            Music.Tick(now, dt, Mathf.Max(_slowMo, _paused ? 0.55f : 0f), _slowMo, _music * _master);
            if (_victoryAt > 0f && now >= _victoryAt) { _victoryAt = -1f; Music.Play(MusicId.Victory, 1.5f); }
            if (_autoMusicAt > 0f && now >= _autoMusicAt)
            {
                _autoMusicAt = -1f;
                if (!_musicRequested && _autoMusic != MusicId.None) Music.Play(_autoMusic, 1.5f);
            }
            if (now >= _nextListenerCheck) CheckListener(now);
            if (_babblePending) ServiceBabble(now);
        }

        void LateUpdate()
        {
            if (Instance != this) return;
            if (_fallbackListener == null || !_fallbackListener.enabled) return;
            var cam = Camera.main;
            if (cam != null) _fallbackListener.transform.SetPositionAndRotation(cam.transform.position, cam.transform.rotation);
        }

        /// <summary>Never crash or go silent without a listener: enable a fallback listener that tracks Camera.main.</summary>
        void CheckListener(float now)
        {
            _nextListenerCheck = now + 1f;
            var all = FindObjectsByType<AudioListener>();
            int others = 0;
            foreach (var l in all) if (l != _fallbackListener && l.isActiveAndEnabled) others++;
            if (others > 0)
            {
                _noListenerChecks = 0;
                if (_fallbackListener != null && _fallbackListener.enabled) _fallbackListener.enabled = false;
                return;
            }
            if (++_noListenerChecks < 2) return;
            if (_fallbackListener == null)
            {
                var go = new GameObject("[FallbackAudioListener]");
                go.transform.SetParent(_root, false);
                _fallbackListener = go.AddComponent<AudioListener>();
            }
            _fallbackListener.enabled = true;
        }

        // ================================================================== CAMPAIGN (THE GREAT SHIFT)
        /// <summary>Highest playback gain a ScareStinger may get (= the loudest regular SFX table volume, CAMPAIGN 10.1 rule 4).</summary>
        public const float MaxStingerVolume = 0.6f;
        /// <summary>Babble requests older than this when their clip becomes ready are dropped (the subtitle moved on).</summary>
        public const float BabbleMaxWait = 1.2f;
        public const float BlipInterval = 0.055f;
        const int BabbleId = -3;

        bool _babblePending;
        long _babbleKey, _lastBabbleKey;
        float _babbleSince, _lastBabbleAt = -99f, _lastBlip = -99f;
        VoicePreset _babblePreset;
        float _babblePitch, _babbleVolume;
        string _babbleText;
        VoiceLine _babbleLine;
        bool _babbleIsLine;
        Transform _babbleFollow;
        Vector3 _babbleOffset;
        AudioVoice _babbleVoice;
        float _babbleStart;
        static CharacterProfile _genericSpeaker;

        public int BabbleCount { get; private set; }
        public int BabbleDropped { get; private set; }
        public int BlipCount { get; private set; }
        public string LastSpeakerResolved { get; private set; }

        /// <summary>SPOOKY: MILD setting (GameSession settings; false without a session).</summary>
        public static bool SpookyMild { get { var gs = GameSession.Instance; return gs != null && gs.Settings != null && gs.Settings.SpookyMild; } }

        /// <summary>True while a preset babble plays or waits for its clip.</summary>
        public bool PresetVoicePlaying => _babblePending || BabbleVoiceAlive;

        bool BabbleVoiceAlive => _babbleVoice != null && _babbleVoice.Busy && _babbleVoice.Id == BabbleId && Mathf.Approximately(_babbleVoice.Start, _babbleStart);

        /// <summary>Gibberish for a line of text in a preset voice. follow == null: 2D (broadcast / radio / narration);
        /// otherwise 3D at follow + offset (mouth). pitch 0 = the preset's default (Villain 0.62; pass 1.45 for the squeaky
        /// real voice). Never blocks: the clip renders time-sliced and starts when ready (dropped after BabbleMaxWait).
        /// A new babble replaces the current one; the same text again within 1 s is ignored. Returns false if ignored.</summary>
        public bool PlayPresetVoice(VoicePreset preset, string text, float pitch = 0f, Transform follow = null, Vector3 offset = default, float volume = 1f)
        {
            if (PresetVoices == null || Pool == null) return false;
            if (preset == VoicePreset.Ghost && SpookyMild) preset = VoicePreset.GhostMild;
            if (pitch <= 0f) pitch = VoicePresets.DefaultPitch(preset);
            long key = PresetVoiceBank.ChatterKey(preset, pitch, text);
            if (key == _lastBabbleKey && Time.unscaledTime - _lastBabbleAt < 1f && PresetVoicePlaying) return false;
            StopPresetVoice(0.08f);
            _babblePreset = preset; _babblePitch = pitch; _babbleText = text; _babbleIsLine = false;
            return QueueBabble(key, follow, offset, volume);
        }

        /// <summary>A canned VoiceLine (Laugh, Gasp, Denied, Victory, Taunt, Hurt...) in a preset voice (same rules as PlayPresetVoice).</summary>
        public bool PlayPresetLine(VoicePreset preset, VoiceLine line, float pitch = 0f, Transform follow = null, Vector3 offset = default, float volume = 1f)
        {
            if (PresetVoices == null || Pool == null) return false;
            if (preset == VoicePreset.Ghost && SpookyMild) preset = VoicePreset.GhostMild;
            if (pitch <= 0f) pitch = VoicePresets.DefaultPitch(preset);
            StopPresetVoice(0.05f);
            _babblePreset = preset; _babblePitch = pitch; _babbleLine = line; _babbleIsLine = true;
            return QueueBabble(PresetVoiceBank.LineKey(preset, pitch, line, 0) ^ 0x5A5A, follow, offset, volume);
        }

        /// <summary>Renders the babble for a line ahead of time (e.g. the next script line) so it starts instantly.</summary>
        public void PrewarmPresetVoice(VoicePreset preset, string text, float pitch = 0f)
        {
            if (preset == VoicePreset.Ghost && SpookyMild) preset = VoicePreset.GhostMild;
            PresetVoices?.GetChatter(preset, pitch, text);
        }

        public void StopPresetVoice(float fadeSeconds = 0.12f)
        {
            _babblePending = false;
            if (BabbleVoiceAlive && Pool != null) Pool.FadeOut(_babbleVoice, Mathf.Max(0.01f, fadeSeconds));
            _babbleVoice = null;
        }

        /// <summary>Voices a story line by its SpeakerId: presets via VoicePresets.TryResolveSpeaker (max / percy / ghost / pa /
        /// robot ...); interns by DisplayName, Name, archetype or a {BRUTE}/{SPRINTER}/{BRAINIAC} token (chatter in their
        /// own voice, at their mouth unless it is a broadcast); anything else gets a neutral 2D voice. Called automatically for
        /// GameEvents.StoryLineShown while AutoStoryVoices is on.</summary>
        public bool SpeakLine(StoryLine line)
        {
            string text = line.Text ?? "";
            if (VoicePresets.TryResolveSpeaker(line.SpeakerId, out var preset, out float pitch) ||
                VoicePresets.TryResolveSpeaker(line.SpeakerName, out preset, out pitch))
            {
                LastSpeakerResolved = "preset:" + preset + "@" + pitch.ToString("0.00");
                return PlayPresetVoice(preset, text, pitch, null, default, line.Broadcast ? 0.9f : 1f);
            }
            var ch = FindIntern(line.SpeakerId) ?? FindIntern(line.SpeakerName);
            StopPresetVoice(0.05f);
            if (ch != null && ch.Profile != null)
            {
                LastSpeakerResolved = "intern:" + ch.Profile.DisplayName;
                Vector3 mouth = Vector3.up * (ch.Height * 0.85f);
                var v = line.Broadcast ? PlayChatter(ch.Profile, text, Vector3.zero, null, Vector3.zero)
                                       : PlayChatter(ch.Profile, text, ch.transform.position + mouth, ch.transform, mouth);
                return v != null;
            }
            if (_genericSpeaker == null) _genericSpeaker = new CharacterProfile { Name = "Narrator", Archetype = Archetype.Brainiac, VoicePitch = 0.95f };
            LastSpeakerResolved = "generic";
            return PlayChatter(_genericSpeaker, text, Vector3.zero, null, Vector3.zero) != null;
        }

        /// <summary>Typewriter blip for the DialogueBox (call it per typed character; rate-limited to one per BlipInterval,
        /// pitched per speaker: Max low, squeaky Max / interns higher, Percy wobbly, ghosts soft).</summary>
        public void PlayDialogueBlip(string speakerId = null, float volume = 1f)
        {
            float now = Time.unscaledTime;
            if (now - _lastBlip < BlipInterval) return;
            _lastBlip = now;
            float pitch = 1f, vol = volume;
            if (VoicePresets.TryResolveSpeaker(speakerId, out var preset, out float vp))
            {
                switch (preset)
                {
                    case VoicePreset.Villain: pitch = vp > 1f ? 1.45f : 0.72f; break;
                    case VoicePreset.Percy: pitch = 0.85f + _rng.Range(0f, 0.3f); break;
                    case VoicePreset.Ghost: case VoicePreset.GhostMild: pitch = 1.25f; vol *= 0.6f; break;
                    case VoicePreset.Robot: pitch = 0.8f; break;
                    default: pitch = 0.9f; break;
                }
            }
            else
            {
                var ch = FindIntern(speakerId);
                if (ch != null && ch.Profile != null) pitch = Mathf.Clamp(ch.Profile.VoicePitch, 0.6f, 1.6f) * (ch.Archetype == Archetype.Brute ? 0.8f : ch.Archetype == Archetype.Sprinter ? 1.2f : 1f);
            }
            BlipCount++;
            Play(SfxId.DialogueBlip, vol, pitch * (1f + _rng.Range(-0.03f, 0.03f)));
        }

        /// <summary>Pre-warms / releases a lazy SFX group by hand (heist + scary are lazy; the rest are always cached).</summary>
        public int PrewarmSfxGroup(SfxGroup g) => Bank != null ? Bank.PrewarmGroup(g) : 0;
        public int ReleaseSfxGroup(SfxGroup g) => Bank != null ? Bank.ReleaseGroup(g) : 0;

        void ApplyCampaignGroups(string sceneName)
        {
            if (Bank == null) return;
            CampaignGroupsFor(sceneName, out bool boss, out bool heist, out bool scary);
            if (boss) Bank.PrewarmGroup(SfxGroup.Boss); else Bank.ReleaseGroup(SfxGroup.Boss);
            if (heist) Bank.PrewarmGroup(SfxGroup.Heist); else Bank.ReleaseGroup(SfxGroup.Heist);
            if (scary) Bank.PrewarmGroup(SfxGroup.Scary); else Bank.ReleaseGroup(SfxGroup.Scary);
        }

        /// <summary>Which lazy SFX groups a scene keeps rendered (AutoCampaignGroups applies this on every scene load;
        /// all other groups are always cached).</summary>
        public static void CampaignGroupsFor(string sceneName, out bool boss, out bool heist, out bool scary)
        {
            heist = false; scary = false; boss = false;
            int idx = string.IsNullOrEmpty(sceneName) || sceneName == LevelCatalog.MainMenuScene ? -1 : LevelCatalog.IndexOfScene(sceneName);
            var info = idx >= 0 ? LevelCatalog.Get(idx) : null;
            if (info != null)
            {
                heist = info.Theme == LevelTheme.Bank || info.Music == MusicId.Heist;
                scary = info.Theme == LevelTheme.Night || info.Music == MusicId.Spooky;
                boss = idx >= LevelCatalog.MainGameCount && (info.Music == MusicId.Villain || info.Music == MusicId.Boss || info.Music == MusicId.FinalBoss
                       || info.Music == MusicId.Finale || sceneName.IndexOf("Getaway", System.StringComparison.OrdinalIgnoreCase) >= 0);
            }
        }

        static ShifterCharacter FindIntern(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            var cm = CharacterManager.Instance;
            if (cm == null || cm.Team == null) return null;
            string s = id.Trim().Trim('{', '}');
            Archetype? arch = null;
            if (string.Equals(s, "BRUTE", System.StringComparison.OrdinalIgnoreCase) || string.Equals(s, "MUSCLE", System.StringComparison.OrdinalIgnoreCase)) arch = Archetype.Brute;
            else if (string.Equals(s, "SPRINTER", System.StringComparison.OrdinalIgnoreCase) || string.Equals(s, "WHEELS", System.StringComparison.OrdinalIgnoreCase)) arch = Archetype.Sprinter;
            else if (string.Equals(s, "BRAINIAC", System.StringComparison.OrdinalIgnoreCase) || string.Equals(s, "BRAINS", System.StringComparison.OrdinalIgnoreCase)) arch = Archetype.Brainiac;
            else if (string.Equals(s, "ACTIVE", System.StringComparison.OrdinalIgnoreCase)) return cm.Active;
            foreach (var c in cm.Team)
            {
                if (c == null || c.Profile == null) continue;
                if (arch.HasValue ? c.Archetype == arch.Value
                    : string.Equals(c.Profile.DisplayName, s, System.StringComparison.OrdinalIgnoreCase) || string.Equals(c.Profile.Name, s, System.StringComparison.OrdinalIgnoreCase))
                    return c;
            }
            return null;
        }

        bool QueueBabble(long key, Transform follow, Vector3 offset, float volume)
        {
            _babbleKey = key; _lastBabbleKey = key; _lastBabbleAt = Time.unscaledTime;
            _babbleFollow = follow; _babbleOffset = offset; _babbleVolume = volume;
            _babbleSince = Time.unscaledTime;
            _babblePending = true;
            ServiceBabble(Time.unscaledTime);   // starts at once when cached
            return true;
        }

        void ServiceBabble(float now)
        {
            if (!_babblePending || PresetVoices == null) return;
            var clip = _babbleIsLine ? PresetVoices.GetLine(_babblePreset, _babblePitch, _babbleLine)
                                     : PresetVoices.GetChatter(_babblePreset, _babblePitch, _babbleText);
            if (clip == null)
            {
                if (now - _babbleSince > BabbleMaxWait) { _babblePending = false; BabbleDropped++; }
                return;
            }
            _babblePending = false;
            bool spatial = _babbleFollow != null;
            var v = Pool.Acquire(spatial, 9);
            if (v == null) { RejectedCount++; return; }
            Vector3 pos = spatial ? _babbleFollow.position + _babbleOffset : Vector3.zero;
            Begin(v, clip, BabbleId, 9, 0.9f * _babbleVolume, 1f, pos, _babbleFollow, false, false);
            v.FollowOffset = _babbleOffset;
            _babbleVoice = v;
            _babbleStart = v.Start;
            BabbleCount++;
            VoiceCount++;
        }

        void OnStoryLineShown(StoryLine line) { if (AutoStoryVoices) SpeakLine(line); }
        void OnStoryLineHidden() { if (AutoStoryVoices) StopPresetVoice(0.15f); }
    }

    /// <summary>Null-safe shortcuts for gameplay code.</summary>
    public static class Sfx
    {
        public static void Play(SfxId id, float volume = 1f, float pitch = 1f) { if (AudioManager.Instance != null) AudioManager.Instance.Play(id, volume, pitch); }
        public static void At(SfxId id, Vector3 pos, float volume = 1f, float pitch = 1f) { if (AudioManager.Instance != null) AudioManager.Instance.PlayAt(id, pos, volume, pitch); }
        /// <summary>Voice line in the character's gibberish voice; follows the character while it plays.</summary>
        public static void Voice(ShifterCharacter c, VoiceLine line)
        {
            if (AudioManager.Instance != null && c != null && c.Profile != null)
                AudioManager.Instance.PlayVoiceAt(c.Profile, line, c.Center, c.transform, Vector3.up * (c.Height * 0.85f));
        }
        /// <summary>3D sound following a transform.</summary>
        public static void Attached(SfxId id, Transform follow, float volume = 1f, float pitch = 1f) { if (AudioManager.Instance != null) AudioManager.Instance.PlayAttached(id, follow, volume, pitch); }
        /// <summary>Looping sound; keep the handle and call Sfx.StopLoop(handle). Returns 0 when audio is unavailable.</summary>
        public static int Loop(SfxId id, Vector3 pos, float volume = 1f, Transform follow = null) => AudioManager.Instance != null ? AudioManager.Instance.PlayLoop(id, pos, volume, follow) : 0;
        public static void StopLoop(int handle, float fadeSeconds = 0.25f) { if (AudioManager.Instance != null && handle > 0) AudioManager.Instance.StopLoop(handle, fadeSeconds); }
        public static void LoopVolume(int handle, float volume) { if (AudioManager.Instance != null && handle > 0) AudioManager.Instance.SetLoopVolume(handle, volume); }

        // ---- campaign
        /// <summary>Preset gibberish (2D unless follow is given). Villain pitch: 0 = 0.62 deep, 1.45 = squeaky real voice.</summary>
        public static void Babble(VoicePreset preset, string text, float pitch = 0f, Transform follow = null, float volume = 1f)
        { if (AudioManager.Instance != null) AudioManager.Instance.PlayPresetVoice(preset, text, pitch, follow, follow != null ? Vector3.up * 1.6f : Vector3.zero, volume); }
        /// <summary>Canned line (Laugh, Gasp, Denied...) in a preset voice.</summary>
        public static void PresetLine(VoicePreset preset, VoiceLine line, float pitch = 0f, Transform follow = null)
        { if (AudioManager.Instance != null) AudioManager.Instance.PlayPresetLine(preset, line, pitch, follow, follow != null ? Vector3.up * 1.6f : Vector3.zero); }
        /// <summary>Voices a story line by SpeakerId (done automatically for GameEvents.StoryLineShown).</summary>
        public static void Speak(StoryLine line) { if (AudioManager.Instance != null) AudioManager.Instance.SpeakLine(line); }
        /// <summary>DialogueBox typewriter blip (call per typed character; rate-limited inside).</summary>
        public static void Blip(string speakerId = null, float volume = 1f) { if (AudioManager.Instance != null) AudioManager.Instance.PlayDialogueBlip(speakerId, volume); }
        /// <summary>Starts rendering a music track in the background (e.g. the next chapter's during Level Complete).</summary>
        public static void PreloadMusic(MusicId id) { if (AudioManager.Instance != null) AudioManager.Instance.PreloadMusic(id); }
    }
}
