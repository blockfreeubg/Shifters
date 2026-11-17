// SHIFTERS - DevPlay: shader test sheet + Fx sheet.
//   devplay.sh <clone> rsheet -devSetup Shifters.RenderingSheetTests.Run
using System.Collections;
using UnityEngine;

namespace Shifters
{
    public static class RenderingSheetTests
    {
        public static IEnumerator Run(DevPlayContext ctx)
        {
            // ---------------------------------------------------------------- shaders exist & compile
            string[] shaders = { MaterialLibrary.LitShaderName, MaterialLibrary.HologramShaderName, MaterialLibrary.SkyShaderName, MaterialLibrary.ParticleShaderName, MaterialLibrary.UnlitShaderName };
            foreach (var n in shaders)
            {
                var sh = Shader.Find(n);
                ctx.Check(sh != null && sh.isSupported, "shader supported: " + n);
            }
            int bad = 0, total = 0;
            foreach (LevelTheme th in System.Enum.GetValues(typeof(LevelTheme)))
                foreach (MatKey k in System.Enum.GetValues(typeof(MatKey)))
                {
                    var m = MaterialLibrary.Get(k, th); total++;
                    if (m == null || m.shader == null || !m.shader.isSupported || m.shader.name.Contains("Error")) bad++;
                }
            ctx.Check(bad == 0, "all " + total + " theme materials valid (bad=" + bad + ")");
            ctx.Check(LevelLighting.SkyMaterial(LevelTheme.Lab) != null, "sky material");

            var sun = LevelLighting.Apply(LevelTheme.Training);
            PostFx.SetTheme(LevelTheme.Training);
            ctx.Check(sun != null && RenderSettings.sun == sun, "sun applied");
            ctx.Check(PostFx.Volume != null && PostFx.Volume.sharedProfile != null, "postfx volume");

            // ---------------------------------------------------------------- shader sheet scene
            var root = new GameObject("ShaderSheet").transform;
            Kit.Theme = LevelTheme.Training;
            Kit.SolidBox(root, "Floor", new Vector3(0, -0.25f, 4), new Vector3(30, 0.5f, 24), MatKey.Floor, 0.02f);
            Kit.SolidBox(root, "BackWall", new Vector3(0, 2f, 9f), new Vector3(30, 4.5f, 0.4f), MatKey.Wall, 0.02f);
            Kit.SolidBox(root, "SideWall", new Vector3(-7f, 1.5f, 5f), new Vector3(0.4f, 3f, 8f), MatKey.WallAlt, 0.02f);
            MatKey[] keys = { MatKey.PuzzleBody, MatKey.MetalLight, MatKey.Wood, MatKey.CrateHeavy, MatKey.Glass, MatKey.Hologram, MatKey.LightOn, MatKey.HazardStripes, MatKey.Acid };
            for (int i = 0; i < keys.Length; i++)
            {
                float x = -6f + i * 1.5f;
                Kit.BoxVisual(root, "Box_" + keys[i], new Vector3(x, 0.5f, 3f), Vector3.one, keys[i], 0.08f);
                var sph = ProcMesh.Part("Sph_" + keys[i], root, ProcMesh.Sphere(0.45f, 20, 14, true), Kit.Mat(keys[i]), new Vector3(x, 1.6f, 3f));
            }
            // character stand-ins: colored capsules, one with reveal glow
            Color[] cc = { UIColors.Hex(0xFF6B3D), UIColors.Hex(0x3DA5FF), UIColors.Hex(0xB07CFF) };
            for (int i = 0; i < 3; i++)
            {
                var m = MaterialLibrary.Instance(cc[i], 0.35f);
                var cap = ProcMesh.Part("Cap" + i, root, ProcMesh.Capsule(0.4f, 1.7f, 16, 5), m, new Vector3(-3f + i * 1.4f, 0.85f, 0.5f));
                if (i == 2) { m.SetColor(ShaderIds.RevealColor, UIColors.Intellect * 1.5f); m.SetFloat(ShaderIds.RevealStrength, 1f); }
            }
            // point light response + additional light on wall
            var lgo = new GameObject("PointLight");
            lgo.transform.SetParent(root, false);
            lgo.transform.localPosition = new Vector3(4.5f, 1.2f, 6.5f);
            var pl = lgo.AddComponent<Light>(); pl.type = LightType.Point; pl.color = new Color(1f, 0.45f, 0.1f); pl.range = 6f; pl.intensity = 6f;
            var lgo2 = new GameObject("PointLight2");
            lgo2.transform.SetParent(root, false);
            lgo2.transform.localPosition = new Vector3(-5.5f, 1.2f, 6f);
            var pl2 = lgo2.AddComponent<Light>(); pl2.type = LightType.Point; pl2.color = new Color(0.2f, 0.8f, 1f); pl2.range = 5f; pl2.intensity = 5f;

            var cam = Camera.main;
            RenderTestUtil.Place(cam, new Vector3(0f, 3.4f, -6.5f), new Vector3(0f, 1f, 3.5f), 55f);
            yield return ctx.Wait(0.3f);
            var shot = ctx.Shot("shader_sheet");
            ctx.Check(RenderTestUtil.MagentaFraction(shot) < 0.001f, "no magenta in shader sheet");
            float luma = RenderTestUtil.MeanLuma(shot);
            ctx.Log("shader sheet luma=" + luma.ToString("0.00"));
            ctx.Check(luma > 0.15f && luma < 0.92f, "shader sheet exposure sane");

            // dark themes too
            LevelLighting.Apply(LevelTheme.Chaos); PostFx.SetTheme(LevelTheme.Chaos);
            yield return ctx.Wait(0.1f);
            var chaos = ctx.Shot("shader_sheet_chaos");
            ctx.Check(RenderTestUtil.MagentaFraction(chaos) < 0.001f, "no magenta (chaos)");

            // ---------------------------------------------------------------- Fx sheet
            LevelLighting.Apply(LevelTheme.Training); PostFx.SetTheme(LevelTheme.Training);
            Object.Destroy(root.gameObject);
            var fxRoot = new GameObject("FxSheet").transform;
            Kit.SolidBox(fxRoot, "Floor", new Vector3(0, -0.25f, 4), new Vector3(40, 0.5f, 30), MatKey.FloorAlt, 0.02f);
            var dummy = ProcMesh.Part("KOGuy", fxRoot, ProcMesh.Capsule(0.4f, 1.7f, 16, 5), MaterialLibrary.Colored(UIColors.Hex(0xFF6B3D)), new Vector3(-6f, 0.85f, 4f));
            var runner = ProcMesh.Part("Runner", fxRoot, ProcMesh.Capsule(0.35f, 1.8f, 16, 5), MaterialLibrary.Colored(UIColors.Hex(0x3DE0FF)), new Vector3(6f, 0.9f, 2f));
            RenderTestUtil.Place(cam, new Vector3(0f, 4.2f, -7.5f), new Vector3(0f, 1f, 4f), 60f);

            Fx.ScanPulse(new Vector3(0, 0, 4), UIColors.Intellect, 12f, 1.2f);
            Fx.Stars(dummy.transform, 5f);
            Fx.SpeedLines(runner.transform, 5f);
            for (int f = 0; f < 3; f++)
            {
                Fx.Puff(new Vector3(-4.5f, 0f, 1.5f), 1f);
                Fx.Sparks(new Vector3(-3f, 1f, 1.5f), Vector3.up, 14);
                Fx.Impact(new Vector3(-1.5f, 1f, 6f), Vector3.back, UIColors.Hex(0xFFC93C));
                Fx.Tracer(new Vector3(-6f, 1.4f, 0f), new Vector3(-1.5f, 1f, 6f), UIColors.Hex(0xFF4D5E), 0.08f, 0.6f);
                Fx.MuzzleFlash(new Vector3(-6f, 1.4f, 0f), new Vector3(0.6f, 0f, 1f), UIColors.Hex(0xFFC93C));
                yield return ctx.Wait(0.05f);
            }
            Fx.Poof(new Vector3(0f, 0.6f, 1.5f), UIColors.Hex(0x3DDC97), 1f);
            Fx.Confetti(new Vector3(2f, 0.5f, 5f));
            Fx.Splash(new Vector3(3.5f, 0f, 1.5f), UIColors.Hex(0x7CFF4D));
            Fx.Explosion(new Vector3(5f, 0.5f, 7f), 1.5f);
            Fx.FloatingText(new Vector3(-6f, 2.2f, 4f), "OOF", UIColors.Hex(0xFFC93C), 1f);
            Fx.FloatingText(new Vector3(0f, 2.5f, 3f), "TOO HEAVY!", UIColors.Danger, 1f);
            Fx.FloatingText(new Vector3(4f, 2.2f, 3f), "-35", Color.white, 1.2f);
            PostFx.Flash(new Color(1f, 0.3f, 0.3f), 0.5f);
            yield return ctx.Wait(0.12f);
            var fxa = ctx.Shot("fx_sheet_a");
            var fx = FxSystem.Instance;
            ctx.Check(fx != null, "fx system alive");
            if (fx != null)
            {
                ctx.Log("fx particles=" + fx.ParticleCount + " texts=" + fx.ActiveTexts + " stars=" + fx.ActiveStars + " pulses=" + fx.ActivePulses + " tracers=" + fx.ActiveTracers + " speed=" + fx.ActiveSpeedLines);
                ctx.Check(fx.ParticleCount > 50, "particles emitted");
                ctx.Check(fx.ActiveTexts == 3 && fx.ActiveStars == 1 && fx.ActivePulses == 1 && fx.ActiveTracers > 0 && fx.ActiveSpeedLines == 1, "pooled fx active");
            }
            ctx.Check(RenderTestUtil.MagentaFraction(fxa) < 0.001f, "no magenta in fx sheet");
            yield return ctx.Wait(0.35f);
            ctx.Shot("fx_sheet_b");
            // spam safety
            for (int i = 0; i < 400; i++) { Fx.Sparks(Vector3.up, Vector3.up, 20); Fx.FloatingText(Vector3.up * 2, "SPAM", Color.white); Fx.Tracer(Vector3.zero, Vector3.one * 3, Color.red); }
            yield return ctx.Wait(1.5f);
            ctx.Check(fx != null && fx.ActiveTexts <= 32, "text pool capped");
            ctx.Shot("fx_sheet_c");
            yield return ctx.Wait(1.5f);
            ctx.Check(fx != null && fx.ActivePulses == 0 && fx.ActiveTracers == 0, "fx pools recycled");
        }
    }
}
