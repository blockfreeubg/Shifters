// =====================================================================================
//  SHIFTERS - SfxBank.cs   [owned by the AUDIO agent]
//  Cache of every SfxId x variation (+ special stingers). Pre-warmed at boot through the
//  ClipFactory: on a worker thread on desktop, time-sliced (SynthPlanTask, < 1 ms slices)
//  without threads (WebGL). Get NEVER renders on the calling thread: a clip that is not
//  ready yet is promoted to the front of the render queue and Get returns another ready
//  variation, or null - AudioManager then plays the sound once it is ready (or skips it).
// =====================================================================================
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Shifters
{
    public sealed class SfxBank
    {
        readonly ClipFactory _factory;
        readonly AudioClip[][] _clips;
        readonly ClipFactory.Job[][] _jobs;
        readonly int[] _last;
        readonly AudioClip[] _special;
        readonly ClipFactory.Job[] _specialJobs;
        AudioRng _rng = new AudioRng(20261113);
        int _prewarmRemaining;

        public int ClipCount { get; private set; }
        public long SampleCount { get; private set; }
        /// <summary>On-demand requests: a clip was wanted before the pre-warm had rendered it. It is moved to the
        /// front of the render queue (never rendered synchronously on the calling thread).</summary>
        public int SyncRenders { get; private set; }
        /// <summary>Main-thread milliseconds spent on those requests (queue bookkeeping only).</summary>
        public double SyncRenderMs { get; private set; }
        public bool PrewarmStarted { get; private set; }
        public bool PrewarmDone => PrewarmStarted && _prewarmRemaining <= 0;
        public int PrewarmTotal { get; private set; }
        public float PrewarmStartTime { get; private set; }
        public float PrewarmEndTime { get; private set; } = -1f;
        /// <summary>Approximate PCM memory held by cached clips (float32).</summary>
        public float MemoryMB => SampleCount * 4f / (1024f * 1024f);

        public SfxBank(ClipFactory factory)
        {
            _factory = factory;
            int n = SfxTable.Count;
            _clips = new AudioClip[n][];
            _jobs = new ClipFactory.Job[n][];
            _last = new int[n];
            for (int i = 0; i < n; i++)
            {
                int variants = Math.Max(1, SfxTable.Get((SfxId)i).Variants);
                _clips[i] = new AudioClip[variants];
                _jobs[i] = new ClipFactory.Job[variants];
                _last[i] = -1;
            }
            int specials = Enum.GetValues(typeof(SfxSpecial)).Length;
            _special = new AudioClip[specials];
            _specialJobs = new ClipFactory.Job[specials];
        }

        /// <summary>Queues background rendering of every variation (idempotent; nothing is rendered in this call).</summary>
        public void StartPrewarm()
        {
            if (PrewarmStarted || _factory == null) return;
            PrewarmStarted = true;
            PrewarmStartTime = Time.realtimeSinceStartup;
            var jobs = new List<ClipFactory.Job>(256);
            for (int i = 1; i < _clips.Length; i++)
                for (int v = 0; v < _clips[i].Length; v++)
                {
                    if (_clips[i][v] != null || _jobs[i][v] != null) continue;   // already rendered / requested
                    if (SfxTable.IsLazy((SfxId)i)) continue;                      // Heist/Scary: PrewarmGroup on demand
                    jobs.Add(_jobs[i][v] = MakeJob(i, v, true));
                }
            for (int s = 0; s < _special.Length; s++)
            {
                if (_special[s] != null || _specialJobs[s] != null) continue;
                jobs.Add(_specialJobs[s] = MakeSpecialJob(s, true));
            }
            PrewarmTotal = jobs.Count;
            _prewarmRemaining = jobs.Count;
            if (jobs.Count == 0) PrewarmEndTime = PrewarmStartTime;
            _factory.SubmitBatch(jobs.ToArray());
        }

        ClipFactory.Job MakeJob(int id, int variant, bool prewarm)
        {
            var task = new SynthPlanTask(() => SfxRecipes.Plan((SfxId)id, variant));
            return new ClipFactory.Job
            {
                Name = "sfx_" + (SfxId)id + "_" + variant,
                Steps = task,                                   // time-sliced without threads
                Render = () => SfxTable.Get((SfxId)id).Loopable ? task.Result : TrimTail(task.Result),
                Obsolete = () => _clips[id][variant] != null,   // already there
                OnReady = clip => Store(id, variant, clip, prewarm)
            };
        }

        ClipFactory.Job MakeSpecialJob(int s, bool prewarm)
        {
            var task = new SynthPlanTask(() => SfxRecipes.PlanSpecial((SfxSpecial)s));
            return new ClipFactory.Job
            {
                Name = "sfx_special_" + (SfxSpecial)s,
                Steps = task,
                Render = () => TrimTail(task.Result),
                Obsolete = () => _special[s] != null,
                OnReady = clip => StoreSpecial(s, clip, prewarm)
            };
        }

        /// <summary>Tail-trim threshold (-60 dBFS): recipe buffers are sized generously; the inaudible tail is cut before
        /// upload (PCM budget, CAMPAIGN 21). Loopable SFX are never trimmed.</summary>
        public const float TrimThreshold = 0.001f;
        const int TrimMargin = 512, TrimFade = 256, TrimMinSave = 2048;
        static long _trimmedSamples;
        /// <summary>Samples saved by tail trimming since start (all banks).</summary>
        public static long TrimmedSamples => System.Threading.Interlocked.Read(ref _trimmedSamples);

        /// <summary>Cuts the inaudible tail (after the last sample above TrimThreshold + a 12 ms margin, 6 ms fade-out).</summary>
        public static float[] TrimTail(float[] b)
        {
            if (b == null || b.Length < TrimMinSave * 2) return b;
            int last = b.Length - 1;
            while (last > 0 && Math.Abs(b[last]) < TrimThreshold) last--;
            int len = Math.Min(b.Length, last + 1 + TrimMargin);
            if (b.Length - len < TrimMinSave) return b;
            var o = new float[len];
            Array.Copy(b, o, len);
            int f0 = Math.Max(0, len - TrimFade);
            for (int i = f0; i < len; i++) o[i] *= (len - i) / (float)(len - f0);
            System.Threading.Interlocked.Add(ref _trimmedSamples, b.Length - len);
            return o;
        }

        void Done()
        {
            _prewarmRemaining--;
            if (_prewarmRemaining <= 0 && PrewarmEndTime < 0f) PrewarmEndTime = Time.realtimeSinceStartup;
        }

        void Store(int id, int v, AudioClip clip, bool prewarm)
        {
            if (prewarm) Done();
            if (clip == null) return;
            if (_clips[id][v] != null) { UnityEngine.Object.Destroy(clip); return; }
            _clips[id][v] = clip;
            Count(clip);
        }

        void StoreSpecial(int s, AudioClip clip, bool prewarm)
        {
            if (prewarm) Done();
            if (clip == null) return;
            if (_special[s] != null) { UnityEngine.Object.Destroy(clip); return; }
            _special[s] = clip;
            Count(clip);
        }

        void Count(AudioClip c) { ClipCount++; SampleCount += (long)c.samples * c.channels; }

        public int VariantCount(SfxId id) { int i = (int)id; return i > 0 && i < _clips.Length ? _clips[i].Length : 0; }

        /// <summary>True when at least one variation of id is ready to play.</summary>
        public bool HasReady(SfxId id)
        {
            int i = (int)id;
            if (i <= 0 || i >= _clips.Length) return false;
            foreach (var c in _clips[i]) if (c != null) return true;
            return false;
        }

        public bool IsReady(SfxId id, int variant)
        {
            int i = (int)id;
            return i > 0 && i < _clips.Length && variant >= 0 && variant < _clips[i].Length && _clips[i][variant] != null;
        }

        public bool IsSpecialReady(SfxSpecial s) { int i = (int)s; return i >= 0 && i < _special.Length && _special[i] != null; }

        /// <summary>Clip for id (variant -1 = random, never the same variation twice in a row). Never blocks: when the
        /// variation is not rendered yet it is requested (rendered next) and another ready variation is returned -
        /// or null when none is ready yet (play it later or skip it).</summary>
        public AudioClip Get(SfxId id, int variant = -1)
        {
            int i = (int)id;
            if (i <= 0 || i >= _clips.Length) return null;
            var arr = _clips[i];
            int n = arr.Length, v = variant;
            if (v < 0 || v >= n)
            {
                v = n == 1 ? 0 : _rng.Range(0, n);
                if (n > 1 && v == _last[i]) v = (v + 1) % n;
            }
            var c = arr[v];
            if (c == null)
            {
                Request(i, v);
                if (variant >= 0 && variant < n) return null;   // that exact variation was asked for
                for (int k = 1; k < n && c == null; k++)
                {
                    int w = (v + k) % n;
                    if (arr[w] != null) { c = arr[w]; v = w; }
                }
                if (c == null) return null;
            }
            _last[i] = v;
            return c;
        }

        /// <summary>Special stinger clip; null (and requested with top priority) while it is not rendered yet.</summary>
        public AudioClip GetSpecial(SfxSpecial s)
        {
            int i = (int)s;
            if (i < 0 || i >= _special.Length) return null;
            if (_special[i] != null) return _special[i];
            if (_factory == null) return null;
            var t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            var job = _specialJobs[i];
            if (job == null) _factory.SubmitUrgent(_specialJobs[i] = MakeSpecialJob(i, false));
            else if (!job.Urgent) _factory.Promote(job);
            else return null;
            SyncRenders++;
            SyncRenderMs += (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            return null;
        }

        /// <summary>Asks for (id, v) to be rendered next (pre-warm job promoted, or a new urgent job).</summary>
        void Request(int i, int v)
        {
            if (_factory == null) return;
            var job = _jobs[i][v];
            if (job != null && job.Urgent) return;   // already on its way
            var t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            if (job == null) _factory.SubmitUrgent(_jobs[i][v] = MakeJob(i, v, false));
            else _factory.Promote(job);
            SyncRenders++;
            SyncRenderMs += (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        }

        // ------------------------------------------------------------------ campaign groups (lazy pre-warm / release)

        /// <summary>Queues background rendering of every missing variation of a group (no-op for ready/queued ones).
        /// Returns the number of jobs queued.</summary>
        public int PrewarmGroup(SfxGroup g)
        {
            if (_factory == null) return 0;
            var jobs = new List<ClipFactory.Job>(16);
            for (int i = 1; i < _clips.Length; i++)
            {
                if (SfxTable.Get((SfxId)i).Group != g) continue;
                for (int v = 0; v < _clips[i].Length; v++)
                {
                    if (_clips[i][v] != null || _jobs[i][v] != null) continue;
                    jobs.Add(_jobs[i][v] = MakeJob(i, v, false));
                }
            }
            if (jobs.Count > 0) _factory.SubmitBatch(jobs.ToArray());
            return jobs.Count;
        }

        /// <summary>True when every variation of the group is rendered.</summary>
        public bool GroupReady(SfxGroup g)
        {
            for (int i = 1; i < _clips.Length; i++)
            {
                if (SfxTable.Get((SfxId)i).Group != g) continue;
                foreach (var c in _clips[i]) if (c == null) return false;
            }
            return true;
        }

        /// <summary>Destroys the rendered clips of a group (PCM budget). Jobs still in flight finish normally.
        /// Do not call while those sounds play. Returns the number of clips released.</summary>
        public int ReleaseGroup(SfxGroup g)
        {
            int n = 0;
            for (int i = 1; i < _clips.Length; i++)
            {
                if (SfxTable.Get((SfxId)i).Group != g) continue;
                for (int v = 0; v < _clips[i].Length; v++)
                {
                    var c = _clips[i][v];
                    if (c == null) continue;
                    ClipCount--;
                    SampleCount -= (long)c.samples * c.channels;
                    UnityEngine.Object.Destroy(c);
                    _clips[i][v] = null;
                    _jobs[i][v] = null;
                    n++;
                }
            }
            return n;
        }

        /// <summary>PCM memory (MB) held by one group.</summary>
        public float GroupMemoryMB(SfxGroup g)
        {
            long smp = 0;
            for (int i = 1; i < _clips.Length; i++)
            {
                if (SfxTable.Get((SfxId)i).Group != g) continue;
                foreach (var c in _clips[i]) if (c != null) smp += (long)c.samples * c.channels;
            }
            return smp * 4f / (1024f * 1024f);
        }

        public void Dispose()
        {
            foreach (var arr in _clips)
                for (int v = 0; v < arr.Length; v++)
                    if (arr[v] != null) { UnityEngine.Object.Destroy(arr[v]); arr[v] = null; }
            for (int s = 0; s < _special.Length; s++)
                if (_special[s] != null) { UnityEngine.Object.Destroy(_special[s]); _special[s] = null; }
        }
    }
}
