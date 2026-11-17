// =====================================================================================
//  SHIFTERS - LightBudget.cs   (RENDERING)
//  WebGL / Mobile real-time light budget. WebGL renders with the Mobile quality level (Mobile_RPAsset: plain Forward,
//  per-object light loop), where every visible point/spot light costs per pixel. When a gameplay scene loads, a pass
//  runs in the first LateUpdate (after every Start) and again ~1 s and ~3 s later (lights level code creates late):
//    * KEPT, never touched: directional lights (sun); checkpoint lamps (CheckpointManager lights the active one);
//      lights on moving gameplay objects (under a Rigidbody: bot sirens); HUD portrait-studio lights; shadow-casting
//      lights; lights that are off when the pass runs (gameplay toggles them); DecorLight.KeepOnLowEnd; persistent
//      systems (DontDestroyOnLoad: Fx flashes).
//    * DECOR, switched off: DecorLight-marked lights (EnvKit.LightPanel / Decor.WarningBeacon add the marker) and, for
//      scenes saved before the marker existed, EnvKit lights recognized by hierarchy (LightPanel/Light,
//      WarningBeacon/Head/Spot*). Emissive faces + bloom keep the look.
//    * anything else (non-shadow point/spot lights placed by level code, e.g. holding-cell lamps): the KeepUnknown
//      nearest to the level path (spawn -> checkpoints by index -> exit) stay on, the rest count as decor.
//  Low-end = WebGL player, the Mobile URP asset rendering (WebGL's quality level 0 "Mobile"), a player's quality level
//  named "Mobile", or the Low graphics setting (PostFx.Quality 0); ForceLowEnd overrides. Leaving low-end (settings
//  change) restores every light it switched off.
//  Menus (no LevelManager / checkpoints / exit) keep their staging lights. Play mode only; no per-frame work.
// =====================================================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Shifters
{
    /// <summary>Real-time light budget for WebGL/Mobile (see file header). Static API; a hidden runner schedules the
    /// passes. Use <see cref="DecorLight"/> to mark your own decorative lights.</summary>
    [DefaultExecutionOrder(10000)]
    public class LightBudget : MonoBehaviour
    {
        /// <summary>Real-time lights a gameplay scene keeps on low-end (sun and active checkpoint lamp included).</summary>
        public const int MaxLights = 6;
        /// <summary>Unclassified level lights kept on low-end: MaxLights minus the sun and the checkpoint lamp.</summary>
        public const int KeepUnknown = MaxLights - 2;

        /// <summary>Override for tests/settings: true/false forces low-end on/off; null = detect (see IsLowEnd).</summary>
        public static bool? ForceLowEnd;

        /// <summary>Counters of the last pass (tests, debug overlay).</summary>
        public struct PassStats
        {
            public bool LowEnd, GameplayScene;
            public string Reason;
            public int Lights, Protected, DecorOff, UnknownKept, UnknownOff, EnabledAfter;
            public override string ToString() =>
                "lowEnd=" + LowEnd + (LowEnd ? " (" + Reason + ")" : "") + " gameplay=" + GameplayScene + " lights=" + Lights + " protected=" + Protected +
                " decorOff=" + DecorOff + " unknownKept=" + UnknownKept + " unknownOff=" + UnknownOff + " enabledAfter=" + EnabledAfter;
        }

        /// <summary>Stats of the most recent pass.</summary>
        public static PassStats Last { get; private set; }
        /// <summary>Number of passes run since play mode started.</summary>
        public static int Passes { get; private set; }

        enum Kind { Keep, Decor, Unknown }

        static LightBudget _runner;
        static readonly List<Light> _switchedOff = new List<Light>();
        static readonly List<KeyValuePair<float, Light>> _unknown = new List<KeyValuePair<float, Light>>();
        static readonly List<Vector3> _path = new List<Vector3>();
        static readonly List<Checkpoint> _checkpoints = new List<Checkpoint>();
        static readonly Comparison<KeyValuePair<float, Light>> ByDistance = (a, b) => a.Key.CompareTo(b.Key);
        static readonly Comparison<Checkpoint> ByIndex = (a, b) => a.Index.CompareTo(b.Index);

        int _passA = -1, _passB = -1, _passC = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            ForceLowEnd = null;
            Last = default;
            Passes = 0;
            _runner = null;
            _switchedOff.Clear();
            _unknown.Clear();
            _path.Clear();
            _checkpoints.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Schedule();
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Schedule();

        /// <summary>True when the low-end budget applies (see LowEndReason).</summary>
        public static bool IsLowEnd => LowEndReason != null;

        /// <summary>Why the low-end budget applies, or null when it does not: ForceLowEnd; the WebGL player; the Low graphics
        /// setting (PostFx.Quality 0); the Mobile URP asset rendering (WebGL's quality level 0, "Mobile"); in players also the
        /// active quality level's NAME containing "Mobile". The raw index is not used: Standalone builds strip the "Mobile"
        /// level (index 0 is "PC" there), and the editor can report a level whose pipeline override is not the one rendering.</summary>
        public static string LowEndReason
        {
            get
            {
                if (ForceLowEnd.HasValue) return ForceLowEnd.Value ? "forced" : null;
                if (Application.platform == RuntimePlatform.WebGLPlayer) return "WebGL player";
                if (PostFx.Quality == 0) return "Low graphics setting";
                var rp = GraphicsSettings.currentRenderPipeline;
                if (rp != null && rp.name.StartsWith("Mobile", StringComparison.OrdinalIgnoreCase)) return "Mobile URP asset " + rp.name;
                if (!Application.isEditor)
                {
                    int q = QualitySettings.GetQualityLevel();
                    var names = QualitySettings.names;
                    if (q >= 0 && q < names.Length && names[q].IndexOf("Mobile", StringComparison.OrdinalIgnoreCase) >= 0) return "quality level " + names[q];
                }
                return null;
            }
        }

        /// <summary>Queues passes: the next LateUpdate (after every Start), then ~60 and ~180 frames later.</summary>
        public static void Schedule()
        {
            if (!Application.isPlaying) return;
            var r = Runner();
            int f = Time.frameCount;
            r._passA = f;
            r._passB = f + 60;
            r._passC = f + 180;
            r.enabled = true;
        }

        /// <summary>Re-evaluates now (call after the graphics quality changed or after spawning lights at runtime).</summary>
        public static void Refresh()
        {
            if (Application.isPlaying) Apply();
        }

        /// <summary>Runs one budget pass now (idempotent). Low-end gameplay scene: decor lights off, unclassified lights
        /// capped. Not low-end: every light a previous pass switched off is switched back on.</summary>
        public static void Apply()
        {
            if (!Application.isPlaying) return;
            Passes++;
            PruneDestroyed();
            string reason = LowEndReason;
            var stats = new PassStats { LowEnd = reason != null, Reason = reason, GameplayScene = IsGameplayScene() };
            if (!stats.LowEnd) Restore();
            if (!stats.LowEnd || !stats.GameplayScene)
            {
                stats.EnabledAfter = CountEnabledSceneLights();
                Last = stats;
                return;
            }

            BuildPath();
            _unknown.Clear();
            var lights = Object.FindObjectsByType<Light>();
            for (int i = 0; i < lights.Length; i++)
            {
                var l = lights[i];
                if (l == null) continue;
                stats.Lights++;
                bool ours = _switchedOff.Contains(l);
                switch (Classify(l))
                {
                    case Kind.Keep:
                        stats.Protected++;
                        if (ours) SwitchOn(l);
                        break;
                    case Kind.Decor:
                        if (l.enabled || ours) { SwitchOff(l); stats.DecorOff++; }
                        break;
                    default:
                        // a light that is off and was not switched off by us belongs to gameplay code: leave it alone
                        if (l.enabled || ours) _unknown.Add(new KeyValuePair<float, Light>(DistanceToPath(l.transform.position), l));
                        break;
                }
            }
            _unknown.Sort(ByDistance);
            for (int i = 0; i < _unknown.Count; i++)
            {
                var l = _unknown[i].Value;
                if (i < KeepUnknown) { SwitchOn(l); stats.UnknownKept++; }
                else { SwitchOff(l); stats.UnknownOff++; }
            }
            _unknown.Clear();
            stats.EnabledAfter = CountEnabledSceneLights();
            Last = stats;
        }

        /// <summary>Switches every light the budget turned off back on.</summary>
        public static void Restore()
        {
            for (int i = 0; i < _switchedOff.Count; i++)
                if (_switchedOff[i] != null) _switchedOff[i].enabled = true;
            _switchedOff.Clear();
        }

        /// <summary>True if the last passes switched this light off.</summary>
        public static bool IsSwitchedOff(Light l) => l != null && _switchedOff.Contains(l);

        /// <summary>Enabled point/spot/directional lights in loaded scenes (persistent DontDestroyOnLoad systems and the HUD
        /// portrait studio excluded): the level's real-time light count.</summary>
        public static int CountEnabledSceneLights()
        {
            int n = 0;
            var lights = Object.FindObjectsByType<Light>();
            for (int i = 0; i < lights.Length; i++)
            {
                var l = lights[i];
                if (l == null || !l.isActiveAndEnabled || IsPersistent(l.gameObject)) continue;
                if (l.GetComponentInParent<PortraitStudio>() != null) continue;
                n++;
            }
            return n;
        }

        // ------------------------------------------------------------------ classification
        static Kind Classify(Light l)
        {
            if (l.type != LightType.Point && l.type != LightType.Spot) return Kind.Keep; // sun; area lights are baked-only
            if (IsPersistent(l.gameObject)) return Kind.Keep;
            var marker = l.GetComponent<DecorLight>();
            if (marker != null) return marker.KeepOnLowEnd ? Kind.Keep : Kind.Decor;
            if (IsEnvKitDecor(l.transform)) return Kind.Decor;
            if (l.shadows != LightShadows.None) return Kind.Keep;
            if (IsCheckpointLamp(l)) return Kind.Keep;
            if (l.GetComponentInParent<Rigidbody>() != null) return Kind.Keep;
            if (l.GetComponentInParent<PortraitStudio>() != null) return Kind.Keep;
            return Kind.Unknown;
        }

        /// <summary>EnvKit-built decorative lights in scenes saved before DecorLight existed.</summary>
        static bool IsEnvKitDecor(Transform t)
        {
            var p = t.parent;
            if (p == null) return false;
            string n = t.name, pn = p.name;
            if (n == "Light" && pn == "LightPanel") return true;
            return n.StartsWith("Spot", StringComparison.Ordinal) && pn == "Head" && p.parent != null && p.parent.name == "WarningBeacon";
        }

        static bool IsCheckpointLamp(Light l)
        {
            var all = Checkpoint.All;
            for (int i = 0; i < all.Count; i++)
                if (all[i] != null && all[i].Lamp == l) return true;
            return l.GetComponentInParent<Checkpoint>() != null;
        }

        static bool IsPersistent(GameObject go)
        {
            var s = go.scene;
            return !s.IsValid() || (s.buildIndex == -1 && s.name == "DontDestroyOnLoad");
        }

        static bool IsGameplayScene() => LevelManager.Instance != null || Checkpoint.All.Count > 0 || LevelExit.All.Count > 0;

        // ------------------------------------------------------------------ path
        static void BuildPath()
        {
            _path.Clear();
            var lm = LevelManager.Instance;
            if (lm != null && lm.SpawnPoints != null)
                for (int i = 0; i < lm.SpawnPoints.Length; i++)
                    if (lm.SpawnPoints[i] != null) { _path.Add(lm.SpawnPoints[i].position); break; }
            _checkpoints.Clear();
            for (int i = 0; i < Checkpoint.All.Count; i++) if (Checkpoint.All[i] != null) _checkpoints.Add(Checkpoint.All[i]);
            _checkpoints.Sort(ByIndex);
            for (int i = 0; i < _checkpoints.Count; i++) _path.Add(_checkpoints[i].transform.position);
            _checkpoints.Clear();
            for (int i = 0; i < LevelExit.All.Count; i++) if (LevelExit.All[i] != null) _path.Add(LevelExit.All[i].transform.position);
            if (_path.Count == 0)
            {
                var cam = Camera.main;
                _path.Add(cam != null ? cam.transform.position : Vector3.zero);
            }
        }

        static float DistanceToPath(Vector3 p)
        {
            if (_path.Count == 1) return Vector3.Distance(p, _path[0]);
            float best = float.MaxValue;
            for (int i = 0; i + 1 < _path.Count; i++)
            {
                var a = _path[i];
                var ab = _path[i + 1] - a;
                float len2 = ab.sqrMagnitude;
                float t = len2 > 1e-6f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / len2) : 0f;
                float d = (a + ab * t - p).sqrMagnitude;
                if (d < best) best = d;
            }
            return Mathf.Sqrt(best);
        }

        // ------------------------------------------------------------------ switching
        static void SwitchOff(Light l)
        {
            if (!_switchedOff.Contains(l)) _switchedOff.Add(l);
            if (l.enabled) l.enabled = false;
        }

        static void SwitchOn(Light l)
        {
            if (_switchedOff.Remove(l)) l.enabled = true;
        }

        static void PruneDestroyed()
        {
            for (int i = _switchedOff.Count - 1; i >= 0; i--) if (_switchedOff[i] == null) _switchedOff.RemoveAt(i);
        }

        // ------------------------------------------------------------------ runner
        static LightBudget Runner()
        {
            if (_runner != null) return _runner;
            var go = new GameObject("[LightBudget]");
            DontDestroyOnLoad(go);
            _runner = go.AddComponent<LightBudget>();
            return _runner;
        }

        void LateUpdate()
        {
            int f = Time.frameCount;
            if (_passA >= 0 && f >= _passA) { _passA = -1; Apply(); }
            if (_passB >= 0 && f >= _passB) { _passB = -1; Apply(); }
            if (_passC >= 0 && f >= _passC) { _passC = -1; Apply(); }
            if (_passA < 0 && _passB < 0 && _passC < 0) enabled = false;
        }

        void OnDestroy()
        {
            if (_runner == this) _runner = null;
        }
    }
}
