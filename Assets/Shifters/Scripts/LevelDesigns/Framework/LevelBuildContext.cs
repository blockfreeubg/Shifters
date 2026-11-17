// =====================================================================================
//  SHIFTERS - LevelBuildContext.cs   (BUILDER module - runtime level framework)
//  Everything an ILevelBuilder needs: categorized roots, theme/info, spawn + checkpoint + exit + tutorial
//  + hint passthroughs to LevelKit (with visible placeholders while LevelKit is missing), LevelMark helpers,
//  a few exact-geometry helpers (top-surface platforms, ramps, labels) and bookkeeping for the build report.
//  All positions are LOCAL to the given parent; the default roots sit at the world origin (level space = world).
// =====================================================================================
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

namespace Shifters
{
    public class LevelBuildContext
    {
        // ------------------------------------------------------------------ identity
        public ILevelBuilder Builder { get; }
        public LevelInfo Info { get; }
        public LevelTheme Theme => Info.Theme;
        /// <summary>The builder's index (LevelCatalog index, or a special index like L0_Sandbox.Index).</summary>
        public int LevelIndex { get; }
        /// <summary>Index given to LevelManager: the catalog index, or 0 for non-catalog scenes (sandbox/gallery).</summary>
        public int ManagerLevelIndex { get; }
        public bool IsCatalogLevel { get; }
        public bool IsEditMode => !Application.isPlaying;
        /// <summary>True if Build threw - the editor builder then refuses to save the scene.</summary>
        public bool Failed { get; internal set; }

        // ------------------------------------------------------------------ roots
        public Transform Root { get; }
        public Transform Geometry { get; }
        public Transform Puzzles { get; }
        public Transform Props { get; }
        public Transform Hazards { get; }
        public Transform Enemies { get; }
        public Transform Decor { get; }
        public Transform Gameplay { get; }
        public Transform Lights { get; }
        /// <summary>Container for MarkPoint markers.</summary>
        public Transform MarkRoot { get; }

        // ------------------------------------------------------------------ gameplay state
        public Transform[] SpawnPoints { get; private set; } = new Transform[0];
        public Vector3 SpawnPosition { get; private set; }
        public float SpawnYaw { get; private set; }
        public bool HasSpawn { get; private set; }
        public int CheckpointCount { get; private set; }
        public int ExitCount { get; private set; }
        public int TutorialCount { get; private set; }
        public int HintCount { get; private set; }
        /// <summary>Highest checkpoint index used so far (auto indices continue from here).</summary>
        public int LastCheckpointIndex { get; private set; }

        readonly Dictionary<string, GameObject> _marks = new Dictionary<string, GameObject>();
        public IReadOnlyDictionary<string, GameObject> Marks => _marks;
        public readonly List<string> Warnings = new List<string>();
        public readonly Dictionary<string, int> Counts = new Dictionary<string, int>();
        public float BuildSeconds { get; internal set; }

        public static string RootName(LevelInfo info) => "[LEVEL] " + (info != null ? info.SceneName : "Level");

        public LevelBuildContext(ILevelBuilder builder, LevelInfo info, Transform parent = null)
        {
            Builder = builder;
            Info = info ?? LevelBuilderRegistry.InfoFor(builder);
            LevelIndex = builder != null ? builder.LevelIndex : Info.Index;
            IsCatalogLevel = builder != null && LevelBuilderRegistry.IsCatalogLevel(builder);
            ManagerLevelIndex = IsCatalogLevel ? LevelIndex : 0;
            Root = Shifters.Kit.Make(RootName(Info), parent, Vector3.zero).transform;
            Geometry = Child("Geometry");
            Puzzles = Child("Puzzles");
            Props = Child("Props");
            Hazards = Child("Hazards");
            Enemies = Child("Enemies");
            Decor = Child("Decor");
            Gameplay = Child("Gameplay");
            Lights = Child("Lights");
            MarkRoot = Shifters.Kit.Make("Marks", Gameplay, Vector3.zero).transform;
        }

        Transform Child(string name) => Shifters.Kit.Make(name, Root, Vector3.zero).transform;

        // ------------------------------------------------------------------ grouping
        /// <summary>Child transform of Root for grouping a section (e.g. "S2_AptitudeTests") at origin with yaw.</summary>
        public Transform Section(string name, Vector3 origin, float yaw = 0f)
        {
            Count("sections");
            return Shifters.Kit.Make(name, Root, origin, Quaternion.Euler(0f, yaw, 0f)).transform;
        }

