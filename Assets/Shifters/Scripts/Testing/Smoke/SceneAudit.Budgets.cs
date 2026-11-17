// =====================================================================================
//  SHIFTERS - SceneAudit.Budgets.cs   [BOTS module - WebGL budgets, CAMPAIGN §21]
//  SceneAudit.MeasureBudgets() measures the loaded scene against the §21 HARD CAPS (a breach fails the smoke test):
//    active rigidbodies <= 40 (movers included) | non-sun lights ON after LightBudget's WebGL/Mobile pass <= 10
//    (the raw Light component count is reported too) | enabled point/spot lights within 25 m of the camera <= 4
//    (low-end pass; worst of the camera + every checkpoint + exit position) | renderers <= 2,200 | active enemies <= 4
//    (SecurityBot incl. guards, StalkerBrain, boss minions; dormant ones excluded) | cameras rendering to screen <= 1.
//  The low-end measurement forces LightBudget.ForceLowEnd = true for one pass and restores the previous state.
//  Persistent (DontDestroyOnLoad) systems, PortraitStudio / VillainStudio rigs and render-texture cameras are excluded.
// =====================================================================================
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Shifters
{
    public static partial class SceneAudit
    {
        public const int CapRigidbodies = 40, CapLights = 10, CapNearLights = 4, CapRenderers = 2200, CapEnemies = 4, CapCameras = 1;
        public const int TargetRigidbodies = 25, TargetLights = 6, TargetNearLights = 3, TargetRenderers = 1600, TargetEnemies = 3;
        public const float NearLightRadius = 25f;

        public sealed class BudgetReport
        {
            public string Scene;
            public int Rigidbodies, LightsRaw, LightsRawEnabled, LightsLowEnd, NearLightsLowEnd, NearLightsRaw, Renderers, Enemies, Cameras, RtCameras;
            public string NearWorstAt = "camera", LowEndInfo = "";
            public readonly List<string> Breaches = new List<string>();
            public readonly List<string> OverTarget = new List<string>();
            public bool Ok => Breaches.Count == 0;

            public string Line()
            {
                var sb = new StringBuilder();
                sb.Append("BUDGET ").Append(Scene).Append(": rigidbodies ").Append(Rigidbodies).Append('/').Append(CapRigidbodies)
                  .Append(" | lights(low-end on) ").Append(LightsLowEnd).Append('/').Append(CapLights)
                  .Append(" raw ").Append(LightsRaw).Append(" (").Append(LightsRawEnabled).Append(" enabled)")
                  .Append(" | near-25m ").Append(NearLightsLowEnd).Append('/').Append(CapNearLights).Append(" @").Append(NearWorstAt)
                  .Append(" raw ").Append(NearLightsRaw)
                  .Append(" | renderers ").Append(Renderers).Append('/').Append(CapRenderers)
                  .Append(" | enemies ").Append(Enemies).Append('/').Append(CapEnemies)
                  .Append(" | cameras ").Append(Cameras).Append('/').Append(CapCameras).Append(" (+").Append(RtCameras).Append(" RT)")
                  .Append(Ok ? " -> OK" : " -> BREACH: " + string.Join("; ", Breaches));
                if (OverTarget.Count > 0) sb.Append(" | over target: ").Append(string.Join(", ", OverTarget));
                return sb.ToString();
            }
        }

        static bool InScene(GameObject go) => go != null && go.scene.IsValid() && go.scene.name != "DontDestroyOnLoad";

        static bool IsStudio(Component c) =>
            c.GetComponentInParent<PortraitStudio>() != null || BotReflect.FindComponentInParent(c, "VillainStudio") != null;

        /// <summary>Non-sun (point/spot) scene lights: (all components, enabled ones).</summary>
        public static void CountLights(out int raw, out int enabled)
        {
            raw = 0; enabled = 0;
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include))
            {
                if (l == null || !InScene(l.gameObject) || IsStudio(l)) continue;
                if (l.type != LightType.Point && l.type != LightType.Spot) continue;
                raw++;
                if (l.isActiveAndEnabled && l.intensity > 0f) enabled++;
            }
        }

        public static int LightsNear(Vector3 p, float radius)
        {
            int n = 0;
            foreach (var l in Object.FindObjectsByType<Light>())
            {
                if (l == null || !l.isActiveAndEnabled || l.intensity <= 0f || !InScene(l.gameObject) || IsStudio(l)) continue;
                if (l.type != LightType.Point && l.type != LightType.Spot) continue;
                if ((l.transform.position - p).sqrMagnitude <= radius * radius) n++;
            }
            return n;
        }

        static int WorstNear(Vector3 cam, out string where)
        {
            where = "camera";
            int worst = LightsNear(cam, NearLightRadius);
            foreach (var cp in Checkpoint.All)
            {
                if (cp == null) continue;
                int n = LightsNear(cp.transform.position + Vector3.up * 3f, NearLightRadius);
                if (n > worst) { worst = n; where = "checkpoint " + cp.Index; }
            }
            foreach (var ex in LevelExit.All)
            {
                if (ex == null) continue;
                int n = LightsNear(ex.transform.position + Vector3.up * 3f, NearLightRadius);
                if (n > worst) { worst = n; where = "exit"; }
            }
            return worst;
        }

        public static int SceneRenderers()
        {
            int n = 0;
            foreach (var r in Object.FindObjectsByType<Renderer>())
                if (r != null && r.enabled && r.gameObject.activeInHierarchy && InScene(r.gameObject) && !IsStudio(r)) n++;
            return n;
        }

        public static int SceneRigidbodies()
        {
            int n = 0;
            foreach (var rb in Object.FindObjectsByType<Rigidbody>())
                if (rb != null && rb.gameObject.activeInHierarchy && InScene(rb.gameObject) && !IsStudio(rb)) n++;
            return n;
        }

        static readonly string[] EnemyTypes = { "SecurityBot", "StalkerBrain", "BossMinion", "MinionBrain" };

        /// <summary>Awake enemies: SecurityBot (guards are bots), StalkerBrain, boss minions - enabled, active and not
        /// Dormant / IsDormant / Asleep.</summary>
        public static int ActiveEnemies()
        {
            var seen = new HashSet<GameObject>();
            foreach (var tn in EnemyTypes)
                foreach (var mb in BotReflect.FindAll(tn))
                {
                    if (mb == null || !mb.isActiveAndEnabled || !InScene(mb.gameObject)) continue;
                    if ((BotReflect.TryGet(mb, "IsDormant", out bool d1) && d1) || (BotReflect.TryGet(mb, "Dormant", out bool d2) && d2)
                        || (BotReflect.TryGet(mb, "Asleep", out bool d3) && d3)) continue;
                    seen.Add(mb.gameObject);
                }
            return seen.Count;
        }

        public static void CountCameras(out int screen, out int rt)
        {
            screen = 0; rt = 0;
            foreach (var c in Camera.allCameras)
            {
                if (c == null || !c.isActiveAndEnabled) continue;
                if (c.targetTexture != null) rt++; else screen++;
            }
        }

        /// <summary>Measure the loaded scene against the §21 caps (runs one forced low-end LightBudget pass and restores).</summary>
        public static BudgetReport MeasureBudgets(string sceneName = null)
        {
            var r = new BudgetReport { Scene = string.IsNullOrEmpty(sceneName) ? UnityEngine.SceneManagement.SceneManager.GetActiveScene().name : sceneName };
            r.Rigidbodies = SceneRigidbodies();
            r.Renderers = SceneRenderers();
            r.Enemies = ActiveEnemies();
            CountCameras(out r.Cameras, out r.RtCameras);
            CountLights(out r.LightsRaw, out r.LightsRawEnabled);
            var cam = Camera.main;
            Vector3 cp = cam != null ? cam.transform.position : Vector3.zero;
            r.NearLightsRaw = WorstNear(cp, out _);

            var prev = LightBudget.ForceLowEnd;
            try
            {
                LightBudget.ForceLowEnd = true;
                LightBudget.Apply();
                var st = LightBudget.Last;
                r.LowEndInfo = st.ToString();
                CountLights(out _, out r.LightsLowEnd);
                r.NearLightsLowEnd = WorstNear(cp, out r.NearWorstAt);
            }
            finally
            {
                LightBudget.ForceLowEnd = prev;
                LightBudget.Apply();
            }

            void Cap(string what, int v, int cap, int target)
            {
                if (v > cap) r.Breaches.Add(what + " " + v + " > " + cap);
                else if (target > 0 && v > target) r.OverTarget.Add(what + " " + v + " > target " + target);
            }
            Cap("rigidbodies", r.Rigidbodies, CapRigidbodies, TargetRigidbodies);
            Cap("lights (WebGL/Mobile, after LightBudget)", r.LightsLowEnd, CapLights, TargetLights);
            Cap("point/spot within 25 m (" + r.NearWorstAt + ")", r.NearLightsLowEnd, CapNearLights, TargetNearLights);
            Cap("renderers", r.Renderers, CapRenderers, TargetRenderers);
            Cap("active enemies", r.Enemies, CapEnemies, TargetEnemies);
            Cap("screen cameras", r.Cameras, CapCameras, 0);
            return r;
        }
    }
}
