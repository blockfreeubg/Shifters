// SHIFTERS - DevPlay: per-theme environment showcase (EnvKit + MaterialLibrary + LevelLighting + PostFx), then the
// shader / Fx sheets.
//   devplay.sh <clone> rshow -devSetup Shifters.RenderingShowcaseTests.RunAll -devSize 1280x720
//   devplay.sh <clone> rshow -devSetup Shifters.RenderingShowcaseTests.Themes      (showcase only)
using System.Collections;
using UnityEngine;

namespace Shifters
{
    public static class RenderingShowcaseTests
    {
        static readonly LevelTheme[] AllThemes = { LevelTheme.Training, LevelTheme.Factory, LevelTheme.Lab, LevelTheme.Research, LevelTheme.Chaos, LevelTheme.Menu };

        public static IEnumerator RunAll(DevPlayContext ctx)
        {
            yield return Themes(ctx);
            yield return MobilePipeline(ctx);
            yield return RenderingSheetTests.Run(ctx);
        }

        /// <summary>WebGL uses the Mobile quality level (Mobile_RPAsset: plain Forward, no SSAO, 1 cascade, hard shadows).
        /// That level is excluded for Standalone, so the editor refuses SetQualityLevel(0): instead the Mobile pipeline asset
        /// is assigned as the active quality level's pipeline override (editor only) and three themes are rendered.</summary>
        public static IEnumerator MobilePipeline(DevPlayContext ctx)
        {
#if UNITY_EDITOR
            UnityEditor.ShaderUtil.allowAsyncCompilation = false;
            var mobile = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.RenderPipelineAsset>("Assets/Settings/Mobile_RPAsset.asset");
#else
            UnityEngine.Rendering.RenderPipelineAsset mobile = null;
#endif
            ctx.Check(mobile != null, "Mobile_RPAsset found");
            if (mobile == null) yield break;
            var prev = QualitySettings.renderPipeline;
            QualitySettings.renderPipeline = mobile;
            yield return null;
            var active = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
            ctx.Log("active pipeline: " + (active != null ? active.name : "null"));
            ctx.Check(active == mobile, "Mobile pipeline active");
            var cam = Camera.main;
            yield return WarmUp(ctx);
            foreach (var theme in new[] { LevelTheme.Training, LevelTheme.Lab, LevelTheme.Chaos })
            {
                var root = BuildShowcase(theme, out _, out _);
                RenderTestUtil.Place(cam, new Vector3(0.6f, 3.9f, -10.2f), new Vector3(0.2f, 1.3f, 1.5f), 55f);
                yield return ctx.Wait(0.3f);
                string tn = theme.ToString().ToLowerInvariant();
                var shot = ctx.Shot("mobile_" + tn + "_game");
                float l = RenderTestUtil.MeanLuma(shot);
                ctx.Log(tn + " mobile luma=" + l.ToString("0.00"));
                ctx.Check(RenderTestUtil.MagentaFraction(shot) < 0.001f && l > 0.06f && l < 0.93f, tn + ": Mobile pipeline renders (no magenta, sane exposure)");
                Object.Destroy(root);
                yield return null;
            }
            QualitySettings.renderPipeline = prev;
            yield return null;
        }