        // ------------------------------------------------------------------ exact geometry helpers
        /// <summary>Static slab whose TOP surface is centered at topCenter (walkable height = topCenter.y).
        /// Adds thin trim strips along the top edges when trim is true.</summary>
        public BoxCollider Platform(Transform parent, Vector3 topCenter, Vector3 size, MatKey mat = MatKey.Floor, bool trim = true, string name = "Platform")
        {
            parent = parent != null ? parent : Geometry;
            var col = Shifters.Kit.SolidBox(parent, name, topCenter - new Vector3(0f, size.y * 0.5f, 0f), size, mat, 0.05f);
            if (trim && size.x > 1f && size.z > 1f)
            {
                var t = col.transform;
                float h = 0.06f, w = 0.12f, y = size.y * 0.5f - h * 0.5f + 0.005f;
                Shifters.Kit.BoxVisual(t, "TrimN", new Vector3(0f, y, size.z * 0.5f - w * 0.5f), new Vector3(size.x, h, w), MatKey.Trim, 0.01f, null, false);
                Shifters.Kit.BoxVisual(t, "TrimS", new Vector3(0f, y, -size.z * 0.5f + w * 0.5f), new Vector3(size.x, h, w), MatKey.Trim, 0.01f, null, false);
                Shifters.Kit.BoxVisual(t, "TrimE", new Vector3(size.x * 0.5f - w * 0.5f, y, 0f), new Vector3(w, h, size.z - 2f * w), MatKey.Trim, 0.01f, null, false);
                Shifters.Kit.BoxVisual(t, "TrimW", new Vector3(-size.x * 0.5f + w * 0.5f, y, 0f), new Vector3(w, h, size.z - 2f * w), MatKey.Trim, 0.01f, null, false);
            }
            Count("platforms");
            return col;
        }

        /// <summary>Static box centered at center (plain Kit.SolidBox with bookkeeping).</summary>
        public BoxCollider Block(Transform parent, Vector3 center, Vector3 size, MatKey mat = MatKey.Wall, string name = "Block", Quaternion? rot = null)
        {
            Count("blocks");
            return Shifters.Kit.SolidBox(parent != null ? parent : Geometry, name, center, size, mat, 0.05f, rot);
        }

        /// <summary>Walkable ramp whose top surface runs from bottom to top (both on the walking surface).</summary>
        public BoxCollider Ramp(Transform parent, Vector3 bottom, Vector3 top, float width, float thickness = 0.4f, MatKey mat = MatKey.FloorAlt, string name = "Ramp")
        {
            var d = top - bottom;
            float len = d.magnitude;
            if (len < 0.01f) return null;
            var rot = Quaternion.LookRotation(d / len, Vector3.up);
            var normal = rot * Vector3.up;
            var center = (bottom + top) * 0.5f - normal * (thickness * 0.5f);
            Count("ramps");
            return Shifters.Kit.SolidBox(parent != null ? parent : Geometry, name, center, new Vector3(width, thickness, len), mat, 0.03f, rot);
        }

        static TMP_FontAsset _labelFont;
        static bool _labelFontResolved;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _labelFont = null; _labelFontResolved = false; }

        static TMP_FontAsset LabelFont
        {
            get
            {
                if (_labelFontResolved && _labelFont != null) return _labelFont;
                _labelFontResolved = true;
                string[] names = { "Fonts/BarlowCondensed-Bold SDF", "Fonts/BarlowCondensed SDF", "Fonts/Barlow Condensed Bold SDF", "Fonts/Bungee-Regular SDF", "Fonts/Bungee SDF" };
                foreach (var n in names) { _labelFont = Resources.Load<TMP_FontAsset>(n); if (_labelFont != null) return _labelFont; }
                _labelFont = TMP_Settings.defaultFontAsset;
                return _labelFont;
            }
        }

