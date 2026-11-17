// =====================================================================================
//  SHIFTERS - E2EHelpers.cs   [INTEGRATION test helpers]
//  Small helpers for the end-to-end DevPlay scenario (EndToEnd.cs): motor driving, condition waits,
//  team lookup by archetype, event counters, reflection access to optional module members and a
//  PlayerPrefs backup (the batchmode Editor shares PlayerPrefs with the user's real project).
// =====================================================================================
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Shifters
{
    public static class E2E
    {
        public static DevPlayContext Ctx;

        // ------------------------------------------------------------------ event counters
        public static int Switched, StateChanges, Banners, Toasts, Prompts, PerspectiveChanges, Checkpoints, LevelsCompleted,
            Speeches, TeamSpawns, LevelStarts, Noises, Puzzles, SwitcherOpens, SwitcherCloses, Crosshairs, Pauses, Objectives;
        public static LevelResult LastResult;
        public static readonly List<string> BannerLog = new List<string>();
        public static readonly List<string> ToastLog = new List<string>();
        public static PerspectiveMask LastPerspective;
        public static ShifterCharacter LastPerspectiveViewer;

        public static void Subscribe()
        {
            Switched = StateChanges = Banners = Toasts = Prompts = PerspectiveChanges = Checkpoints = LevelsCompleted = 0;
            Speeches = TeamSpawns = LevelStarts = Noises = Puzzles = SwitcherOpens = SwitcherCloses = Crosshairs = Pauses = Objectives = 0;
            BannerLog.Clear(); ToastLog.Clear();
            LastResult = default;
            GameEvents.CharacterSwitched += OnSwitched;
            GameEvents.CharacterStateChanged += OnState;
            GameEvents.Banner += OnBanner;
            GameEvents.Toast += OnToast;
            GameEvents.Prompt += OnPrompt;
            GameEvents.PerspectiveChanged += OnPerspective;
            GameEvents.CheckpointReached += OnCheckpoint;
            GameEvents.LevelCompleted += OnLevelCompleted;
            GameEvents.Speech += OnSpeech;
            GameEvents.TeamSpawned += OnTeamSpawned;
            GameEvents.LevelStarted += OnLevelStarted;
            GameEvents.Noise += OnNoise;
            GameEvents.PuzzleSolved += OnPuzzle;
            GameEvents.SwitcherOpenChanged += OnSwitcher;
            GameEvents.CrosshairChanged += OnCrosshair;
            GameEvents.PauseChanged += OnPause;
            GameEvents.ObjectiveChanged += OnObjective;
        }

        public static void Unsubscribe()
        {
            GameEvents.CharacterSwitched -= OnSwitched;
            GameEvents.CharacterStateChanged -= OnState;
            GameEvents.Banner -= OnBanner;
            GameEvents.Toast -= OnToast;
            GameEvents.Prompt -= OnPrompt;
            GameEvents.PerspectiveChanged -= OnPerspective;
            GameEvents.CheckpointReached -= OnCheckpoint;
            GameEvents.LevelCompleted -= OnLevelCompleted;
            GameEvents.Speech -= OnSpeech;
            GameEvents.TeamSpawned -= OnTeamSpawned;
            GameEvents.LevelStarted -= OnLevelStarted;
            GameEvents.Noise -= OnNoise;
            GameEvents.PuzzleSolved -= OnPuzzle;
            GameEvents.SwitcherOpenChanged -= OnSwitcher;
            GameEvents.CrosshairChanged -= OnCrosshair;
            GameEvents.PauseChanged -= OnPause;
            GameEvents.ObjectiveChanged -= OnObjective;
        }

        static void OnSwitched(ShifterCharacter a, ShifterCharacter b) => Switched++;
        static void OnState(ShifterCharacter c) => StateChanges++;
        static void OnBanner(string t, string s, BannerStyle st) { Banners++; BannerLog.Add(t ?? ""); }
        static void OnToast(string t, Color c) { Toasts++; ToastLog.Add(t ?? ""); }
        static void OnPrompt(PromptInfo? p) => Prompts++;
        static void OnPerspective(PerspectiveMask m, ShifterCharacter v) { PerspectiveChanges++; LastPerspective = m; LastPerspectiveViewer = v; }
        static void OnCheckpoint(int i) => Checkpoints++;
        static void OnLevelCompleted(LevelResult r) { LevelsCompleted++; LastResult = r; }
        static void OnSpeech(ShifterCharacter c, string t) => Speeches++;
        static void OnTeamSpawned() => TeamSpawns++;
        static void OnLevelStarted() => LevelStarts++;
        static void OnNoise(NoiseEvent e) => Noises++;
        static void OnPuzzle(string s) => Puzzles++;
        static void OnSwitcher(bool open) { if (open) SwitcherOpens++; else SwitcherCloses++; }
        static void OnCrosshair(bool v) => Crosshairs++;
        static void OnPause(bool p) => Pauses++;
        static void OnObjective(string s) => Objectives++;

        public static bool BannerContains(string part)
        {
            foreach (var b in BannerLog) if (b.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        // ------------------------------------------------------------------ team
        public static CharacterManager Mgr => CharacterManager.Instance;

        public static ShifterCharacter Get(Archetype a)
        {
            var m = Mgr;
            if (m == null) return null;
            foreach (var c in m.Team) if (c != null && c.Archetype == a) return c;
            return null;
        }

        public static ShifterCharacter Brute => Get(Archetype.Brute);
        public static ShifterCharacter Sprinter => Get(Archetype.Sprinter);
        public static ShifterCharacter Brainiac => Get(Archetype.Brainiac);

        public static ShifterCharacter GunOwner
        {
            get
            {
                var m = Mgr;
                if (m == null) return null;
                foreach (var c in m.Team) if (c != null && c.Weapon != null && c.Weapon.HasWeapon) return c;
                return null;
            }
        }

        public static string Who(ShifterCharacter c) => c == null ? "(null)" : c.DisplayName + " [" + c.Archetype + "]";

        public static void TakeControl()
        {
            var m = Mgr;
            if (m == null) return;
            foreach (var c in m.Team) if (c != null) { c.ExternalControl = true; if (c.Motor != null) c.Motor.ClearCommand(); }
        }

        // ------------------------------------------------------------------ driving
        public static void Drive(ShifterCharacter c, Vector3 move, bool sprint = false, bool jump = false, bool jumpHeld = false)
        {
            if (c == null || c.Motor == null) return;
            c.Motor.SetCommand(new CharacterMotor.Command { Move = move, Sprint = sprint, Jump = jump, JumpHeld = jumpHeld || jump });
        }

        public static void Stop(ShifterCharacter c) { if (c != null && c.Motor != null) c.Motor.SetCommand(default); }

        public static void StopAll() { var m = Mgr; if (m != null) foreach (var c in m.Team) Stop(c); }

        public static void Place(ShifterCharacter c, Vector3 feet, float yaw = 0f)
        {
            if (c == null || c.Motor == null) return;
            c.Motor.Teleport(feet, Quaternion.Euler(0f, yaw, 0f));
        }

        public static float Planar(Vector3 a, Vector3 b) { a.y = 0f; b.y = 0f; return Vector3.Distance(a, b); }

        public static bool LastWalkOk;

        /// <summary>Walk (steering every frame) to a planar target; LastWalkOk tells whether it arrived.</summary>
        public static IEnumerator WalkTo(ShifterCharacter c, Vector3 target, bool sprint = false, float tolerance = 0.5f, float timeout = 12f)
        {
            int frames = Mathf.RoundToInt(timeout * 60f);
            LastWalkOk = false;
            for (int i = 0; i < frames; i++)
            {
                if (c == null) yield break;
                var d = target - c.Feet; d.y = 0f;
                if (d.magnitude <= tolerance) { LastWalkOk = true; break; }
                float slow = Mathf.Clamp01(d.magnitude / 1.2f);
                Drive(c, d.normalized * Mathf.Max(0.35f, slow), sprint && d.magnitude > 2f);
                yield return null;
            }
            Stop(c);
        }

        /// <summary>Sprint from where the character stands toward 'to'; jump once when the feet pass 'takeoff'
        /// (measured along the run direction), keep steering until 'to' is reached or the timeout ends.</summary>
        public static IEnumerator RunAndJump(ShifterCharacter c, Vector3 takeoff, Vector3 to, float timeout = 6f)
        {
            if (c == null) yield break;
            Vector3 dir = to - c.Feet; dir.y = 0f; dir.Normalize();
            float takeoffAlong = Vector3.Dot(takeoff - c.Feet, dir);
            Vector3 start = c.Feet;
            bool jumped = false;
            LastWalkOk = false;
            int frames = Mathf.RoundToInt(timeout * 60f), airFrames = 0;
            for (int i = 0; i < frames; i++)
            {
                float along = Vector3.Dot(c.Feet - start, dir);
                var rem = to - c.Feet; rem.y = 0f;
                if (rem.magnitude < 0.6f && c.Motor.IsGrounded) { LastWalkOk = true; break; }
                bool jumpNow = !jumped && along >= takeoffAlong;
                if (jumpNow) jumped = true;
                if (jumped) airFrames++;
                Drive(c, (rem.sqrMagnitude > 0.01f ? rem.normalized : dir), true, jumpNow, jumped && airFrames < 30);
                yield return null;
            }
            Stop(c);
        }

        public static bool LastUntilOk;

        public static IEnumerator Until(Func<bool> cond, float timeout)
        {
            int frames = Mathf.RoundToInt(timeout * 60f);
            LastUntilOk = cond();
            for (int i = 0; i < frames && !LastUntilOk; i++) { yield return null; LastUntilOk = cond(); }
        }

        /// <summary>Switch through CharacterManager and wait for the camera transition to finish.</summary>
        public static IEnumerator SwitchTo(ShifterCharacter c, bool instant = false)
        {
            var m = Mgr;
            if (m == null || c == null) yield break;
            if (m.Active == c) yield break;
            yield return Until(() => !m.IsTransitioning, 3f);
            bool ok = m.SwitchTo(c.Slot, instant);
            Ctx.Check(ok, "SwitchTo(" + Who(c) + (instant ? ", instant" : "") + ") accepted");
            yield return Until(() => !m.IsTransitioning && m.Active == c && c.IsActive, 4f);
            Ctx.Check(m.Active == c && c.IsActive, "active is now " + Who(c));
            foreach (var t in m.Team) if (t != null) t.ExternalControl = true;
        }

        /// <summary>Point the gameplay camera (yaw/pitch, degrees) - used to frame screenshots.</summary>
        public static void Frame(float yaw, float pitch = 18f)
        {
            var cam = ShiftersCamera.Instance;
            if (cam == null) return;
            cam.Yaw = yaw;
            cam.Pitch = pitch;
        }

        public static float YawTo(Vector3 from, Vector3 to) { var d = to - from; d.y = 0f; return d.sqrMagnitude < 1e-6f ? 0f : Quaternion.LookRotation(d).eulerAngles.y; }

        // ------------------------------------------------------------------ reflection (optional module members)
        const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        /// <summary>Value of a field/property by name (instance or static), or null if missing.</summary>
        public static object Member(object target, string name)
        {
            if (target == null) return null;
            var t = target as Type ?? target.GetType();
            object inst = target is Type ? null : target;
            for (; t != null; t = t.BaseType)
            {
                var p = t.GetProperty(name, Flags | BindingFlags.DeclaredOnly);
                if (p != null && p.GetIndexParameters().Length == 0) { try { return p.GetValue(inst); } catch { return null; } }
                var f = t.GetField(name, Flags | BindingFlags.DeclaredOnly);
                if (f != null) return f.GetValue(inst);
            }
            return null;
        }

        public static bool HasMember(object target, string name)
        {
            if (target == null) return false;
            for (var t = target.GetType(); t != null; t = t.BaseType)
                if (t.GetProperty(name, Flags | BindingFlags.DeclaredOnly) != null || t.GetField(name, Flags | BindingFlags.DeclaredOnly) != null) return true;
            return false;
        }

        /// <summary>Invokes a parameterless instance method by name (optional module API); false if missing.</summary>
        public static bool Invoke(object target, string method)
        {
            if (target == null) return false;
            var m = target.GetType().GetMethod(method, BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
            if (m == null) return false;
            m.Invoke(target, null);
            return true;
        }

        public static string TypeName(object o) => o == null ? "null" : o.GetType().Name;

        public static bool Truthy(object o) => o is bool b ? b : o != null && !(o is UnityEngine.Object u && u == null);

        // ------------------------------------------------------------------ scene helpers
        public static BoxCollider Box(Transform parent, string n, Vector3 center, Vector3 size, MatKey mat = MatKey.Floor)
            => Kit.SolidBox(parent, n, center, size, mat);

        public static int CountShaderProblems(out string firstBad)
        {
            firstBad = null;
            int bad = 0;
            foreach (var r in UnityEngine.Object.FindObjectsByType<Renderer>())
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                if (r is ParticleSystemRenderer || r is LineRenderer || r is TrailRenderer) continue;
                if (r.transform.position.y < -500f) continue; // portrait studio and other off-world rigs
                foreach (var m in r.sharedMaterials)
                {
                    string why = null;
                    if (m == null) why = "null material";
                    else if (m.shader == null) why = "null shader";
                    else
                    {
                        var sn = m.shader.name;
                        if (sn == "Hidden/InternalErrorShader") why = "magenta (error shader)";
                        else if (m.name.StartsWith("Default-Material") || m.name.StartsWith("Lit (Instance)")) why = "default material (" + m.name + ")";
                    }
                    if (why != null) { bad++; if (firstBad == null) firstBad = r.name + ": " + why; }
                }
            }
            return bad;
        }
    }

    /// <summary>Backs up the SHIFTERS PlayerPrefs keys before an integration run and restores them afterwards
    /// (completing a level marks it completed and saves the run - never leak that into the user's real prefs).</summary>
    public sealed class E2EPrefsBackup
    {
        readonly Dictionary<string, string> _strings = new Dictionary<string, string>();
        readonly Dictionary<string, int> _ints = new Dictionary<string, int>();
        readonly Dictionary<string, float> _floats = new Dictionary<string, float>();
        static readonly string[] StringKeys = { SessionPrefs.SettingsKey, SessionPrefs.RunKey };

        public static E2EPrefsBackup Capture()
        {
            var b = new E2EPrefsBackup();
            foreach (var k in StringKeys) if (PlayerPrefs.HasKey(k)) b._strings[k] = PlayerPrefs.GetString(k);
            if (PlayerPrefs.HasKey(SessionPrefs.CompletedKey)) b._ints[SessionPrefs.CompletedKey] = PlayerPrefs.GetInt(SessionPrefs.CompletedKey);
            for (int i = 0; i < 8; i++) { string k = SessionPrefs.BestTimePrefix + i; if (PlayerPrefs.HasKey(k)) b._floats[k] = PlayerPrefs.GetFloat(k); }
            return b;
        }

        public void Restore()
        {
            foreach (var k in StringKeys) { if (_strings.TryGetValue(k, out var v)) PlayerPrefs.SetString(k, v); else PlayerPrefs.DeleteKey(k); }
            if (_ints.TryGetValue(SessionPrefs.CompletedKey, out int c)) PlayerPrefs.SetInt(SessionPrefs.CompletedKey, c); else PlayerPrefs.DeleteKey(SessionPrefs.CompletedKey);
            for (int i = 0; i < 8; i++)
            {
                string k = SessionPrefs.BestTimePrefix + i;
                if (_floats.TryGetValue(k, out float f)) PlayerPrefs.SetFloat(k, f); else PlayerPrefs.DeleteKey(k);
            }
            PlayerPrefs.Save();
        }
    }
}
