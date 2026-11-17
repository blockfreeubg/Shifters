// =====================================================================================
//  SHIFTERS - ClipFactory.cs   [owned by the AUDIO agent]
//  Renders sample data on worker threads (System.Threading.Tasks) and turns finished
//  buffers into AudioClips on the main thread under a per-frame time budget (large clips
//  are uploaded in chunks via SetData offsets).
//  WITHOUT threads (WebGL, single core, or ForceSynchronous) nothing is rendered inside
//  Submit: jobs are queued and rendered time-sliced in Pump (SyncRenderBudgetMs per frame).
//  Incremental jobs (Steps: music via MusicSynth.RenderSteps, SFX via SynthPlanTask) do a
//  small slice per MoveNext, so no single slice blocks the frame. Three urgency levels:
//  Urgent (Promote: a clip somebody is waiting to hear) > Priority (the music about to play)
//  > normal (pre-warm). A more urgent job parks the job being sliced and goes first; the
//  parked one resumes later where it stopped. Urgent results are also uploaded first.
// =====================================================================================
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Shifters
{
    public sealed class ClipFactory
    {
        public sealed class Job
        {
            public string Name;
            public int Channels = 1;
            /// <summary>Returns the rendered (interleaved) samples. With Steps set it is called after Steps finished.</summary>
            public Func<float[]> Render;
            /// <summary>Optional incremental renderer: every MoveNext does a small slice of the work.</summary>
            public IEnumerator Steps;
            /// <summary>Optional: the job is skipped (OnReady(null)) when this returns true before rendering starts.</summary>
            public Func<bool> Obsolete;
            /// <summary>Rendered before normal jobs in synchronous mode (e.g. the music that is about to play).</summary>
            public bool Priority;
            /// <summary>Main thread. clip is null when rendering failed or the job was skipped.</summary>
            public Action<AudioClip> OnReady;
            internal float[] Data;
            internal Exception Error;
            internal AudioClip Clip;
            internal int Uploaded;
            internal double RenderMs;
            internal int State;               // Queued / Running / Parked / Rendered / Finished
            internal volatile bool UrgentFlag;

            /// <summary>Somebody waits for this clip (set by Promote / SubmitUrgent): rendered and uploaded first.</summary>
            public bool Urgent => UrgentFlag;
            /// <summary>OnReady has been called (clip delivered, failed or skipped).</summary>
            public bool Finished => Volatile.Read(ref State) == Done;
            /// <summary>Total render time of this job so far (ms).</summary>
            public double RenderMilliseconds => RenderMs;
            internal int Level => UrgentFlag ? 2 : Priority ? 1 : 0;
        }

        const int Queued = 0, Running = 1, Parked = 2, Rendered = 3, Done = 4;
        const int ChunkFrames = 32768;
        /// <summary>WebGL: one SetData per clip (no offset uploads).</summary>
        static bool SingleUpload => Application.platform == RuntimePlatform.WebGLPlayer;

        /// <summary>Tests: makes new factories behave like WebGL (no threads, time-sliced rendering).</summary>
        public static bool ForceSynchronous;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            ForceSynchronous = false;
            SynthPlan.ResetRecording();
        }

        readonly ConcurrentQueue<Job> _done = new ConcurrentQueue<Job>(), _doneUrgent = new ConcurrentQueue<Job>();
        readonly LinkedList<Job>[] _queues = { new LinkedList<Job>(), new LinkedList<Job>(), new LinkedList<Job>() };   // by Level
        readonly bool _threads;
        Job _uploading, _stepping;
        int _inFlight, _promotions;
        volatile bool _disposed;
        float[] _chunk;

        /// <summary>Synchronous mode: milliseconds of rendering per Pump (frame).</summary>
        public double SyncRenderBudgetMs = 5.0;
        /// <summary>Synchronous mode: slice cap per Pump - keeps the budget even where the browser clock is coarse.</summary>
        public int MaxSlicesPerPump = 48;
        public bool ThreadsAvailable => _threads;
        /// <summary>Jobs rendering, waiting for upload or uploading.</summary>
        public int Pending => Volatile.Read(ref _inFlight) + _done.Count + _doneUrgent.Count + (_uploading != null ? 1 : 0);
        public double TotalRenderMs { get; private set; }
        public double TotalUploadMs { get; private set; }
        /// <summary>Longest upload pump (one frame) during which no garbage collection ran (a GC pause is not upload
        /// work: it is counted in MaxGcPumpMs / GcPumps instead).</summary>
        public double MaxPumpMs { get; private set; }
        /// <summary>Synchronous mode: longest single render slice (one MoveNext of an incremental job, or one atomic job)
        /// during which no garbage collection ran; slices hit by a GC pause go to MaxGcSliceMs / GcSlices.</summary>
        public double MaxSyncSliceMs { get; private set; }
        /// <summary>Job name of the MaxSyncSliceMs slice (diagnostics).</summary>
        public string MaxSyncSliceName { get; private set; }
        /// <summary>Synchronous mode: non-GC slices longer than SliceWarnMs (8 ms, CAMPAIGN 21).</summary>
        public int SlicesOverBudget { get; private set; }
        public const double SliceWarnMs = 8.0;
        /// <summary>Distinct job names that had a non-GC slice over SliceWarnMs (diagnostics, at most 16).</summary>
        public readonly List<string> OverBudgetJobs = new List<string>();
        /// <summary>Longest render slice that included a GC pause (any thread's allocation can stop the world).</summary>
        public double MaxGcSliceMs { get; private set; }
        public int GcSlices { get; private set; }
        public double MaxGcPumpMs { get; private set; }
        public int GcPumps { get; private set; }
        /// <summary>Synchronous mode: longest total render time inside one Pump (all slices of that frame).</summary>
        public double MaxSyncFrameMs { get; private set; }
        public int SyncSlices { get; private set; }
        public long SamplesUploaded { get; private set; }
        public int ClipsCreated { get; private set; }
        public int Skipped { get; private set; }
        /// <summary>Jobs moved to the front because somebody was waiting for them.</summary>
        public int Promotions => Volatile.Read(ref _promotions);
        /// <summary>Synchronous mode: slicing of a less urgent job was interrupted for a more urgent one.</summary>
        public int Parks { get; private set; }

        public ClipFactory() : this(!ForceSynchronous && Application.platform != RuntimePlatform.WebGLPlayer && SystemInfo.processorCount > 1) { }

        public ClipFactory(bool useThreads) { _threads = useThreads; }

        public void Submit(Job job) => SubmitBatch(new[] { job });

        /// <summary>Submits a job somebody is already waiting for (rendered/uploaded before everything else).</summary>
        public void SubmitUrgent(Job job)
        {
            if (job == null) return;
            job.UrgentFlag = true;
            Interlocked.Increment(ref _promotions);
            SubmitBatch(new[] { job });
        }

        /// <summary>Threads: renders the jobs one after another on a single worker (keeps other cores free).
        /// No threads: queues them for time-sliced rendering in Pump (nothing heavy happens here).</summary>
        public void SubmitBatch(Job[] jobs)
        {
            if (jobs == null || jobs.Length == 0 || _disposed) return;
            Interlocked.Add(ref _inFlight, jobs.Length);
            if (!_threads)
            {
                foreach (var j in jobs) _queues[j.Level].AddLast(j);
                return;
            }
            try
            {
                Task.Run(() =>
                {
                    foreach (var j in jobs)
                    {
                        if (!Claim(j)) continue;                                   // promoted onto its own worker already
                        if (_disposed) { Interlocked.Decrement(ref _inFlight); continue; }
                        RunOne(j);
                    }
                });
            }
            catch (Exception)
            {
                foreach (var j in jobs) _queues[j.Level].AddLast(j);   // will be pumped
            }
        }

        /// <summary>Somebody is waiting for this job's clip: render it next (threads: on its own worker right away;
        /// no threads: ahead of everything, parking the job being sliced) and upload it first. Idempotent, cheap.</summary>
        public void Promote(Job j)
        {
            if (j == null || _disposed || j.UrgentFlag) return;
            j.UrgentFlag = true;
            Interlocked.Increment(ref _promotions);
            int s = Volatile.Read(ref j.State);
            if (s != Queued && s != Parked) return;                                // rendering or rendered already
            if (_threads && s == Queued && Claim(j))
            {
                try { Task.Run(() => RunOne(j)); return; }
                catch (Exception) { Volatile.Write(ref j.State, Queued); }        // no worker: pump it below
            }
            int now = Volatile.Read(ref j.State);
            if (now == Queued || now == Parked) _queues[2].AddLast(j);   // its old place in line is skipped later
        }

        static bool Claim(Job j) => Interlocked.CompareExchange(ref j.State, Running, Queued) == Queued;

        void RunOne(Job j)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                if (j.Obsolete != null && j.Obsolete()) j.Data = null;
                else
                {
                    if (j.Steps != null) while (j.Steps.MoveNext()) { }
                    j.Data = j.Render != null ? j.Render() : null;
                }
            }
            catch (Exception e) { j.Error = e; }
            j.RenderMs += sw.Elapsed.TotalMilliseconds;
            Finish(j);
        }

        void Finish(Job j)
        {
            Volatile.Write(ref j.State, Rendered);
            (j.UrgentFlag ? _doneUrgent : _done).Enqueue(j);
            Interlocked.Decrement(ref _inFlight);
        }

        /// <summary>Main thread, once per frame: time-sliced rendering (synchronous mode) then clip creation/upload for at most budgetMs.</summary>
        public void Pump(double budgetMs)
        {
            if (_disposed) return;
            if (!_threads || _stepping != null || AnyQueued()) RenderSlice();
            if (_uploading == null && _done.IsEmpty && _doneUrgent.IsEmpty) return;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            int gc0 = GC.CollectionCount(0);
            while (sw.Elapsed.TotalMilliseconds < budgetMs)
            {
                if (_uploading == null)
                {
                    if (!_doneUrgent.TryDequeue(out var j) && !_done.TryDequeue(out j)) break;
                    TotalRenderMs += j.RenderMs;
                    if (j.Error != null || j.Data == null || j.Data.Length < j.Channels)
                    {
                        if (j.Error != null) Debug.LogWarning("[Audio] render failed for " + j.Name + ": " + j.Error.Message);
                        else Skipped++;
                        Deliver(j, null);
                        continue;
                    }
                    j.Clip = AudioClip.Create(j.Name, j.Data.Length / j.Channels, j.Channels, Synth.SampleRate, false);
                    j.Uploaded = 0;
                    _uploading = j;
                }
                if (UploadStep(_uploading))
                {
                    var j = _uploading;
                    _uploading = null;
                    ClipsCreated++;
                    SamplesUploaded += j.Data.Length;
                    Deliver(j, j.Clip);
                }
            }
            double ms = sw.Elapsed.TotalMilliseconds;
            TotalUploadMs += ms;
            if (GC.CollectionCount(0) != gc0) { GcPumps++; if (ms > MaxGcPumpMs) MaxGcPumpMs = ms; }
            else if (ms > MaxPumpMs) MaxPumpMs = ms;
        }

        bool AnyQueued() => _queues[0].Count > 0 || _queues[1].Count > 0 || _queues[2].Count > 0;

        static bool Takeable(Job j) { int s = j.State; return s == Queued || s == Parked; }

        /// <summary>Next job to render: most urgent level first (parked jobs sit at the front of their level).
        /// Stale entries (a promoted job's old place in line) are dropped on the way.</summary>
        Job NextQueued()
        {
            for (int lv = 2; lv >= 0; lv--)
            {
                var q = _queues[lv];
                while (q.Count > 0)
                {
                    var j = q.First.Value;
                    q.RemoveFirst();
                    if (Interlocked.CompareExchange(ref j.State, Running, Queued) == Queued ||
                        Interlocked.CompareExchange(ref j.State, Running, Parked) == Parked) return j;
                }
            }
            return null;
        }

        bool MoreUrgentQueued(int level)
        {
            for (int lv = 2; lv > level; lv--) if (TakeableAt(lv)) return true;
            return false;
        }

        bool TakeableAt(int level)
        {
            var q = _queues[level];
            while (q.Count > 0 && !Takeable(q.First.Value)) q.RemoveFirst();
            return q.Count > 0;
        }

        /// <summary>Stops slicing the current job; it resumes where it stopped (front of its level, or back = round-robin).</summary>
        void Park(bool front)
        {
            var j = _stepping;
            _stepping = null;
            j.State = Parked;
            if (front) _queues[j.Level].AddFirst(j);
            else _queues[j.Level].AddLast(j);
            Parks++;
        }

        /// <summary>Synchronous mode: renders queued jobs for SyncRenderBudgetMs (most urgent first).</summary>
        void RenderSlice()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            int slices = 0;
            while (sw.Elapsed.TotalMilliseconds < SyncRenderBudgetMs && slices++ < MaxSlicesPerPump)
            {
                double t0 = sw.Elapsed.TotalMilliseconds;
                _sliceGc = GC.CollectionCount(0);
                if (_stepping != null)
                {
                    int lv = _stepping.Level;
                    if (MoreUrgentQueued(lv)) Park(true);                    // a sound somebody wants NOW overtakes pre-warm / music
                    else if (lv == 2 && TakeableAt(2)) Park(false);           // several awaited clips: round-robin, short ones finish first
                }
                if (_stepping == null)
                {
                    var j = NextQueued();
                    if (j == null) break;
                    if (j.Steps == null || (j.Obsolete != null && j.Obsolete()))
                    {
                        RunOne(j);   // atomic job (voice line) or a skip - never runs a sliced job to the end
                        NoteSlice(sw.Elapsed.TotalMilliseconds - t0, j.Name);
                        continue;
                    }
                    _stepping = j;
                }
                var s = _stepping;
                bool finished;
                try
                {
                    finished = !s.Steps.MoveNext();
                    if (finished) s.Data = s.Render != null ? s.Render() : null;
                }
                catch (Exception e) { s.Error = e; finished = true; }
                double slice = sw.Elapsed.TotalMilliseconds - t0;
                s.RenderMs += slice;
                NoteSlice(slice, s.Name);
                if (finished)
                {
                    _stepping = null;
                    Finish(s);
                }
            }
            double frame = sw.Elapsed.TotalMilliseconds;
            if (frame > MaxSyncFrameMs) MaxSyncFrameMs = frame;
        }

        int _sliceGc;

        void NoteSlice(double ms, string name)
        {
            SyncSlices++;
            if (GC.CollectionCount(0) != _sliceGc) { GcSlices++; if (ms > MaxGcSliceMs) MaxGcSliceMs = ms; return; }
            if (ms > SliceWarnMs) { SlicesOverBudget++; if (OverBudgetJobs.Count < 16 && name != null && !OverBudgetJobs.Contains(name)) OverBudgetJobs.Add(name); }
            if (ms > MaxSyncSliceMs) { MaxSyncSliceMs = ms; MaxSyncSliceName = name; }
        }

        bool UploadStep(Job j)
        {
            int ch = j.Channels;
            int total = j.Data.Length / ch;
            if (j.Uploaded == 0 && (total <= ChunkFrames || SingleUpload))
            {
                j.Clip.SetData(j.Data, 0);
                j.Uploaded = total;
                return true;
            }
            int frames = Math.Min(ChunkFrames, total - j.Uploaded);
            float[] buf;
            if (frames == ChunkFrames)
            {
                if (_chunk == null || _chunk.Length != ChunkFrames * ch) _chunk = new float[ChunkFrames * ch];
                buf = _chunk;
            }
            else buf = new float[frames * ch];
            Array.Copy(j.Data, j.Uploaded * ch, buf, 0, frames * ch);
            j.Clip.SetData(buf, j.Uploaded);
            j.Uploaded += frames;
            return j.Uploaded >= total;
        }

        /// <summary>Hands the clip out and lets go of the renderer: owners may keep finished jobs (SfxBank does, for
        /// promotion), so the samples / steps / closures must not stay referenced (the clip holds the audio now).</summary>
        static void Deliver(Job j, AudioClip clip)
        {
            var onReady = j.OnReady;
            j.Data = null; j.Steps = null; j.Render = null; j.Obsolete = null; j.OnReady = null; j.Clip = null;
            Volatile.Write(ref j.State, Done);
            try { onReady?.Invoke(clip); }
            catch (Exception e) { Debug.LogException(e); }
        }

        /// <summary>Synchronous clip creation (main thread).</summary>
        public static AudioClip CreateNow(string name, float[] data, int channels)
        {
            if (data == null || data.Length < channels) return null;
            var clip = AudioClip.Create(name, data.Length / channels, channels, Synth.SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        public void Dispose()
        {
            _disposed = true;
            if (_uploading != null && _uploading.Clip != null) UnityEngine.Object.Destroy(_uploading.Clip);
            _uploading = null;
            _stepping = null;
            foreach (var q in _queues) q.Clear();
            while (_done.TryDequeue(out _)) { }
            while (_doneUrgent.TryDequeue(out _)) { }
        }
    }
}