        /// <summary>World-space TextMeshPro label (no background). height = approximate letter height in meters.</summary>
        public TextMeshPro Label(Transform parent, Vector3 position, Quaternion rotation, string text, float height = 0.5f, Color? color = null, float maxWidth = 12f)
        {
            var go = Shifters.Kit.Make("Label_" + Sanitize(text, 24), parent != null ? parent : Decor, position, rotation);
            var tmp = go.AddComponent<TextMeshPro>();
            var font = LabelFont;
            if (font != null) tmp.font = font;
            tmp.text = text;
            tmp.fontSize = height * 10f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.color = color ?? Color.white;
            tmp.rectTransform.sizeDelta = new Vector2(maxWidth, height * 2.5f);
            var r = go.GetComponent<MeshRenderer>();
            if (r != null) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Count("labels");
            return tmp;
        }

        /// <summary>A readable sign: EnvKit.Sign when available, else a dark panel with a TMP label. Faces -Z of yaw
        /// (i.e. readable by someone looking along +Z rotated by yaw... text faces the viewer at position - forward).</summary>
        public GameObject Sign(Transform parent, Vector3 position, float yaw, string text, float letterHeight = 0.45f)
        {
            parent = parent != null ? parent : Decor;
            var rot = Quaternion.Euler(0f, yaw, 0f);
            var go = KitCatalog.Spawn("EnvKit", "Sign", parent, position, rot, text);
            if (go != null) { Count("signs"); return go; }
            float w = Mathf.Max(1.2f, text.Length * letterHeight * 0.62f + 0.6f), h = letterHeight * 2.2f;
            var root = Shifters.Kit.Make("Sign_" + Sanitize(text, 20), parent, position, rot);
            Shifters.Kit.BoxVisual(root.transform, "Panel", Vector3.zero, new Vector3(w, h, 0.08f), MatKey.MetalDark, 0.02f);
            Shifters.Kit.BoxVisual(root.transform, "Stripe", new Vector3(0f, -h * 0.5f + 0.05f, -0.045f), new Vector3(w, 0.08f, 0.02f), MatKey.Trim, 0.005f, null, false);
            Label(root.transform, new Vector3(0f, 0.02f, -0.06f), Quaternion.identity, text, letterHeight, new Color(1f, 0.95f, 0.85f), w);
            Count("signs");
            return root;
        }

        // ------------------------------------------------------------------ gameplay passthroughs (LevelKit)
        /// <summary>Sets the level start: 3 spawn points in a row (LevelKit.SpawnPoints, 1.6 m apart) facing yaw.
        /// Marked "spawn" (the first point's parent/holder).</summary>
        public Transform[] SetSpawn(Vector3 position, float yaw)
        {
            if (HasSpawn) Warn("SetSpawn called twice - replacing the previous spawn");
            foreach (var old in SpawnPoints) if (old != null && old.parent != null && old.parent.name == "StartSpawns") { Shifters.Kit.SafeDestroy(old.parent.gameObject); break; }
            HasSpawn = true; SpawnPosition = position; SpawnYaw = yaw;
            var rot = Quaternion.Euler(0f, yaw, 0f);
            Transform[] points = null;
            if (KitCatalog.TryInvoke("LevelKit", "SpawnPoints", out var r, Gameplay, position, rot)) points = ToTransforms(r);
            if (points == null || points.Length < 3)
            {
                var holder = Shifters.Kit.Make("StartSpawns", Gameplay, position, rot).transform;
                points = new Transform[3];
                for (int i = 0; i < 3; i++)
                    points[i] = Shifters.Kit.Make("Spawn_" + i, holder, new Vector3((i - 1) * 1.6f, 0.05f, 0f)).transform;
            }
            SpawnPoints = points;
            var markTarget = points[0].parent != null && points[0].parent != Gameplay ? points[0].parent.gameObject : points[0].gameObject;
            Mark("spawn", markTarget);
            return points;
        }

        static Transform[] ToTransforms(object r)
        {
            if (r is Transform[] arr) return arr;
            if (r is IEnumerable<Transform> en) return new List<Transform>(en).ToArray();
            Transform holder = r is Transform t ? t : r is GameObject g ? g.transform : r is Component c ? c.transform : null;
            if (holder == null) return null;
            var list = new List<Transform>();
            foreach (Transform child in holder) list.Add(child);
            return list.Count > 0 ? list.ToArray() : new[] { holder };
        }

