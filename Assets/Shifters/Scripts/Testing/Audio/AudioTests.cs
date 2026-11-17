// =====================================================================================
//  SHIFTERS - AudioTests.cs   [owned by the AUDIO agent]
//  DevPlay scenarios:
//    Shifters.AudioTests.All       = Generate + Runtime (one Unity run)
//    Shifters.AudioTests.Generate  every SfxId x variation, every VoiceLine (3 archetypes x 3 pitches),
//                                  chatter, every MusicId: bounds, peak, RMS, NaN, clicks, loop seams,
//                                  pitch contours; WAV previews + spectrogram PNGs -> $SP/audio_preview/
//    Shifters.AudioTests.Runtime   bootstrap AudioManager: prewarm budget, real team (footsteps, jump,
//                                  speech chatter, KO/revive/lost, switching), spam collapse, voice bounds,
//                                  stealing, loops, music + crossfade, slow-mo muffle, volumes/settings,
//                                  stingers, pause, listener fallback, allocations.
//  Real-time waits are used for audio timing (AudioManager runs on unscaled wall-clock time).
// =====================================================================================
#if UNITY_EDITOR   // DevPlay test code: editor only (uses System.IO), never shipped in player/WebGL builds
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Shifters
{
    public static class AudioTests
    {
        static DevPlayContext ctx;

        public static IEnumerator All(DevPlayContext c)
        {
            yield return Generate(c);
            yield return WebGLSim(c);
            yield return Runtime(c);
        }

        // ================================================================== GENERATION
        public static IEnumerator Generate(DevPlayContext c)
        {
            ctx = c;
            string dir = AudioTestTools.PreviewDir();
            ctx.Log("audio preview dir: " + dir);
            var plotsComedy = new List<AudioTestTools.PlotItem>();
            var plotsVoice = new List<AudioTestTools.PlotItem>();
            var plotsMusic = new List<AudioTestTools.PlotItem>();
            var comedy = new HashSet<SfxId> { SfxId.Jump, SfxId.Tumble, SfxId.Bonk, SfxId.KO, SfxId.Lost, SfxId.Squeak, SfxId.Chicken, SfxId.Bounce,
                                              SfxId.BridgeForm, SfxId.BridgeRecover, SfxId.Footstep, SfxId.Siren, SfxId.Plunger, SfxId.Explosion, SfxId.SwitcherOpen, SfxId.LevelComplete };

            // ---------------------------------------------------------- SFX
            var sw = System.Diagnostics.Stopwatch.StartNew();
            int clips = 0, bad = 0; long samples = 0; double worstMs = 0; string worstId = "";
            foreach (SfxId id in Enum.GetValues(typeof(SfxId)))
            {
                if (id == SfxId.None) continue;
                var info = SfxTable.Get(id);
                for (int v = 0; v < Math.Max(1, info.Variants); v++)
                {
                    var t0 = sw.Elapsed.TotalMilliseconds;
                    var b = SfxRecipes.Render(id, v);
                    double ms = sw.Elapsed.TotalMilliseconds - t0;
                    if (ms > worstMs) { worstMs = ms; worstId = id + "_" + v; }
                    clips++; samples += b != null ? b.Length : 0;
                    if (!CheckClip("sfx " + id + "#" + v, b, 0.02f, 6f, !info.Loopable)) bad++;
                    if (info.Loopable && b != null)
                    {
                        AudioTestTools.Jumps(b, 1, 0, out float interior, out float seam);
                        if (!ctx.Check(seam <= Math.Max(0.02f, interior * 1.05f), $"loop {id} seam continuous (seam jump {seam:0.000} vs max interior {interior:0.000})")) bad++;
                    }
                    if (b != null) AudioTestTools.WriteWav(Path.Combine(dir, "sfx", id + "_" + v + ".wav"), b, 1);
                    if (v == 0 && comedy.Contains(id) && b != null) plotsComedy.Add(new AudioTestTools.PlotItem { Label = id.ToString(), Data = b, Channels = 1 });
                }
            }
            foreach (SfxSpecial s in Enum.GetValues(typeof(SfxSpecial)))
            {
                var b = SfxRecipes.RenderSpecial(s);
                clips++;
                if (!CheckClip("special " + s, b, 0.3f, 6f, true)) bad++;
                if (b != null) AudioTestTools.WriteWav(Path.Combine(dir, "sfx", "Special_" + s + ".wav"), b, 1);
            }
            ctx.Check(bad == 0, $"all {clips} SFX clips (every SfxId x variation + specials) pass bounds/peak/RMS/NaN/click checks");
            ctx.Log($"SFX generation: {clips} clips, {sw.Elapsed.TotalMilliseconds:0} ms total ({sw.Elapsed.TotalMilliseconds / Math.Max(1, clips):0.00} ms avg, worst {worstId} {worstMs:0.0} ms), {samples * 4 / 1048576f:0.0} MB float32");
            yield return null;

            // ---------------------------------------------------------- VOICES
            sw.Restart();
            int vclips = 0, vbad = 0;
            var arches = new[] { Archetype.Brute, Archetype.Sprinter, Archetype.Brainiac };
            var pitches = new Dictionary<Archetype, float[]>
            {
                { Archetype.Brute, new[] { 0.75f, 0.82f, 0.9f } },
                { Archetype.Sprinter, new[] { 1.15f, 1.22f, 1.3f } },
                { Archetype.Brainiac, new[] { 1.0f, 1.07f, 1.15f } },
            };
            var greetPitch = new Dictionary<Archetype, float>();
            foreach (var a in arches)
                foreach (VoiceLine line in Enum.GetValues(typeof(VoiceLine)))
                {
                    var ps = pitches[a];
                    for (int pi = 0; pi < ps.Length; pi++)
                        for (int v = 0; v < (pi == 1 ? VoiceBank.LineVariants : 1); v++)
                        {
                            float p = VoiceBank.BucketPitch(VoiceBank.PitchBucket(ps[pi]));
                            var b = VoiceSynth.RenderLine(a, p, line, v);
                            vclips++;
                            float maxLen = line == VoiceLine.Jump ? 0.6f : 3f;
                            float minLen = line == VoiceLine.Scream || line == VoiceLine.Lost ? 0.8f : line == VoiceLine.Thrown ? 0.5f : 0.08f;
                            if (!CheckClip($"voice {a} {line} p{p:0.00} v{v}", b, minLen, maxLen, true)) vbad++;
                            if (pi == 1 && v == 0 && b != null)
                            {
                                AudioTestTools.WriteWav(Path.Combine(dir, "voice", a + "_" + line + ".wav"), b, 1);
                                if (line == VoiceLine.Greeting) greetPitch[a] = AudioTestTools.MedianPitch(b);
                                if (line == VoiceLine.Greeting || (a == Archetype.Sprinter && (line == VoiceLine.Thrown || line == VoiceLine.Lost || line == VoiceLine.Scream || line == VoiceLine.Taunt)) || (a == Archetype.Brute && line == VoiceLine.Strain))
                                    plotsVoice.Add(new AudioTestTools.PlotItem { Label = a + " " + line, Data = b, Channels = 1 });
                                if (line == VoiceLine.Thrown)
                                {
                                    float p0 = AudioTestTools.PitchAtFraction(b, 0.15f), p1 = AudioTestTools.PitchAtFraction(b, 0.8f);
                                    if (!ctx.Check(p0 > 0f && p1 > p0 * 1.2f, $"{a} Thrown 'wheee' rises ({p0:0} Hz -> {p1:0} Hz)")) vbad++;
                                }
                                if (line == VoiceLine.Lost)
                                {
                                    float p0 = AudioTestTools.PitchAtFraction(b, 0.2f), p1 = AudioTestTools.PitchAtFraction(b, 0.85f);
                                    if (!ctx.Check(p0 > 0f && p1 > 0f && p1 < p0 * 0.9f, $"{a} Lost 'nooo' falls ({p0:0} Hz -> {p1:0} Hz)")) vbad++;
                                }
                            }
                        }
                }
            ctx.Check(greetPitch.ContainsKey(Archetype.Brute) && greetPitch[Archetype.Brute] > 0f && greetPitch[Archetype.Brute] < greetPitch[Archetype.Brainiac] && greetPitch[Archetype.Brainiac] < greetPitch[Archetype.Sprinter],
                $"per-character voices: Brute {Get(greetPitch, Archetype.Brute):0} Hz < Brainiac {Get(greetPitch, Archetype.Brainiac):0} Hz < Sprinter {Get(greetPitch, Archetype.Sprinter):0} Hz (Greeting median F0)");
            // chatter: syllables follow text length, capped at 1.5 s
            string[] texts = { "Hi!", "Finally, some respect.", "Why am I always the one who has to carry everybody across the acid pit?" };
            int prevSyl = 0; float prevLen = 0f; bool chatOk = true;
            for (int i = 0; i < texts.Length; i++)
            {
                int syl = VoiceSynth.SyllablesForText(texts[i]);
                var b = VoiceSynth.RenderChatter(Archetype.Brainiac, 1.05f, syl, 0, VoiceSynth.EndingForText(texts[i]));
                vclips++;
                float len = b != null ? b.Length / (float)Synth.SampleRate : 0f;
                chatOk &= CheckClip($"chatter '{texts[i]}'", b, 0.1f, VoiceSynth.MaxChatterSeconds + 0.25f, true);
                chatOk &= syl >= prevSyl && len >= prevLen - 0.01f;
                prevSyl = syl; prevLen = len;
                if (b != null) AudioTestTools.WriteWav(Path.Combine(dir, "voice", "Chatter_" + i + ".wav"), b, 1);
                ctx.Log($"chatter '{texts[i]}' -> {syl} syllables, {len:0.00} s, ending {VoiceSynth.EndingForText(texts[i])}");
            }
            ctx.Check(chatOk && prevLen <= VoiceSynth.MaxChatterSeconds + 0.25f, "speech chatter grows with text length and is capped near 1.5 s");
            ctx.Check(vbad == 0, $"all {vclips} voice clips (14 lines x 3 archetypes x 3 pitches + variants) pass checks");
            ctx.Log($"voice generation: {vclips} clips, {sw.Elapsed.TotalMilliseconds:0} ms total ({sw.Elapsed.TotalMilliseconds / Math.Max(1, vclips):0.00} ms avg)");
            yield return null;

            // ---------------------------------------------------------- MUSIC
            int mbad = 0;
            double musicMs = 0;
            float musicMB = 0f;
            foreach (MusicId id in Enum.GetValues(typeof(MusicId)))
            {
                if (id == MusicId.None) continue;
                var md = MusicSynth.Render(id);
                if (!ctx.Check(md != null && md.Interleaved != null, $"music {id} renders")) { mbad++; continue; }
                musicMs += md.RenderMs;
                musicMB += md.Interleaved.Length * 4f / 1048576f;
                bool exact = md.Frames == md.Bars * md.BarFrames && md.Interleaved.Length == md.Frames * 2;
                bool finite = AudioFx.AllFinite(md.Interleaved);
                float peak = AudioFx.Peak(md.Interleaved), rms = AudioFx.Rms(md.Interleaved);
                AudioTestTools.Jumps(md.Interleaved, 2, 0, out float inL, out float seamL);
                AudioTestTools.Jumps(md.Interleaved, 2, 1, out float inR, out float seamR);
                float secs = md.Frames / (float)Synth.SampleRate;
                bool ok = ctx.Check(exact && md.Bars >= 4 && md.Bars <= 16, $"music {id}: loop = exactly {md.Bars} bars x {md.BarFrames} frames ({secs:0.00} s @ {md.Bpm:0} BPM)")
                        & ctx.Check(finite && peak <= 0.9f && rms >= 0.08f && rms <= 0.3f, $"music {id}: peak {peak:0.000} (<= -1 dBFS), RMS {rms:0.000} (~{20f * Mathf.Log10(rms):0.0} dBFS), finite")
                        & ctx.Check(seamL <= Math.Max(0.02f, inL) && seamR <= Math.Max(0.02f, inR), $"music {id}: seamless loop point (seam {seamL:0.000}/{seamR:0.000} vs interior max {inL:0.000}/{inR:0.000})")
                        & ctx.Check(md.Bpm >= 90f && md.Bpm <= 160f, $"music {id}: tempo {md.Bpm:0} BPM in range");
                if (!ok) mbad++;
                ctx.Log($"music {id}: render {md.RenderMs:0} ms, bus RMS drums {md.BusRms[0]:0.000} bass {md.BusRms[1]:0.000} keys {md.BusRms[2]:0.000} lead {md.BusRms[3]:0.000}");
                AudioTestTools.WriteWav(Path.Combine(dir, "music", id + ".wav"), md.Interleaved, 2, Synth.Frames(20f));
                plotsMusic.Add(new AudioTestTools.PlotItem { Label = id.ToString(), Data = md.Interleaved, Channels = 2, Seconds = Math.Min(8f, secs) });
                yield return null;
            }
            ctx.Check(mbad == 0, "all music tracks pass");
            ctx.Log($"music generation: {musicMs:0} ms total, {musicMB:0.0} MB float32 for all 7 loops (player caches max {MusicPlayer.MaxCached})");

            // ---------------------------------------------------------- plots
            AudioTestTools.PlotGrid(Path.Combine(dir, "plots", "sfx_comedy.png"), plotsComedy, 4);
            AudioTestTools.PlotGrid(Path.Combine(dir, "plots", "voices.png"), plotsVoice, 4);
            AudioTestTools.PlotGrid(Path.Combine(dir, "plots", "music.png"), plotsMusic, 2, 600, 160);
            ctx.Log("plot order sfx_comedy: " + Labels(plotsComedy));
            ctx.Log("plot order voices: " + Labels(plotsVoice));
            ctx.Log("plot order music: " + Labels(plotsMusic));
        }

        static float Get(Dictionary<Archetype, float> d, Archetype a) => d.TryGetValue(a, out var v) ? v : 0f;

        static string Labels(List<AudioTestTools.PlotItem> l)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < l.Count; i++) { if (i > 0) sb.Append(", "); sb.Append(l[i].Label); }
            return sb.ToString();
        }

        /// <summary>Length, peak (&lt;= 0.99 and normalized), RMS floor, NaN/Inf, DC and end click checks. Logs only failures.</summary>
        static bool CheckClip(string what, float[] b, float minSec, float maxSec, bool checkTail)
        {
            if (b == null || b.Length == 0) return ctx.Check(false, what + ": null/empty clip");
            float secs = b.Length / (float)Synth.SampleRate;
            float peak = AudioFx.Peak(b), rms = AudioFx.Rms(b), dc = Math.Abs(AudioTestTools.Mean(b));
            bool finite = AudioFx.AllFinite(b);
            float tail = checkTail ? AudioTestTools.TailPeak(b, 3f) : 0f;
            bool ok = finite && secs >= minSec && secs <= maxSec && peak <= 0.99f && peak >= 0.5f && rms >= 0.01f && dc < 0.02f && tail < 0.06f;
            if (!ok) ctx.Check(false, $"{what}: len {secs:0.000}s [{minSec}-{maxSec}] peak {peak:0.000} rms {rms:0.0000} dc {dc:0.0000} tail {tail:0.000} finite {finite}");
            return ok;
        }

        // ================================================================== RUNTIME
        static IEnumerator WaitReal(float seconds, Func<bool> until = null)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end && (until == null || !until()))
            {
                System.Threading.Thread.Sleep(14);   // ~60 fps so DevPlay's simulated clock tracks real time (harness timeout)
                yield return null;
            }
        }

        // ================================================================== WEBGL SIMULATION
        /// <summary>No threads + no audio filters (what WebGL gets): prewarm must only queue, rendering is time-sliced
        /// per frame with the music first, and the switcher muffle falls back to a volume duck + pitch drop.</summary>
        public static IEnumerator WebGLSim(DevPlayContext c)
        {
            ctx = c;
            var parentGo = new GameObject("[AudioWebGLSim]");
            var f = new ClipFactory(false);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var bank = new SfxBank(f);
            bank.StartPrewarm();
            double submitMs = sw.Elapsed.TotalMilliseconds;
            ctx.Check(!f.ThreadsAvailable && bank.ClipCount == 0 && submitMs < 30.0, $"no-thread mode: StartPrewarm only queues {bank.PrewarmTotal} jobs ({submitMs:0.0} ms, 0 clips rendered in the call)");
            MusicPlayer.ForceNoFilters = true;
            var mp = new MusicPlayer(parentGo.transform, f);
            MusicPlayer.ForceNoFilters = false;
            ctx.Check(!mp.UsesFilters && parentGo.GetComponentsInChildren<AudioLowPassFilter>().Length == 0, "no-filter mode: no AudioLowPassFilter components are created");
            mp.Play(MusicId.Lab, 0.3f);
            double worst = 0;
            int frames = 0, musicFrame = -1, sfxDoneFrame = -1;
            while ((sfxDoneFrame < 0 || musicFrame < 0) && frames < 4000)
            {
                double t0 = sw.Elapsed.TotalMilliseconds;
                f.Pump(AudioManager.PumpBudgetMs);
                mp.Tick(Time.unscaledTime, 1f / 60f, 1f, 1f, 1f);
                double ms = sw.Elapsed.TotalMilliseconds - t0;
                if (ms > worst) worst = ms;
                if (musicFrame < 0 && mp.Current == MusicId.Lab) musicFrame = frames;
                if (sfxDoneFrame < 0 && bank.PrewarmDone) sfxDoneFrame = frames;
                frames++;
                yield return null;
            }
            ctx.Check(musicFrame >= 0 && sfxDoneFrame >= 0 && musicFrame < sfxDoneFrame,
                $"time-sliced rendering: music (priority) ready at frame {musicFrame}, all {bank.ClipCount} SFX at frame {sfxDoneFrame}");
            ctx.Check(worst < 60.0, $"no frame stalls: worst frame {worst:0.0} ms (render budget {f.SyncRenderBudgetMs} ms + upload {AudioManager.PumpBudgetMs} ms; longest single slice {f.MaxSyncSliceMs:0.0} ms)");
            for (int i = 0; i < 3; i++) { mp.Tick(Time.unscaledTime, 1f / 60f, 1f, 1f, 1f); yield return null; }
            ctx.Check(mp.MuffleGainNow <= MusicPlayer.FallbackMuffleGain + 0.01f && Mathf.Abs(mp.ActiveSource.pitch - MusicPlayer.SlowMoPitch) < 0.01f,
                $"fallback muffle: music ducked to {mp.MuffleGainNow:0.00} + pitch {mp.ActiveSource.pitch:0.00}");
            mp.Tick(Time.unscaledTime, 1f / 60f, 0f, 0f, 1f);
            ctx.Check(mp.MuffleGainNow > 0.99f && Mathf.Abs(mp.ActiveSource.pitch - 1f) < 0.001f, "fallback muffle released");
            mp.Dispose(); bank.Dispose(); f.Dispose();
            UnityEngine.Object.Destroy(parentGo);
        }

        static int EnabledListeners() { int n = 0; foreach (var l in UnityEngine.Object.FindObjectsByType<AudioListener>()) if (l.isActiveAndEnabled) n++; return n; }

        static AudioVoice Newest(AudioManager am, SfxId id) => am.Pool.FindById((int)id, true);

        public static IEnumerator Runtime(DevPlayContext c)
        {
            ctx = c;
            var am = AudioManager.Instance;
            if (!ctx.Check(am != null && am.Pool != null && am.Bank != null && am.Music != null, "AudioManager created by GameBootstrap")) yield break;
            ctx.Log($"audio config: output {AudioSettings.outputSampleRate} Hz, speaker {AudioSettings.GetConfiguration().speakerMode}, threads {am.Factory.ThreadsAvailable}");

            // ---------------------------------------------------------- prewarm budget
            yield return WaitReal(10f, () => am.Bank.PrewarmDone);
            float prewarmWall = am.Bank.PrewarmEndTime - am.Bank.PrewarmStartTime;
            ctx.Check(am.Bank.PrewarmDone, $"SFX prewarm finished: {am.Bank.PrewarmTotal} clips in {prewarmWall * 1000f:0} ms wall-clock (worker thread)");
            ctx.Check(am.Factory.TotalUploadMs <= 300.0 && am.Factory.MaxPumpMs <= 8.0,
                $"prewarm main-thread cost: {am.Factory.TotalUploadMs:0.0} ms total (<= 300), worst frame {am.Factory.MaxPumpMs:0.00} ms (budget {AudioManager.PumpBudgetMs} ms/frame; {am.Factory.GcPumps} pumps hit by a GC pause, longest {am.Factory.MaxGcPumpMs:0.0} ms)");
            ctx.Log($"worker render time {am.Factory.TotalRenderMs:0} ms, clips created {am.Factory.ClipsCreated}, sync renders so far {am.Bank.SyncRenders}, SFX cache {am.Bank.MemoryMB:0.0} MB");

            // ---------------------------------------------------------- scene + real team
            Kit.SolidBox(null, "AudioTestFloor", new Vector3(0f, -0.5f, 0f), new Vector3(40f, 1f, 40f), MatKey.Floor);
            var cam = Camera.main;
            if (cam != null && cam.GetComponent<ShiftersCamera>() == null) cam.gameObject.AddComponent<ShiftersCamera>();
            ctx.Check(EnabledListeners() == 1, "exactly one enabled AudioListener (camera)");
            var mgrGo = new GameObject("[AudioTestCharacters]");
            var mgr = mgrGo.AddComponent<CharacterManager>();
            var switcher = mgrGo.AddComponent<CharacterSwitcher>();
            var poses = new List<Pose> { new Pose(new Vector3(-3f, 0f, 0f), Quaternion.identity), new Pose(new Vector3(0f, 0f, 0f), Quaternion.identity), new Pose(new Vector3(3f, 0f, 0f), Quaternion.identity) };
            mgr.SpawnTeam(CharacterGenerator.GenerateTeam(2026), poses);
            ShifterCharacter brute = null, sprinter = null, brain = null;
            foreach (var ch in mgr.Team)
            {
                if (ch.Archetype == Archetype.Brute) brute = ch; else if (ch.Archetype == Archetype.Sprinter) sprinter = ch; else brain = ch;
            }
            if (!ctx.Check(mgr.Team.Count == 3 && brute != null && sprinter != null && brain != null, "team spawned")) yield break;
            bool allAudio = true;
            foreach (var ch in mgr.Team) allAudio &= ch.GetComponent<CharacterAudio>() != null;
            ctx.Check(allAudio, "every character has CharacterAudio (CharacterFactory)");
            // Sprinter active: the KO/Lost victims below must be INACTIVE (hurting the active one triggers
            // CharacterManager's own auto-switch + arrival quip, which would interleave with the switch checks).
            mgr.SwitchTo(sprinter.Slot, true);
            if (ShiftersCamera.Instance != null) ShiftersCamera.Instance.SetTarget(mgr.Active, true);
            yield return ctx.Wait(0.6f);
            yield return WaitReal(0.9f);
            ctx.Shot("team");

            // footsteps (3 characters walking at once - none of them swallowed), jump, landing
            foreach (var ch in mgr.Team) ch.ExternalControl = true;
            int f0 = am.StartCount(SfxId.Footstep) + am.StartCount(SfxId.FootstepHeavy);
            int heavy0 = am.StartCount(SfxId.FootstepHeavy);
            foreach (var ch in mgr.Team) ch.Motor.SetCommand(new CharacterMotor.Command { Move = Vector3.forward });
            yield return ctx.Wait(1.6f);
            foreach (var ch in mgr.Team) ch.Motor.SetCommand(default);
            int steps = am.StartCount(SfxId.Footstep) + am.StartCount(SfxId.FootstepHeavy) - f0;
            ctx.Check(steps >= 8 && am.StartCount(SfxId.FootstepHeavy) > heavy0, $"walking team -> {steps} footsteps played (Brute uses FootstepHeavy: {am.StartCount(SfxId.FootstepHeavy) - heavy0})");
            yield return ctx.Wait(0.3f);
            int j0 = am.StartCount(SfxId.Jump), l0 = am.StartCount(SfxId.Land) + am.StartCount(SfxId.LandHeavy);
            brain.Motor.SetCommand(new CharacterMotor.Command { Jump = true, JumpHeld = true });
            yield return ctx.Wait(0.15f);
            brain.Motor.SetCommand(default);
            yield return ctx.Wait(1.2f);
            ctx.Check(am.StartCount(SfxId.Jump) > j0, "jump -> boing");
            ctx.Check(am.StartCount(SfxId.Land) + am.StartCount(SfxId.LandHeavy) > l0, "landing -> thud");

            // speech bubble -> gibberish chatter in the speaker's voice, following them
            int vc0 = am.VoiceCount;
            sprinter.Say("Finally, some respect.");
            yield return null;
            var sv = am.Pool.FindOwner(sprinter.Profile);
            ctx.Check(am.VoiceCount == vc0 + 1 && sv != null && sv.Follow == sprinter.transform && sv.Src.clip != null && sv.Src.clip.name.Contains("Sprinter"),
                $"speech -> chatter voice in the Sprinter's voice that follows the speaker ({(sv != null && sv.Src.clip != null ? sv.Src.clip.name + ", " + sv.Src.clip.length.ToString("0.00") + " s, " + sv.Src.clip.samples + " samples" : "none")})");
            ctx.Check(sv != null && sv.Src.clip != null && sv.Src.clip.length <= VoiceSynth.MaxChatterSeconds + 0.3f, "chatter capped near 1.5 s");
            brute.Say("Finally, some respect.");
            yield return null;
            var bv = am.Pool.FindOwner(brute.Profile);
            ctx.Check(bv != null && sv != null && bv.Src.clip != sv.Src.clip && bv.Src.clip != null && bv.Src.clip.name.Contains("Brute"), "same text, different character -> different (Brute) voice clip; both talk at once");
            yield return WaitReal(1.8f);

            // KO -> bonk + "bleh"; revive -> sparkle + greeting; lost -> scream follows the faller
            int ko0 = am.StartCount(SfxId.KO);
            brute.KnockOut("an audio test anvil");
            yield return null;
            var kv = am.Pool.FindOwner(brute.Profile);
            ctx.Check(am.StartCount(SfxId.KO) == ko0 + 1 && kv != null && kv.Line == VoiceLine.KO, "KnockOut -> KO bonk + KO voice");
            yield return WaitReal(0.8f);
            int rv0 = am.StartCount(SfxId.Revive);
            brute.Revive();
            yield return null;
            ctx.Check(am.StartCount(SfxId.Revive) == rv0 + 1, "Revive -> sparkle");
            yield return WaitReal(0.6f);
            var gv = am.Pool.FindOwner(brute.Profile);
            ctx.Check(gv != null && gv.Line == VoiceLine.Greeting, "revived character greets (Greeting voice)");
            int lost0 = am.StartCount(SfxId.Lost);
            brain.Lose("an audio test void");
            yield return null;
            var lv = am.Pool.FindOwner(brain.Profile);
            var ls = Newest(am, SfxId.Lost);
            ctx.Check(am.StartCount(SfxId.Lost) == lost0 + 1 && ls != null && ls.Follow == brain.transform, "Lost -> falling scream attached to the character");
            ctx.Check(lv != null && lv.Line == VoiceLine.Lost, "Lost -> 'nooooo' voice line");
            yield return WaitReal(0.3f);

            // switching: camera transition -> arrival quip only (no double talk); instant switch -> Selected line
            yield return WaitReal(1.2f);
            int vcs = am.VoiceCount;
            bool switched = mgr.SwitchTo(brute.Slot);
            yield return ctx.Wait(1.6f);
            var qv = am.Pool.FindOwner(brute.Profile);
            ctx.Check(switched && mgr.Active == brute && qv != null && (int)qv.Line == 99 && am.VoiceCount == vcs + 1,
                $"transition switch -> exactly one voice: the arrival quip chatter (switched {switched}, voices +{am.VoiceCount - vcs}, line {(qv != null ? qv.Line.ToString() : "none")})");
            yield return WaitReal(1.6f);
            mgr.SwitchTo(sprinter.Slot, true);
            yield return WaitReal(0.35f);
            var selv = am.Pool.FindOwner(sprinter.Profile);
            ctx.Check(mgr.Active == sprinter && selv != null && selv.Line == VoiceLine.Selected, "instant switch -> Selected voice line");

            // ---------------------------------------------------------- spam collapse / bounds / stealing
            yield return WaitReal(0.2f);
            int col0 = am.CollapsedCount, imp0 = am.StartCount(SfxId.Impact);
            var p0 = new Vector3(0f, 1f, 6f);
            for (int i = 0; i < 10; i++) am.PlayAt(SfxId.Impact, p0 + new Vector3(i * 0.1f, 0f, 0f));
            ctx.Check(am.StartCount(SfxId.Impact) - imp0 == 1 && am.CollapsedCount - col0 == 9, $"10 identical impacts in one frame collapse into 1 voice (started {am.StartCount(SfxId.Impact) - imp0}, collapsed {am.CollapsedCount - col0})");
            for (int i = 0; i < 10; i++) am.PlayAt(SfxId.MetalHit, new Vector3(i * 5f - 25f, 1f, 8f));
            ctx.Check(am.StartCount(SfxId.MetalHit) <= AudioManager.MaxStartsPerFrame, $"10 spread-out hits in one frame -> at most {AudioManager.MaxStartsPerFrame} start");
            yield return WaitReal(0.1f);
            int fs0 = am.StartCount(SfxId.Footstep);
            am.PlayAt(SfxId.Footstep, new Vector3(-8f, 0f, 0f)); am.PlayAt(SfxId.Footstep, new Vector3(0f, 0f, 0f)); am.PlayAt(SfxId.Footstep, new Vector3(8f, 0f, 0f));
            ctx.Check(am.StartCount(SfxId.Footstep) - fs0 == 3, "3 characters' footsteps in the same frame all play");
            var rng = new System.Random(5);
            var ids = (SfxId[])Enum.GetValues(typeof(SfxId));
            Exception burstEx = null;
            try
            {
                for (int i = 0; i < 80; i++)
                {
                    var id = ids[1 + rng.Next(ids.Length - 1)];
                    if (SfxTable.Get(id).Loopable) continue;
                    am.PlayAt(id, new Vector3(rng.Next(-15, 15), 1f, rng.Next(-15, 15)), 1f, 0.9f + (float)rng.NextDouble() * 0.2f);
                }
            }
            catch (Exception e) { burstEx = e; }
            int busy3 = am.Pool.CountBusy(true);
            ctx.Check(burstEx == null && busy3 <= AudioManager.Pool3DSize, $"80-sound burst: no exception, {busy3}/{AudioManager.Pool3DSize} 3D voices busy");
            int koS = am.StartCount(SfxId.KO), stolen0 = am.Pool.Stolen;
            am.PlayAt(SfxId.Footstep, new Vector3(12f, 0f, 12f));
            am.PlayAt(SfxId.KO, new Vector3(1f, 1f, 1f));
            ctx.Check(am.StartCount(SfxId.KO) == koS + 1, $"important sound (KO) still starts with a full pool (voices stolen so far {am.Pool.Stolen}, +{am.Pool.Stolen - stolen0} now)");
            yield return WaitReal(1.0f);
            ctx.Check(am.Pool.MaxActive3D <= AudioManager.Pool3DSize && am.Pool.MaxActive2D <= AudioManager.Pool2DSize, $"voice counts bounded (max 3D {am.Pool.MaxActive3D}, max 2D {am.Pool.MaxActive2D})");

            // allocations on the hot path (cached clips)
            am.PlayAt(SfxId.Footstep, Vector3.zero); am.PlayVoice(sprinter.Profile, VoiceLine.Jump, Vector3.zero);
            yield return WaitReal(0.1f);
            long a0 = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 200; i++) { am.PlayAt(SfxId.Footstep, new Vector3(i, 0f, 0f)); am.Play(SfxId.UIHover); }
            long a1 = GC.GetAllocatedBytesForCurrentThread();
            ctx.Check(a1 - a0 < 2048, $"PlayAt/Play on cached clips allocate nothing ({a1 - a0} bytes for 400 calls)");
            yield return WaitReal(0.5f);

            // ---------------------------------------------------------- loops
            int h = am.PlayLoop(SfxId.Siren, new Vector3(4f, 1f, 4f));
            yield return WaitReal(1.5f);
            ctx.Check(h > 0 && am.IsLoopPlaying(h), "PlayLoop(Siren) keeps looping past its 1.2 s clip length");
            am.SetLoopVolume(h, 0.5f);
            am.StopLoop(h, 0.15f);
            yield return WaitReal(0.4f);
            ctx.Check(!am.IsLoopPlaying(h), "StopLoop fades out and frees the voice");
            int h2 = Sfx.Loop(SfxId.Conveyor, Vector3.zero, 0.8f, brute.transform);
            yield return null;
            Sfx.StopLoop(h2, 0f);
            yield return null;
            ctx.Check(h2 > 0 && !am.IsLoopPlaying(h2), "Sfx.Loop / Sfx.StopLoop helpers");

            // ---------------------------------------------------------- music + crossfade
            float tm = Time.realtimeSinceStartup;
            am.PlayMusic(MusicId.Training, 0.5f);
            yield return WaitReal(20f, () => am.CurrentMusic == MusicId.Training);
            ctx.Check(am.CurrentMusic == MusicId.Training && am.Music.ActiveSource.clip != null, $"PlayMusic(Training) starts after background render ({(Time.realtimeSinceStartup - tm) * 1000f:0} ms)");
            yield return WaitReal(0.7f);
            int trainingSrc = -1;
            for (int i = 0; i < 2; i++) if (am.Music.SourceTrack(i) == MusicId.Training) trainingSrc = i;
            ctx.Check(trainingSrc >= 0 && am.Music.SourceFade(trainingSrc) >= 0.999f, "Training faded in fully");
            am.PreloadMusic(MusicId.Factory);
            yield return WaitReal(20f, () => am.Music.IsCached(MusicId.Factory));
            am.PlayMusic(MusicId.Factory, 1.0f);
            yield return null;
            int factorySrc = 1 - trainingSrc;
            yield return WaitReal(0.45f);
            float fOld = am.Music.SourceFade(trainingSrc), fNew = am.Music.SourceFade(factorySrc);
            ctx.Check(am.CurrentMusic == MusicId.Factory && fOld > 0.05f && fOld < 0.95f && fNew > 0.05f && fNew < 0.95f, $"crossfade in progress (Training {fOld:0.00} -> Factory {fNew:0.00})");
            yield return WaitReal(1.0f);
            ctx.Check(am.Music.SourceFade(trainingSrc) <= 0.001f && am.Music.SourceFade(factorySrc) >= 0.999f && am.Music.SourceTrack(trainingSrc) == MusicId.None, "crossfade completed, old source released");
            foreach (var id in new[] { MusicId.Lab, MusicId.Research })
            {
                am.PreloadMusic(id);
                yield return WaitReal(20f, () => am.Music.IsCached(id));
            }
            ctx.Check(am.Music.CachedTracks <= MusicPlayer.MaxCached && am.Music.IsCached(MusicId.Factory), $"music LRU cache bounded ({am.Music.CachedTracks} tracks, playing track kept)");

            // ---------------------------------------------------------- slow-mo muffle
            am.SetSlowMoAmount(1f);
            yield return null; yield return null;
            am.PlayAt(SfxId.Squeak, new Vector3(0f, 1f, 2f));
            am.Play(SfxId.UIClick);
            yield return null;
            var wv = Newest(am, SfxId.Squeak); var uv = Newest(am, SfxId.UIClick);
            var lp = am.Music.ActiveSource.GetComponent<AudioLowPassFilter>();
            ctx.Check(Mathf.Abs(am.Music.Cutoff - MusicPlayer.MuffledCutoff) < 5f && lp != null && lp.enabled && Mathf.Abs(lp.cutoffFrequency - 900f) < 5f && Mathf.Abs(am.Music.ActiveSource.pitch - 0.85f) < 0.01f,
                $"slow-mo 1: music low-pass {am.Music.Cutoff:0} Hz, pitch {am.Music.ActiveSource.pitch:0.00}");
            ctx.Check(wv != null && Mathf.Abs(wv.Src.pitch - wv.BasePitch * AudioManager.SlowMoWorldPitch) < 0.01f && uv != null && Mathf.Abs(uv.Src.pitch - uv.BasePitch) < 0.01f,
                "slow-mo bends 3D world SFX pitch (x0.82), UI sounds unaffected");
            am.SetSlowMoAmount(0.5f);
            yield return null;
            float expect = 22000f * Mathf.Pow(900f / 22000f, 0.5f);
            ctx.Check(Mathf.Abs(am.Music.Cutoff - expect) < expect * 0.03f, $"slow-mo 0.5: cutoff {am.Music.Cutoff:0} Hz (log sweep, expect {expect:0})");
            am.SetSlowMoAmount(0f);
            yield return null;
            ctx.Check(lp != null && !lp.enabled && Mathf.Abs(am.Music.ActiveSource.pitch - 1f) < 0.001f, "slow-mo 0: filter bypassed, pitch 1");
            GameEvents.RaiseSwitcherOpenChanged(true);
            yield return WaitReal(0.45f);
            ctx.Check(am.SlowMoAmount > 0.99f && am.Music.Cutoff < 905f, $"SwitcherOpenChanged(true) ramps the muffle in by itself ({am.SlowMoAmount:0.00})");
            GameEvents.RaiseSwitcherOpenChanged(false);
            yield return WaitReal(0.45f);
            ctx.Check(am.SlowMoAmount < 0.01f, "SwitcherOpenChanged(false) ramps it out");
            switcher.Open();
            yield return ctx.Wait(0.5f);
            bool opened = switcher.IsOpen;
            float smOpen = am.SlowMoAmount, cutOpen = am.Music.Cutoff;
            switcher.Close(false);
            yield return ctx.Wait(0.6f);
            yield return WaitReal(0.3f);
            ctx.Check(!opened || (smOpen > 0.95f && cutOpen < 1000f && am.SlowMoAmount < 0.05f), $"real CharacterSwitcher drives the muffle (opened {opened}, slow-mo {smOpen:0.00}, cutoff {cutOpen:0} Hz, after close {am.SlowMoAmount:0.00})");

            // ---------------------------------------------------------- volumes / settings
            am.ApplyVolumes(0.5f, 0.4f, 0.6f);
            am.PlayAt(SfxId.Bonk, new Vector3(0f, 1f, 3f));
            yield return null; yield return null;
            var bk = Newest(am, SfxId.Bonk);
            var ms = am.Music.ActiveSource;
            float expectMusic = 0.4f * 0.5f * MusicPlayer.BusGain * am.Music.DuckValue;
            ctx.Check(bk != null && Mathf.Abs(bk.Src.volume - bk.BaseVolume * bk.Fade * 0.6f * 0.5f) < 0.002f && Mathf.Abs(ms.volume - expectMusic) < 0.01f,
                $"ApplyVolumes(0.5, 0.4, 0.6): sfx {(bk != null ? bk.Src.volume : -1f):0.000}, music {ms.volume:0.000} (expect {expectMusic:0.000})");
            var gs = GameSession.Instance;
            if (gs != null && gs.Settings != null)
            {
                float m0 = gs.Settings.MasterVolume, mu0 = gs.Settings.MusicVolume, s0 = gs.Settings.SfxVolume;
                gs.Settings.MasterVolume = 0.3f; gs.Settings.MusicVolume = 0.7f; gs.Settings.SfxVolume = 0.2f;
                GameEvents.RaiseSettingsChanged();
                ctx.Check(Mathf.Approximately(am.MasterVolume, 0.3f) && Mathf.Approximately(am.MusicVolume, 0.7f) && Mathf.Approximately(am.SfxVolume, 0.2f), "GameEvents.SettingsChanged -> volumes re-read from GameSession.Settings");
                gs.Settings.MasterVolume = 0f;
                gs.ApplySettings();
                yield return null; yield return null;
                ctx.Check(am.MasterVolume == 0f && ms.volume == 0f, "GameSession.ApplySettings(master 0) mutes everything");
                gs.Settings.MasterVolume = m0; gs.Settings.MusicVolume = mu0; gs.Settings.SfxVolume = s0;
                gs.ApplySettings();
                ctx.Check(Mathf.Approximately(am.MasterVolume, m0), $"settings restored (master {m0:0.00}, music {mu0:0.00}, sfx {s0:0.00})");
            }
            else ctx.Log("GameSession missing - settings path not tested");

            // ---------------------------------------------------------- stingers
            yield return WaitReal(0.3f);
            int st0 = am.StingerCount, ps0 = am.StartCount(SfxId.PuzzleSolved);
            GameEvents.RaisePuzzleSolved("AUDIO TEST");
            Sfx.At(SfxId.PuzzleSolved, new Vector3(2f, 1f, 2f));   // e.g. SequenceLock also plays it itself
            yield return WaitReal(0.25f);
            ctx.Check(am.StartCount(SfxId.PuzzleSolved) == ps0 + 1 && am.StingerCount == st0 + 1, "PuzzleSolved event -> one jingle (a second request is merged)");
            ctx.Check(am.Music.DuckValue < 0.8f, $"jingle ducks the music ({am.Music.DuckValue:0.00})");
            int cp0 = am.StartCount(SfxId.Checkpoint);
            yield return WaitReal(1.1f);
            GameEvents.RaiseCheckpointReached(1);
            yield return null;
            ctx.Check(am.StartCount(SfxId.Checkpoint) == cp0 + 1, "CheckpointReached -> checkpoint jingle");
            int lc0 = am.StartCount(SfxId.LevelComplete);
            GameEvents.RaiseLevelCompleted(new LevelResult());
            yield return WaitReal(1.5f);
            Sfx.Play(SfxId.LevelComplete);   // what the LevelCompleteScreen does when it opens
            ctx.Check(am.StartCount(SfxId.LevelComplete) == lc0 + 1, "LevelCompleted -> fanfare once (UI's later request merged, no double fanfare)");
            yield return WaitReal(20f, () => am.CurrentMusic == MusicId.Victory);
            ctx.Check(am.CurrentMusic == MusicId.Victory, "level complete crossfades to Victory music");
            int st1 = am.StingerCount;
            GameEvents.RaiseTeamWiped();
            yield return WaitReal(0.4f);
            ctx.Check(am.StingerCount == st1 + 1 && am.Music.DuckValue < 0.5f, $"TeamWiped -> sad trombone + music ducked ({am.Music.DuckValue:0.00})");
            GameEvents.RaiseLevelStarted();
            yield return WaitReal(0.8f);
            ctx.Check(am.Music.DuckValue > 0.95f, "LevelStarted clears the duck");

            // ---------------------------------------------------------- pause
            GameEvents.RaisePauseChanged(true);
            yield return WaitReal(0.1f);
            int ui0 = am.StartCount(SfxId.UIClick);
            am.Play(SfxId.UIClick);
            ctx.Check(AudioListener.pause && am.Paused && am.Music.Cutoff < 5000f && am.StartCount(SfxId.UIClick) == ui0 + 1, $"pause: world paused, music muffled ({am.Music.Cutoff:0} Hz), UI clicks still play");
            GameEvents.RaisePauseChanged(false);
            yield return WaitReal(0.1f);
            ctx.Check(!AudioListener.pause && !am.Paused && am.Music.Cutoff > 21000f, "unpause restores");

            // ---------------------------------------------------------- stop music
            am.StopMusic(0.3f);
            yield return WaitReal(0.6f);
            ctx.Check(am.CurrentMusic == MusicId.None && am.Music.SourceFade(0) <= 0.001f && am.Music.SourceFade(1) <= 0.001f, "StopMusic fades both sources out");

            // ---------------------------------------------------------- listener fallback
            var camListener = cam != null ? cam.GetComponent<AudioListener>() : null;
            if (camListener != null)
            {
                camListener.enabled = false;
                yield return WaitReal(3.2f, () => EnabledListeners() == 1);
                ctx.Check(EnabledListeners() == 1, "no listener -> AudioManager enables its fallback listener (never silent, never crashes)");
                camListener.enabled = true;
                yield return WaitReal(2.5f, () => EnabledListeners() == 1 && camListener.isActiveAndEnabled);
                yield return WaitReal(1.2f);
                ctx.Check(EnabledListeners() == 1 && camListener.isActiveAndEnabled, "camera listener back -> fallback disabled (no 'two listeners' warning)");
            }

            ctx.Log($"runtime totals: played {am.PlayedCount}, collapsed {am.CollapsedCount}, rejected {am.RejectedCount}, voices {am.VoiceCount}, stingers {am.StingerCount}, stolen {am.Pool.Stolen}");
            ctx.Log($"memory: {am.CachedMemoryMB:0.0} MB cached PCM (SFX {am.Bank.MemoryMB:0.0} MB in {am.Bank.ClipCount} clips, voices {am.Voices.MemoryMB:0.0} MB in {am.Voices.ClipCount} clips, music {am.Music.CachedTracks} tracks); voice sync renders {am.Voices.SyncRenders}, SFX sync renders {am.Bank.SyncRenders} ({am.Bank.SyncRenderMs:0.0} ms)");
            ctx.Log($"main thread: upload total {am.Factory.TotalUploadMs:0.0} ms, worst frame {am.Factory.MaxPumpMs:0.00} ms; worker render total {am.Factory.TotalRenderMs:0} ms");
            ctx.Shot("end");
        }
    }
}
#endif