        public static IEnumerator Themes(DevPlayContext ctx)
        {
#if UNITY_EDITOR
            // the editor renders a placeholder while shader variants compile asynchronously (players precompile):
            // compile synchronously so the very first showcase frame is representative
            UnityEditor.ShaderUtil.allowAsyncCompilation = false;
#endif
            // baked assets load at runtime (MaterialBaker output in Resources/Materials)
            var baked = Resources.Load<Material>("Materials/Training_Floor");
            ctx.Check(baked != null && baked.shader != null && baked.shader.name == MaterialLibrary.LitShaderName, "baked Training_Floor loads from Resources with StylizedLit");
            MaterialLibrary.ClearCache();
            ctx.Check(baked == null || MaterialLibrary.Get(MatKey.Floor, LevelTheme.Training) == baked, "MaterialLibrary.Get returns the baked asset");
            ctx.Check(Resources.Load<Material>("Materials/Sky_Lab") != null && LevelLighting.SkyMaterial(LevelTheme.Lab) == Resources.Load<Material>("Materials/Sky_Lab"), "baked sky used");

            var cam = Camera.main;
            yield return WarmUp(ctx);
            foreach (var theme in AllThemes)
            {
                var root = BuildShowcase(theme, out var signText, out var signViewRot);
                yield return ctx.Wait(0.35f);

                string tn = theme.ToString().ToLowerInvariant();
                // ---- colliders where players walk / bump
                ctx.Check(HitY(new Vector3(-1f, 6f, -1.2f), out float fy) && Mathf.Abs(fy) < 0.02f, tn + ": floor collider top at y=0 (" + fy.ToString("0.00") + ")");
                ctx.Check(HitY(new Vector3(12.5f, 6f, 2f), out float py) && Mathf.Abs(py) < 0.02f, tn + ": platform collider top at y=0 (" + py.ToString("0.00") + ")");
                ctx.Check(HitY(new Vector3(-5.5f, 6f, 0.7f), out float sy) && sy > 0.6f && sy < 1.9f, tn + ": stairs walkable (" + sy.ToString("0.00") + ")");
                ctx.Check(HitY(new Vector3(3f, 6f, 0.25f), out float ry) && ry > 0.4f && ry < 1.1f, tn + ": ramp collider (" + ry.ToString("0.00") + ")");
                ctx.Check(Physics.Raycast(new Vector3(-6.5f, 2.6f, 4f), Vector3.right, 4f, ~0, QueryTriggerInteraction.Ignore), tn + ": mezzanine railing collider");
                ctx.Check(Physics.Raycast(new Vector3(0f, 2f, 4f), Vector3.forward, 6f, ~0, QueryTriggerInteraction.Ignore), tn + ": back wall collider");
                // ---- no default / missing materials
                int badMats = 0, renderers = 0;
                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                {
                    renderers++;
                    var m = r.sharedMaterial;
                    if (m == null || m.shader == null || m.name.StartsWith("Default-") || m.shader.name.Contains("InternalError")) { badMats++; ctx.Log("bad material on " + r.name); }
                }
                ctx.Check(badMats == 0, tn + ": no missing/default materials (" + renderers + " renderers)");
                // ---- the 4-arg Sign (builder path) is readable by a viewer looking along rot*forward
                var tmp = signText;
                ctx.Check(tmp != null && Vector3.Dot(tmp.transform.forward, signViewRot * Vector3.forward) > 0.99f, tn + ": sign text faces the viewer (mounted-piece convention)");

                // ---- gameplay camera (~6 m behind/above the team) and wide establishing shot
                RenderTestUtil.Place(cam, new Vector3(0.6f, 3.9f, -10.2f), new Vector3(0.2f, 1.3f, 1.5f), 55f);
                yield return ctx.Wait(0.1f);
                var g = ctx.Shot("theme_" + tn + "_game");
                RenderTestUtil.Place(cam, new Vector3(-21f, 13f, -25f), new Vector3(3f, 0.5f, 2f), 52f);
                yield return ctx.Wait(0.1f);
                var w = ctx.Shot("theme_" + tn + "_wide");
                float lg = RenderTestUtil.MeanLuma(g), lw = RenderTestUtil.MeanLuma(w);
                ctx.Log(tn + " luma game=" + lg.ToString("0.00") + " wide=" + lw.ToString("0.00") + " renderers=" + renderers + " lights=" + root.GetComponentsInChildren<Light>().Length);
                ctx.Check(RenderTestUtil.MagentaFraction(g) < 0.001f && RenderTestUtil.MagentaFraction(w) < 0.001f, tn + ": no magenta");
                ctx.Check(lg > 0.06f && lg < 0.93f && lw > 0.04f && lw < 0.93f, tn + ": exposure sane");
                if (theme == LevelTheme.Factory)
                {
                    RenderTestUtil.Place(cam, new Vector3(9.5f, -4.5f, -6.5f), new Vector3(12.5f, -0.6f, 2f), 55f);
                    yield return ctx.Wait(0.05f);
                    ctx.Shot("theme_factory_platform_under");
                }
                Object.Destroy(root);
                yield return null;
            }
        }

        /// <summary>The editor shows untinted placeholder materials for the first frames after the first scene build (and after
        /// a pipeline switch) while variants load - players are unaffected. Build + render once before the first screenshot.</summary>
        static IEnumerator WarmUp(DevPlayContext ctx)
        {
            var warm = BuildShowcase(LevelTheme.Training, out _, out _);
            RenderTestUtil.Place(Camera.main, new Vector3(0.6f, 3.9f, -10.2f), new Vector3(0.2f, 1.3f, 1.5f), 55f);
            yield return ctx.Wait(1f);
            Object.Destroy(warm);
            yield return null;
        }

        static bool HitY(Vector3 from, out float y)
        {
            y = float.NaN;
            if (!Physics.Raycast(from, Vector3.down, out var hit, 20f, ~0, QueryTriggerInteraction.Ignore)) return false;
            y = hit.point.y;
            return true;
        }