        /// <summary>Checkpoint (LevelKit.Checkpoint). index &lt; 0 = auto (previous + 1, first = 1, matching C1..Cn).
        /// Marked "checkpoint.&lt;index&gt;". Needs a clear flat 6x6 m area.</summary>
        public GameObject Checkpoint(Vector3 position, float yaw, int index = -1, Transform parent = null)
        {
            if (index < 0) index = LastCheckpointIndex + 1;
            if (index <= LastCheckpointIndex) Warn("checkpoint index " + index + " is not ascending (last " + LastCheckpointIndex + ")");
            LastCheckpointIndex = Mathf.Max(LastCheckpointIndex, index);
            CheckpointCount++;
            parent = parent != null ? parent : Gameplay;
            var rot = Quaternion.Euler(0f, yaw, 0f);
            var go = KitCatalog.Spawn("LevelKit", "Checkpoint", parent, position, rot, index);
            if (go == null)
            {
                go = Placeholder(parent, "Checkpoint_" + index + " (placeholder)", position, rot, MatKey.LightOn, new Vector3(0.25f, 3.2f, 0.25f));
                for (int i = 0; i < 3; i++) Shifters.Kit.Make("Spawn_" + i, go.transform, new Vector3((i - 1) * 1.6f, 0.05f, 1.5f));
            }
            Mark("checkpoint." + index, go);
            return go;
        }

        /// <summary>Level exit (LevelKit.Exit). Marked "exit".</summary>
        public GameObject Exit(Vector3 position, float yaw, Transform parent = null)
        {
            ExitCount++;
            parent = parent != null ? parent : Gameplay;
            var rot = Quaternion.Euler(0f, yaw, 0f);
            var go = KitCatalog.Spawn("LevelKit", "Exit", parent, position, rot);
            if (go == null)
            {
                go = Placeholder(parent, "Exit (placeholder)", position, rot, MatKey.SafetyOrange, new Vector3(4f, 0.3f, 4f));
                Shifters.Kit.BoxVisual(go.transform, "SignBar", new Vector3(0f, 4.2f, 2f), new Vector3(3.2f, 0.9f, 0.2f), MatKey.SafetyOrange);
                Label(go.transform, new Vector3(0f, 4.2f, 1.85f), Quaternion.identity, "EXIT", 0.6f, Color.white, 3f);
            }
            Mark(ExitCount == 1 ? "exit" : "exit." + ExitCount, go);
            return go;
        }

        /// <summary>Tutorial trigger (LevelKit.Tutorial): fires when the ACTIVE character enters. Marked "tutorial.&lt;id&gt;".</summary>
        public GameObject Tutorial(Vector3 center, Vector3 size, string id, string title, string body, string[] keys = null, bool important = false, Transform parent = null)
        {
            TutorialCount++;
            parent = parent != null ? parent : Gameplay;
            var go = KitCatalog.Spawn("LevelKit", "Tutorial", parent, center, size, id, title, body, keys ?? new string[0], important);
            if (go == null) go = Shifters.Kit.Make("Tutorial_" + id + " (placeholder)", parent, center);
            Mark("tutorial." + id, go);
            return go;
        }

        /// <summary>Hint zone (LevelKit.HintZone): H gives realHint to IQ>=7, vagueHint to IQ 4-6. Marked "hint.&lt;n&gt;".</summary>
        public GameObject Hint(Vector3 center, Vector3 size, string realHint, string vagueHint, Transform parent = null)
        {
            HintCount++;
            parent = parent != null ? parent : Gameplay;
            var go = KitCatalog.Spawn("LevelKit", "HintZone", parent, center, size, realHint, vagueHint);
            if (go == null) go = Shifters.Kit.Make("Hint_" + HintCount + " (placeholder)", parent, center);
            Mark("hint." + HintCount, go);
            return go;
        }

        GameObject Placeholder(Transform parent, string name, Vector3 pos, Quaternion rot, MatKey mat, Vector3 size)
        {
            Warn(name + ": LevelKit factory missing - placed a visual placeholder (rebuild once LevelKit is published)");
            var go = Shifters.Kit.Make(name, parent, pos, rot);
            Shifters.Kit.BoxVisual(go.transform, "Visual", new Vector3(0f, size.y * 0.5f, 0f), size, mat, 0.03f);
            return go;
        }

