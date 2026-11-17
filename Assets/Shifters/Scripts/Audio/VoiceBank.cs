// =====================================================================================
//  SHIFTERS - VoiceBank.cs   [owned by the AUDIO agent]
//  Cache of gibberish voice clips keyed by (archetype, line, pitch bucket, variant) and of
//  speech-bubble chatter keyed by (archetype, pitch bucket, syllables, ending, variant).
//  Pitch is bucketed to 0.05 steps so the cache stays small. Prewarm(profile) renders the
//  first variation of every line for a character on a worker thread (time-sliced, one line
//  per slice, without threads). A random variation that is not rendered yet is replaced by
//  a ready variation of the same line while it renders in the background; only a line with
//  nothing ready at all renders on the spot (short gibberish, ~1-2 ms on desktop).
// =====================================================================================
using System.Collections.Generic;
using UnityEngine;

namespace Shifters
{
    public sealed class VoiceBank
    {
        public const int LineVariants = 3;
        public const int ChatterVariants = 3;
        const VoiceLine ChatterMarker = (VoiceLine)99;

        readonly ClipFactory _factory;
        readonly Dictionary<long, AudioClip> _clips = new Dictionary<long, AudioClip>(256);
        readonly HashSet<long> _pending = new HashSet<long>();
        readonly Dictionary<long, int> _lastVariant = new Dictionary<long, int>(64);
        readonly List<ClipFactory.Job> _jobScratch = new List<ClipFactory.Job>(16);
        AudioRng _rng = new AudioRng(4711);

        public int ClipCount { get; private set; }
        public long SampleCount { get; private set; }
        public int SyncRenders { get; private set; }
        /// <summary>A missing random variation was replaced by a ready one (and queued for background rendering).</summary>
        public int VariantFallbacks { get; private set; }
        public float MemoryMB => SampleCount * 4f / (1024f * 1024f);

        public VoiceBank(ClipFactory factory) { _factory = factory; }

        public static int PitchBucket(float pitch) => Mathf.Clamp(Mathf.RoundToInt((pitch <= 0f ? 1f : pitch) / 0.05f), 8, 40);
        public static float BucketPitch(int bucket) => bucket * 0.05f;

        static long Key(Archetype a, VoiceLine line, int bucket, int variant, int syl = 0, int ending = 0) =>
            ((long)(int)a << 48) | ((long)((int)line & 0xFF) << 40) | ((long)(syl & 0xFF) << 32) | ((long)(bucket & 0xFF) << 16) | ((long)(ending & 0xF) << 8) | (long)(variant & 0xFF);

        /// <summary>Clip for a voice line (variant -1 = random, avoids immediate repeats).</summary>
        public AudioClip GetLine(CharacterProfile p, VoiceLine line, int variant = -1)
        {
            if (p == null) return null;
            int bucket = PitchBucket(p.VoicePitch);
            bool random = variant < 0 || variant >= LineVariants;
            long lk = Key(p.Archetype, line, bucket, 0xFF);
            if (random)
            {
                _lastVariant.TryGetValue(lk, out int last);
                variant = _rng.Range(0, LineVariants);
                if (variant == last - 1) variant = (variant + 1) % LineVariants;
                _lastVariant[lk] = variant + 1;
            }
            long key = Key(p.Archetype, line, bucket, variant);
            if (_clips.TryGetValue(key, out var c) && c != null) return c;
            if (random)
                for (int k = 1; k < LineVariants; k++)
                {
                    int w = (variant + k) % LineVariants;
                    if (!_clips.TryGetValue(Key(p.Archetype, line, bucket, w), out var other) || other == null) continue;
                    QueueLine(p.Archetype, line, bucket, variant);   // ready next time
                    _lastVariant[lk] = w + 1;
                    VariantFallbacks++;
                    return other;
                }
            c = ClipFactory.CreateNow("voice_" + p.Archetype + "_" + line + "_" + bucket + "_" + variant,
                                      VoiceSynth.RenderLine(p.Archetype, BucketPitch(bucket), line, variant), 1);
            Put(key, c);
            SyncRenders++;
            return c;
        }

        /// <summary>Speech-bubble gibberish for text (same text => same gibberish).</summary>
        public AudioClip GetChatter(CharacterProfile p, string text)
        {
            if (p == null) return null;
            int bucket = PitchBucket(p.VoicePitch);
            int syl = VoiceSynth.SyllablesForText(text);
            int ending = VoiceSynth.EndingForText(text);
            int h = 17;
            if (text != null) for (int i = 0; i < text.Length; i++) h = h * 31 + text[i];
            int variant = (h & 0x7FFFFFFF) % ChatterVariants;
            long key = Key(p.Archetype, ChatterMarker, bucket, variant, syl, ending);
            if (_clips.TryGetValue(key, out var c) && c != null) return c;
            c = ClipFactory.CreateNow("chatter_" + p.Archetype + "_" + syl + "_" + variant,
                                      VoiceSynth.RenderChatter(p.Archetype, BucketPitch(bucket), syl, variant, ending), 1);
            Put(key, c);
            SyncRenders++;
            return c;
        }

        /// <summary>Background-renders one variation of a line (no-op when cached or already queued).</summary>
        void QueueLine(Archetype a, VoiceLine line, int bucket, int variant)
        {
            long key = Key(a, line, bucket, variant);
            if (_factory == null || _clips.ContainsKey(key) || _pending.Contains(key)) return;
            _pending.Add(key);
            _factory.Submit(new ClipFactory.Job
            {
                Name = "voice_" + a + "_" + line + "_" + bucket + "_" + variant,
                Render = () => VoiceSynth.RenderLine(a, BucketPitch(bucket), line, variant),
                OnReady = clip =>
                {
                    _pending.Remove(key);
                    if (clip == null) return;
                    if (_clips.ContainsKey(key)) { Object.Destroy(clip); return; }
                    Put(key, clip);
                }
            });
        }

        /// <summary>Background-renders variation 0 of every line for this character.</summary>
        public void Prewarm(CharacterProfile p)
        {
            if (p == null || _factory == null) return;
            int bucket = PitchBucket(p.VoicePitch);
            var a = p.Archetype;
            _jobScratch.Clear();
            foreach (VoiceLine line in System.Enum.GetValues(typeof(VoiceLine)))
            {
                long key = Key(a, line, bucket, 0);
                if (_clips.ContainsKey(key) || _pending.Contains(key)) continue;
                _pending.Add(key);
                var l = line;
                _jobScratch.Add(new ClipFactory.Job
                {
                    Name = "voice_" + a + "_" + l + "_" + bucket + "_0",
                    Render = () => VoiceSynth.RenderLine(a, BucketPitch(bucket), l, 0),
                    OnReady = clip =>
                    {
                        _pending.Remove(key);
                        if (clip == null) return;
                        if (_clips.ContainsKey(key)) { Object.Destroy(clip); return; }
                        Put(key, clip);
                    }
                });
            }
            if (_jobScratch.Count > 0) _factory.SubmitBatch(_jobScratch.ToArray());
        }

        void Put(long key, AudioClip c)
        {
            if (c == null) return;
            _clips[key] = c;
            ClipCount++;
            SampleCount += (long)c.samples * c.channels;
        }

        public void Dispose()
        {
            foreach (var kv in _clips) if (kv.Value != null) Object.Destroy(kv.Value);
            _clips.Clear();
        }
    }
}