        /// <summary>Builds the themed showcase (also handy for manual inspection). Returns the root.</summary>
        public static GameObject BuildShowcase(LevelTheme theme, out TMPro.TextMeshPro builderSignText, out Quaternion builderSignViewRot)
        {
            Kit.Theme = theme;
            LevelLighting.Apply(theme);
            PostFx.SetTheme(theme);
            var rootGo = new GameObject("Showcase_" + theme);
            var root = rootGo.transform;
            var accent = LevelLighting.AccentColor(theme);

            // ---- floors around an acid / void pit (x 3..6, z -6.2..-3.8)
            EnvKit.Floor(root, new Vector3(0f, 0f, 2.1f), new Vector3(16f, 1f, 11.8f));
            EnvKit.Floor(root, new Vector3(0f, 0f, -7.1f), new Vector3(16f, 1f, 1.8f));
            EnvKit.Floor(root, new Vector3(-2.5f, 0f, -5f), new Vector3(11f, 1f, 2.4f), MatKey.Floor, false);
            EnvKit.Floor(root, new Vector3(7f, 0f, -5f), new Vector3(2f, 1f, 2.4f), MatKey.Floor, false);
            if (theme == LevelTheme.Lab || theme == LevelTheme.Factory) EnvKit.AcidPool(root, new Vector3(4.5f, 0f, -5f), new Vector3(3f, 1.2f, 2.4f));
            else EnvKit.VoidPit(root, new Vector3(4.5f, 0f, -5f), new Vector3(3f, 6f, 2.4f));

            // ---- walls, signage, lights
            EnvKit.Wall(root, new Vector3(-8f, 0f, 8.2f), new Vector3(8f, 0f, 8.2f), 4.5f);
            EnvKit.Wall(root, new Vector3(-8.2f, 0f, -8f), new Vector3(-8.2f, 0f, 8f), 3f, 0.4f, MatKey.WallAlt);
            string headline = theme switch
            {
                LevelTheme.Factory => "MIND THE GEARS",
                LevelTheme.Lab => "DO NOT FEED THE ROBOTS",
                LevelTheme.Research => "THINK HARDER",
                LevelTheme.Chaos => "EVERYTHING IS FINE",
                LevelTheme.Menu => "SHIFTERS INC.",
                _ => "WELCOME, INTERNS",
            };
            builderSignViewRot = Quaternion.identity;
            var builderSign = EnvKit.Sign(root, new Vector3(-2.6f, 2.7f, 7.96f), builderSignViewRot, headline); // 4-arg (builder) overload
            builderSignText = builderSign.GetComponentInChildren<TMPro.TextMeshPro>();
            EnvKit.Sign(root, new Vector3(5.6f, 3.2f, 7.96f), Quaternion.identity, "EXIT >", new Vector2(2.2f, 0.7f), SignStyle.Arrow);
            EnvKit.Sign(root, new Vector3(5.6f, 2.3f, 7.96f), Quaternion.identity, "CAUTION: WET FLOOR", new Vector2(2.4f, 0.6f), SignStyle.Warning);
            EnvKit.Poster(root, new Vector3(2.4f, 2.1f, 7.98f), Quaternion.identity, "QUOTA: 0 / 1000", "KEEP SHIFTING!");
            EnvKit.Decor.MotivationalPoster(root, new Vector3(-7.98f, 1.7f, -3f), Quaternion.Euler(0f, -90f, 0f), 0);
            var panel = EnvKit.LightPanel(root, new Vector3(0f, 3.9f, 7.96f), Quaternion.identity, new Vector2(2.4f, 0.35f), Color.Lerp(Color.white, accent, 0.35f), true, 2.5f, 9f); // one real light
            if (theme == LevelTheme.Lab) EnvKit.AddFlicker(panel, 0.6f);
            EnvKit.LightPanel(root, new Vector3(-7.96f, 2.4f, 4f), Quaternion.Euler(0f, -90f, 0f), new Vector2(1.6f, 0.3f), UIColors.Hex(0x3DE0FF), false);
            EnvKit.Pipe(root, new[] { new Vector3(-7.7f, 0.35f, -7f), new Vector3(-7.7f, 0.35f, 7.5f), new Vector3(-7.7f, 3.6f, 7.5f) }, 0.14f);
            EnvKit.CableTray(root, new Vector3(-7f, 4.15f, 7.75f), new Vector3(7f, 4.15f, 7.75f));
            EnvKit.HazardStripe(root, new Vector3(7.4f, 0.006f, 2f), new Vector3(1.1f, 0.012f, 5f));

            // ---- stairs -> mezzanine (pillars, railing), ramp -> ledge
            EnvKit.Stairs(root, new Vector3(-5.5f, 0f, -2f), Vector3.forward, 2.2f, 0.25f, 0.45f, 8);
            EnvKit.Floor(root, new Vector3(-5.5f, 2f, 4.2f), new Vector3(3.6f, 0.5f, 5.2f), MatKey.FloorAlt);
            EnvKit.Pillar(root, new Vector3(-3.9f, 0f, 3f), 1.5f, 0.5f);
            EnvKit.Pillar(root, new Vector3(-3.9f, 0f, 6.2f), 1.5f, 0.5f);
            EnvKit.Railing(root, new Vector3(-3.85f, 2f, 1.8f), new Vector3(-3.85f, 2f, 6.7f));
            EnvKit.Ramp(root, new Vector3(3f, 0f, -2f), new Vector3(3f, 1.5f, 2.5f), 2.2f);
            EnvKit.Floor(root, new Vector3(3f, 1.5f, 4.5f), new Vector3(2.2f, 1.5f, 4f), MatKey.Concrete);
            EnvKit.Railing(root, new Vector3(8f, 0f, -8f), new Vector3(8f, 0f, -0.6f));
            EnvKit.Platform(root, new Vector3(12.5f, 0f, 2f), new Vector3(5f, 1.4f, 5f), true);

            // ---- puzzle language: teal bodies + indicator lights, props, team stand-ins
            for (int i = 0; i < 2; i++)
            {
                var b = Kit.SolidBox(root, "PuzzleBox" + i, new Vector3(-1.6f + i * 2.2f, 0.45f, 2.6f), new Vector3(0.9f, 0.9f, 0.9f), MatKey.PuzzleBody, 0.06f);
                Kit.BoxVisual(b.transform, "Light", new Vector3(0f, 0.47f, 0f), new Vector3(0.3f, 0.05f, 0.3f), i == 0 ? MatKey.LightOn : MatKey.LightOff, 0.01f, null, false);
            }
            Kit.SolidBox(root, "Plate", new Vector3(-0.5f, 0.03f, 0.5f), new Vector3(1.4f, 0.06f, 1.4f), MatKey.PuzzleAccent, 0.02f);
            Kit.SolidBox(root, "Crate", new Vector3(1.4f, 0.4f, 0.2f), new Vector3(0.8f, 0.8f, 0.8f), MatKey.Wood, 0.04f);
            Kit.SolidBox(root, "Heavy", new Vector3(-3.2f, 0.6f, -0.2f), new Vector3(1.2f, 1.2f, 1.2f), MatKey.CrateHeavy, 0.06f);
            EnvKit.HazardStripe(root, new Vector3(-3.2f, 0.6f, -0.81f), new Vector3(1.2f, 0.25f, 0.02f));
            Color[] team = { UIColors.Hex(0xFF6B3D), UIColors.Hex(0x3DA5FF), UIColors.Hex(0xB07CFF) };
            for (int i = 0; i < 3; i++)
                ProcMesh.Part("Teammate" + i, root, ProcMesh.Capsule(0.38f + (i == 0 ? 0.1f : 0f), 1.7f, 16, 5), MaterialLibrary.Colored(team[i], 0.35f), new Vector3(-0.9f + i * 1.3f, 0.85f, -3.1f));
            var pl = new GameObject("TealLight").AddComponent<Light>();
            pl.transform.SetParent(root, false); pl.transform.localPosition = new Vector3(-0.5f, 1.4f, 2f);
            pl.type = LightType.Point; pl.color = UIColors.Hex(0x2EC4B6); pl.range = 4.5f; pl.intensity = 3f;

            // ---- theme decor
            var d = root;
            switch (theme)
            {
                case LevelTheme.Training:
                    EnvKit.Decor.Cone(d, new Vector3(6.6f, 0f, -1.5f)); EnvKit.Decor.Cone(d, new Vector3(7.2f, 0f, -0.9f)); EnvKit.Decor.Cone(d, new Vector3(6.4f, 0f, 0.1f));
                    EnvKit.Decor.TrainingDummy(d, new Vector3(5.5f, 0f, 4.5f), Quaternion.Euler(0f, 200f, 0f));
                    EnvKit.Decor.Bench(d, new Vector3(-1f, 0f, 6.8f), Quaternion.identity);
                    EnvKit.Decor.Plant(d, new Vector3(-2.6f, 0f, 7.2f), 0); EnvKit.Decor.Plant(d, new Vector3(0.8f, 0f, 7.2f), 1);
                    EnvKit.Decor.Whiteboard(d, new Vector3(6.2f, 0f, 6.6f), Quaternion.Euler(0f, 20f, 0f));
                    break;
                case LevelTheme.Factory:
                    EnvKit.Decor.Machine(d, new Vector3(5.6f, 0f, 5.6f), Quaternion.Euler(0f, 180f, 0f), new Vector3(2.4f, 2.2f, 1.6f));
                    EnvKit.Decor.Gear(d, new Vector3(-1.5f, 3.2f, 7.95f), Quaternion.identity, 0.9f);
                    EnvKit.Decor.ConveyorStrip(d, new Vector3(-2f, 0f, 5.8f), new Vector3(2f, 0f, 5.8f));
                    EnvKit.Decor.Barrel(d, new Vector3(6.6f, 0f, -1.4f), 0); EnvKit.Decor.Barrel(d, new Vector3(7.2f, 0f, -0.6f), 1); EnvKit.Decor.Barrel(d, new Vector3(6.5f, 0f, 0.2f), 2);
                    EnvKit.Decor.CrateStack(d, new Vector3(1.5f, 0f, 6.5f), Quaternion.Euler(0f, 15f, 0f), 3, 4);
                    EnvKit.Decor.Chain(d, new Vector3(0.5f, 4.4f, 6f), 2.4f);
                    EnvKit.Decor.SteamStack(d, new Vector3(7f, 0f, 3.6f), 3.2f);
                    break;
                case LevelTheme.Lab:
                    EnvKit.Decor.Tank(d, new Vector3(6.3f, 0f, 5.8f)); EnvKit.Decor.Tank(d, new Vector3(4.9f, 0f, 6.6f), 2.6f, 0.5f);
                    EnvKit.Decor.DeskWithMonitors(d, new Vector3(-0.6f, 0f, 6.6f), Quaternion.identity);
                    EnvKit.Decor.BrokenTiles(d, new Vector3(1.8f, 0f, -1.2f), new Vector2(2.5f, 2f));
                    EnvKit.Decor.Cable(d, new Vector3(-7.6f, 3f, 1f), new Vector3(-3.9f, 2.1f, 2f), 0.6f, 1);
                    EnvKit.Decor.Plant(d, new Vector3(6.8f, 0f, -1f), 2);
                    break;
                case LevelTheme.Research:
                    EnvKit.Decor.FloatingShape(d, new Vector3(5.5f, 3.2f, 4.5f), 1.1f, 0); EnvKit.Decor.FloatingShape(d, new Vector3(1.2f, 4.6f, 5.5f), 0.8f, 1);
                    EnvKit.Decor.FloatingShape(d, new Vector3(9.5f, 3.6f, -2f), 1.3f, 2); EnvKit.Decor.FloatingShape(d, new Vector3(-1.5f, 5.2f, 3f), 0.7f, 3);
                    EnvKit.Decor.HoloPanel(d, new Vector3(5.8f, 0f, 1.2f), Quaternion.Euler(0f, 30f, 0f), new Vector2(1.6f, 1f), "IQ CHECK:\nPENDING");
                    EnvKit.Decor.BrainJar(d, new Vector3(-0.8f, 0f, 6.3f));
                    EnvKit.Decor.GridPillar(d, new Vector3(6.9f, 0f, 6.9f)); EnvKit.Decor.GridPillar(d, new Vector3(-7.2f, 0f, -7.2f), 3.5f);
                    break;
                case LevelTheme.Chaos:
                    EnvKit.Decor.SparkingPanel(d, new Vector3(1.2f, 1.6f, 7.98f), Quaternion.identity);
                    EnvKit.Decor.BrokenBeam(d, new Vector3(5.5f, 0f, 4.8f), Quaternion.Euler(0f, 35f, 18f), 4.5f);
                    EnvKit.Decor.WarningBeacon(d, new Vector3(7.4f, 0f, 7.4f)); EnvKit.Decor.WarningBeacon(d, new Vector3(-7.4f, 3f, -7.4f));
                    EnvKit.Decor.Debris(d, new Vector3(6f, 0f, -1f), 1.6f, 14, 9);
                    break;
                default:
                    EnvKit.Decor.Scatter(d, theme, new Vector3(5.5f, 0f, 3f), new Vector2(3f, 6f), 6, 2);
                    break;
            }
            EnvKit.Decor.Scatter(d, theme, new Vector3(-6.4f, 0f, -6.6f), new Vector2(2.6f, 1.6f), 3, 7);
            EnvKit.VoidBackdrop(root, theme, new Bounds(new Vector3(2f, 0f, 0f), new Vector3(34f, 10f, 30f)));
            return rootGo;
        }
    }
}
