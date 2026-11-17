// =====================================================================================
//  SHIFTERS - PresetVoiceBank.cs   [owned by the AUDIO agent]
//  Cache of preset voice clips (VoicePresets: Max, Percy, ghosts, PA, robots). Never renders on
//  the calling thread: a missing clip is queued (urgent, time-sliced VoiceRenderTask) and Get
//  returns null until it is ready. Bounded LRU (MaxClips) so story lines do not grow the PCM
//  footprint; clips used in the last few seconds are never evicted (they may be playing).
// =====================================================================================
using System.Collections.Generic;
using UnityEngine;

namespace Shifters
{
    public sealed class PresetVoiceBank
    {
        public const int MaxClips = 16;
        public const int ChatterVariants = 3;
        const float ProtectSeconds = 4f;

        sealed class Entry { public AudioClip Clip; public float LastUse; public long Samples; }

        readonly ClipFactory _factory;
        readonly Dictionary<long, Entry> _clips = new Dictionary<long, Entry>(32);
        readonly HashSet<long> _pending = new HashSet<long>();
        readonly List<long> _scratch = new List<long>(32);
        AudioRng _rng = new AudioRng(1987);

        public int ClipCount => _clips.Count;
        public long SampleCount { get; private set; }
        public int Renders { get; private set; }
        public int Evictions { get; private set; }
        public int PendingCount => _pending.Count;
        public float MemoryMB => SampleCount * 4f / (1024f * 1024f);

        public PresetVoiceBank(ClipFactory factory) { _factory = factory; }

        public static long ChatterKey(VoicePreset p, float pitch, string text)
        {
            int bucket = VoiceBank.PitchBucket(pitch <= 0f ? VoicePresets.DefaultPitch(p) : pitch);
            int syl = VoicePresets.SyllablesForText(text);
            int ending = VoiceSynth.EndingForText(text);
            int h = 17;
            if (text != null) for (int i = 0; i < text.Length; i++) h = h * 31 + text[i];
            int variant = (h & 0x7FFFFFFF) % ChatterVariants;
            return Key(p, 0, bucket, syl, ending, variant);
        }

        public static long LineKey(VoicePreset p, float pitch, VoiceLine line, int variant)
        {
            int bucket = VoiceBank.PitchBucket(pitch <= 0f ? VoicePresets.DefaultPitch(p) : pitch);
            return Key(p, 1 + (int)line, bucket, 0, 0, variant);
        }

        static long Key(VoicePreset p, int kind, int bucket, int syl, int ending, int variant) =>
            ((long)((int)p & 0xFF) << 48) | ((long)(kind & 0xFF) << 40) | ((long)(syl & 0xFF) << 32) | ((long)(bucket & 0xFF) << 16)
            | ((long)(ending & 0xF) << 8) | (long)(variant & 0xFF);

        /// <summary>Ready clip for the babble of text, or null (then it is queued; ask again next frame).</summary>
        public AudioClip GetChatter(VoicePreset p, float pitch, string text, bool request = true)
        {
            long key = ChatterKey(p, pitch, text);
            var c = Lookup(key);
            if (c != null || !request) return c;
            if (pitch <= 0f) pitch = VoicePresets.DefaultPitch(p);
            float bp = VoiceBank.BucketPitch(VoiceBank.PitchBucket(pitch));
            int syl = VoicePresets.SyllablesForText(text), ending = VoiceSynth.EndingForText(text);
            int variant = (int)(key & 0xFF);
            Queue(key, "pvoice_" + p + "_" + syl + "_" + variant, () => VoicePresets.Chatter(p, bp, syl, variant, ending));
            return null;
        }

        /// <summary>Ready clip for a canned line (variant -1 = random of 3), or null (queued).</summary>
        public AudioClip GetLine(VoicePreset p, float pitch, VoiceLine line, int variant = -1, bool request = true)
        {
            if (variant < 0 || variant >= 3) variant = _rng.Range(0, 3);
            long key = LineKey(p, pitch, line, variant);
            var c = Lookup(key);
            if (c != null) return c;
            for (int k = 1; k < 3; k++)
            {
                var other = Lookup(LineKey(p, pitch, line, (variant + k) % 3));
                if (other != null) { if (request) QueueLine(p, pitch, line, variant, key); return other; }
            }
            if (request) QueueLine(p, pitch, line, variant, key);
            return null;
        }

        void QueueLine(VoicePreset p, float pitch, VoiceLine line, int variant, long key)
        {
            if (pitch <= 0f) pitch = VoicePresets.DefaultPitch(p);
            float bp = VoiceBank.BucketPitch(VoiceBank.PitchBucket(pitch));
            Queue(key, "pvoice_" + p + "_" + line + "_" + variant, () => VoicePresets.Line(p, bp, line, variant));
        }

        public bool IsReady(long key) => _clips.ContainsKey(key);
        public bool IsPending(long key) => _pending.Contains(key);

        AudioClip Lookup(long key)
        {
            if (!_clips.TryGetValue(key, out var e)) return null;
            if (e.Clip == null) { _clips.Remove(key); SampleCount -= e.Samples; return null; }
            e.LastUse = Time.unscaledTime;
            return e.Clip;
        }

        void Queue(long key, string name, System.Func<VoiceRenderTask> make)
        {
            if (_factory == null || _pending.Contains(key) || _clips.ContainsKey(key)) return;
            _pending.Add(key);
            VoiceRenderTask task = null;
            var steps = new LazyVoiceSteps(() => task = make());
            _factory.SubmitUrgent(new ClipFactory.Job
            {
                Name = name,
                Steps = steps,
                Render = () => task != null ? task.Result : null,
                OnReady = clip =>
                {
                    _pending.Remove(key);
                    if (clip == null) return;
                    if (_clips.ContainsKey(key)) { Object.Destroy(clip); return; }
                    Renders++;
                    _clips[key] = new Entry { Clip = clip, LastUse = Time.unscaledTime, Samples = (long)clip.samples * clip.channels };
                    SampleCount += (long)clip.samples * clip.channels;
                    Trim();
                }
            });
        }

        void Trim()
        {
            float now = Time.unscaledTime;
            while (_clips.Count > MaxClips)
            {
                long oldest = 0; float best = float.MaxValue; bool found = false;
                foreach (var kv in _clips)
                    if (now - kv.Value.LastUse > ProtectSeconds && kv.Value.LastUse < best) { best = kv.Value.LastUse; oldest = kv.Key; found = true; }
                if (!found) return;
                var e = _clips[oldest];
                _clips.Remove(oldest);
                SampleCount -= e.Samples;
                if (e.Clip != null) Object.Destroy(e.Clip);
                Evictions++;
            }
        }

        public void Dispose()
        {
            _scratch.Clear();
            foreach (var kv in _clips) if (kv.Value.Clip != null) Object.Destroy(kv.Value.Clip);
            _clips.Clear();
            _pending.Clear();
            SampleCount = 0;
        }

        /// <summary>Creates the task on the first MoveNext (cheap), then forwards to it.</summary>
        sealed class LazyVoiceSteps : System.Collections.IEnumerator
        {
            readonly System.Func<VoiceRenderTask> _make;
            VoiceRenderTask _t;
            public LazyVoiceSteps(System.Func<VoiceRenderTask> make) { _make = make; }
            public object Current => null;
            public bool MoveNext()
            {
                if (_t == null) { _t = _make(); return _t != null; }
                return _t.MoveNext();
            }
            public void Reset() { }
        }
    }
}