        // ------------------------------------------------------------------ kits
        /// <summary>Calls any kit factory by name with loosely typed args (see KitCatalog.TryInvoke). Returns the built
        /// GameObject or null (warning logged once). Prefer calling kits directly once they exist.</summary>
        public GameObject CallKit(string kit, string method, params object[] args)
        {
            var go = KitCatalog.Spawn(kit, method, args);
            if (go != null) Count(kit + "." + method);
            return go;
        }

        // ------------------------------------------------------------------ marks
        /// <summary>Adds a LevelMark with id to go (solution bots: LevelMark.Find(id)). Returns go.</summary>
        public GameObject Mark(string id, GameObject go)
        {
            if (go == null) { Warn("Mark('" + id + "') on a null object"); return null; }
            if (_marks.TryGetValue(id, out var prev) && prev != null && prev != go) Warn("duplicate mark id '" + id + "'");
            else _marks[id] = go;
            LevelMark.Add(go, id);
            return go;
        }

        /// <summary>Marks a component's GameObject and returns the component (fluent).</summary>
        public T Mark<T>(string id, T component) where T : Component
        {
            if (component == null) { Warn("Mark('" + id + "') on a null component"); return null; }
            Mark(id, component.gameObject);
            return component;
        }

        /// <summary>Empty marker transform at a point (e.g. "s3.bridgeTakeoff"). position is local to parent (default MarkRoot).</summary>
        public Transform MarkPoint(string id, Vector3 position, float yaw = 0f, Transform parent = null)
        {
            var go = Shifters.Kit.Make("Mark_" + id, parent != null ? parent : MarkRoot, position, Quaternion.Euler(0f, yaw, 0f));
            Mark(id, go);
            return go.transform;
        }

        // ------------------------------------------------------------------ bookkeeping
        public void Warn(string message)
        {
            Warnings.Add(message);
            Debug.LogWarning("[LevelBuild " + Info.SceneName + "] " + message);
        }

        public void Count(string category, int n = 1)
        {
            Counts.TryGetValue(category, out int c);
            Counts[category] = c + n;
        }

        /// <summary>Called by the assembler after Build: guarantees a spawn.</summary>
        internal void FinishBuild()
        {
            if (!HasSpawn) { Warn("builder never called SetSpawn - spawning at the origin"); SetSpawn(Vector3.zero, 0f); }
            if (IsCatalogLevel && ExitCount == 0) Warn("level has no Exit");
            if (IsCatalogLevel && CheckpointCount == 0) Warn("level has no checkpoints");
        }

        /// <summary>Human readable build summary (objects per root, marks, gameplay counts, warnings).</summary>
        public string Report()
        {
            var sb = new StringBuilder();
            sb.Append(Info.SceneName).Append(" (builder ").Append(Builder != null ? Builder.GetType().Name : "?").Append(", index ").Append(LevelIndex)
              .Append(", theme ").Append(Theme).Append(", ").Append(BuildSeconds.ToString("0.00")).Append(" s)\n");
            foreach (var r in new[] { Geometry, Puzzles, Props, Hazards, Enemies, Decor, Gameplay, Lights })
                if (r != null) sb.Append("  ").Append(r.name).Append(": ").Append(r.GetComponentsInChildren<Transform>(true).Length - 1).Append(" objects\n");
            sb.Append("  spawns ").Append(SpawnPoints.Length).Append(", checkpoints ").Append(CheckpointCount).Append(", exits ").Append(ExitCount)
              .Append(", tutorials ").Append(TutorialCount).Append(", hints ").Append(HintCount).Append(", marks ").Append(_marks.Count).Append('\n');
            if (Counts.Count > 0)
            {
                sb.Append("  counts:");
                var keys = new List<string>(Counts.Keys); keys.Sort(StringComparer.Ordinal);
                foreach (var k in keys) sb.Append(' ').Append(k).Append('=').Append(Counts[k]);
                sb.Append('\n');
            }
            foreach (var w in Warnings) sb.Append("  WARNING: ").Append(w).Append('\n');
            return sb.ToString();
        }

        internal static string Sanitize(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return "x";
            var sb = new StringBuilder();
            foreach (char ch in s) { if (char.IsLetterOrDigit(ch)) sb.Append(ch); else if (sb.Length > 0 && sb[sb.Length - 1] != '_') sb.Append('_'); if (sb.Length >= max) break; }
            return sb.Length > 0 ? sb.ToString() : "x";
        }
    }
}
