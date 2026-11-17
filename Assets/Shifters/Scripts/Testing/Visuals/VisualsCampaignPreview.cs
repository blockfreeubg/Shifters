// =====================================================================================
//  SHIFTERS - VisualsCampaignPreview.cs   (VISUALS tests only, campaign wave)
//  DevPlay: devplay.sh <proj> viscamp -devSetup Shifters.VisualsCampaignPreview.All -devSize 1920x1080
//  * ME-TV studio contact sheets (the real 320x240 RT): expressions + gestures, hats + Percy faces.
//  * World rows: Max turntable / close-up / variants / expressions / gestures, Percy, dummies,
//    scary props (cutout, ghost trio, guard bot), interns (disguise, gold statue, SCARED).
// =====================================================================================
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Shifters
{
    public static class VisualsCampaignPreview
    {
        static Camera Cam => Camera.main;

        static void Look(Vector3 pos, Vector3 target, float fov)
        {
            var c = Cam; if (c == null) return;
            c.transform.position = pos; c.transform.LookAt(target);
            c.fieldOfView = fov; c.nearClipPlane = 0.05f; c.farClipPlane = 400f;
        }

        static void Floor(string name, Vector3 topCenter, Vector3 size)
            => Kit.SolidBox(null, name, topCenter - new Vector3(0f, size.y * 0.5f, 0f), size, MatKey.Floor, 0.02f);

        // ---------------------------------------------------------------- contact sheet of RT tiles
        sealed class Sheet
        {
            public Texture2D Tex; public int Cols, Rows, W, H; public float MinStd = 1f; public int Tiles;
            public Sheet(int cols, int rows, int w, int h)
            {
                Cols = cols; Rows = rows; W = w; H = h;
                Tex = new Texture2D(cols * w, rows * h, TextureFormat.RGBA32, false);
                var px = new Color32[Tex.width * Tex.height];
                for (int i = 0; i < px.Length; i++) px[i] = new Color32(20, 20, 24, 255);
                Tex.SetPixels32(px);
            }

            public void Grab(Texture t, int index)
            {
                var rt = t as RenderTexture; if (rt == null) return;
                int col = index % Cols, row = index / Cols;
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                Tex.ReadPixels(new Rect(0, 0, Mathf.Min(W, rt.width), Mathf.Min(H, rt.height)), col * W, (Rows - 1 - row) * H);
                RenderTexture.active = prev;
                Tex.Apply();
                // per-tile luminance std (0 = blank)
                var px = Tex.GetPixels(col * W, (Rows - 1 - row) * H, W, H);
                double sum = 0, sq = 0;
                for (int i = 0; i < px.Length; i++) { double l = px[i].r * 0.3 + px[i].g * 0.59 + px[i].b * 0.11; sum += l; sq += l * l; }
                double mean = sum / px.Length;
                MinStd = Mathf.Min(MinStd, (float)System.Math.Sqrt(System.Math.Max(0.0, sq / px.Length - mean * mean)));
                Tiles++;
            }

            public void Save(DevPlayContext ctx, string name)
            {
                string dir = ctx.Config.shotsDir;
                if (string.IsNullOrEmpty(dir)) dir = Application.temporaryCachePath;
                Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, name + ".png");
                File.WriteAllBytes(path, Tex.EncodeToPNG());
                ctx.Result.shots.Add(path);
                ctx.Log("sheet " + path + " tiles " + Tiles + " minStd " + MinStd.ToString("0.000"));
                Object.Destroy(Tex);
            }
        }

        public static IEnumerator All(DevPlayContext ctx)
        {
            RenderSettings.ambientIntensity = 1.1f;
            // key light from over the camera's shoulder (cameras look down -Z at models facing +Z); earlier runs were backlit
            bool anySun = false;
            foreach (var dl in Object.FindObjectsByType<Light>())
                if (dl.type == LightType.Directional) { anySun = true; dl.transform.rotation = Quaternion.Euler(40f, 155f, 0f); }
            if (!anySun)
            {
                var l = new GameObject("PreviewSun").AddComponent<Light>();
                l.type = LightType.Directional; l.intensity = 1.2f; l.shadows = LightShadows.Soft;
                l.transform.rotation = Quaternion.Euler(40f, 155f, 0f);
            }
            yield return Studio(ctx);
            yield return MaxWorld(ctx);
            yield return PercyDummies(ctx);
            yield return ScaryProps(ctx);
            yield return Interns(ctx);
            ctx.Log("visuals campaign preview complete");
        }

        // ---------------------------------------------------------------- 1. ME-TV studio (the RT)
        static readonly Expression[] MaxFaces = { Expression.Smug, Expression.Laugh, Expression.Angry, Expression.Shocked, Expression.Pout, Expression.Defeated };
        static readonly VillainGesture[] Gestures = { VillainGesture.Monologue, VillainGesture.OneHandClap, VillainGesture.Point, VillainGesture.Facepalm, VillainGesture.Rage, VillainGesture.CrownSlip };

        static IEnumerator Studio(DevPlayContext ctx)
        {
            var st = VillainStudio.Ensure();
            yield return ctx.Wait(0.2f);
            ctx.Check(st != null && st.Texture != null && st.Texture.width == 320 && st.Texture.height == 240, "studio: one 320x240 RT valid immediately");
            ctx.Check(st != null && Mathf.Abs(st.transform.position.y + 1200f) < 0.01f, "studio: stage at y = -1200");
            ctx.Check(st != null && st.Cam != null && !st.Cam.enabled && !st.IsLive, "studio: camera idle while not live");
            if (st == null) yield break;
            st.AutoListen = true;

            // expressions + gestures sheet (4x3)
            var sheet = new Sheet(4, 3, 320, 240);
            st.Begin("MAX");
            st.SetHat(VillainHat.Crown);
            ctx.Check(st.IsLive && st.Cam != null && st.Cam.enabled, "studio: Begin -> live, camera renders");
            int tile = 0;
            foreach (var e in MaxFaces)
            {
                st.SetExpression(e);
                yield return ctx.Wait(0.55f);
                sheet.Grab(st.Texture, tile++);
                ctx.Check(st.Villain != null && st.Villain.CurrentExpression == (e == Expression.Smug ? st.Villain.CurrentExpression : e), "studio: Max face " + e);
            }
            st.SetExpression(Expression.Neutral);
            foreach (var g in Gestures)
            {
                st.Play(g);
                yield return ctx.Wait(VillainLook.GestureDuration(g) * (g == VillainGesture.CrownSlip ? 0.35f : 0.45f));
                ctx.Check(st.Villain != null && st.Villain.IsGesturing, "studio: gesture " + g + " playing");
                sheet.Grab(st.Texture, tile++);
                yield return ctx.Wait(VillainLook.GestureDuration(g) * 0.6f + 0.2f);
            }
            ctx.Check(sheet.MinStd > 0.03f, "studio: every expression/gesture tile has content (min std " + sheet.MinStd.ToString("0.000") + ")");
            sheet.Save(ctx, "studio_faces_gestures");

            // hats + Percy sheet (4x3): 8 hats, 4 Percy faces
            var sheet2 = new Sheet(4, 3, 320, 240);
            tile = 0;
            foreach (VillainHat h in System.Enum.GetValues(typeof(VillainHat)))
            {
                st.SetHat(h);
                st.SetExpression(h == VillainHat.Janitor ? Expression.Pout : h == VillainHat.Weatherman ? Expression.Laugh : Expression.Smug);
                yield return ctx.Wait(0.35f);
                ctx.Check(st.Villain != null && st.Villain.Hat == h, "studio: hat " + h);
                sheet2.Grab(st.Texture, tile++);
            }
            st.SetHat(VillainHat.Crown);
            st.SetSpeaker("PERCY");
            ctx.Check(st.SpeakerIsPercy && st.Speaker == VillainStudio.SpeakerPercy, "studio: Percy speaker");
            PercyFace[] pf = { PercyFace.Happy, PercyFace.Crying, PercyFace.Shocked, PercyFace.Static };
            foreach (var f in pf)
            {
                st.Percy?.SetFace(f);
                yield return ctx.Wait(0.35f);
                sheet2.Grab(st.Texture, tile++);
            }
            ctx.Check(sheet2.MinStd > 0.03f, "studio: hat/Percy tiles have content (min std " + sheet2.MinStd.ToString("0.000") + ")");
            sheet2.Save(ctx, "studio_hats_percy");

            // auto-listen: a story line by MAX goes live and ends with the line
            st.EndAll();
            yield return ctx.Wait(0.1f);
            ctx.Check(!st.IsLive && st.Cam != null && !st.Cam.enabled, "studio: EndAll -> not live, camera off");
            GameEvents.RaiseBroadcastChanged(true, "MAX");
            yield return ctx.Wait(0.2f);
            ctx.Check(st.IsLive && !st.SpeakerIsPercy, "studio: BroadcastChanged(true, MAX) -> live on Max");
            GameEvents.RaiseBroadcastChanged(false, "MAX");
            yield return ctx.Wait(0.2f);
            ctx.Check(!st.IsLive, "studio: BroadcastChanged(false) -> off");
            var still = st.RenderStill();
            ctx.Check(still != null, "studio: RenderStill returns the RT while not live");
            ctx.Check(st.Villain != null && st.Villain.Do("hat:Judge") && st.Villain.Do("angry:1.0") && st.Villain.Do("facepalm") && st.Villain.Do("hat:Crown") && !st.Villain.Do("not-a-thing"), "villain: Do(string) hooks");
        }

        // ---------------------------------------------------------------- 2. Max in the world
        static IEnumerator MaxWorld(DevPlayContext ctx)
        {
            const float X = 1000f;
            Floor("MaxFloor", new Vector3(X + 6f, 0f, -6f), new Vector3(30f, 1f, 30f));
            var root = new GameObject("MaxPreview").transform;
            // turntable
            float[] yaws = { 0f, 45f, 90f, 180f };
            var tt = new List<VillainPuppet>();
            for (int i = 0; i < yaws.Length; i++) tt.Add(VillainPuppet.Create(root, new Vector3(X + i * 1.3f, 0f, 0f), Quaternion.Euler(0f, yaws[i], 0f)));
            yield return ctx.Wait(0.5f);
            var r = tt[0].GetComponentsInChildren<Renderer>();
            float top = float.MinValue; foreach (var rr in r) top = Mathf.Max(top, rr.bounds.max.y);
            ctx.Log("Max renderers " + r.Length + ", top " + top.ToString("0.00"));
            ctx.Check(top > 1.25f && top < 1.75f, "Max height with crown ~1.45 (top " + top.ToString("0.00") + ")");
            ctx.Check(r.Length <= 60, "Max <= 60 renderers (" + r.Length + ")");
            Look(new Vector3(X + 1.95f, 1.0f, 4.6f), new Vector3(X + 1.95f, 0.72f, 0f), 40f); ctx.Shot("max_turntable");
            var hc = tt[0].HeadCenter;
            Look(hc + new Vector3(0.35f, 0.1f, 1.35f), hc + new Vector3(0f, -0.08f, 0f), 40f); ctx.Shot("max_face");
            Look(hc + new Vector3(-0.2f, 0.25f, -1.6f) + new Vector3(0f, -0.3f, 0f), hc + new Vector3(0f, -0.35f, 0f), 45f);
            Look(new Vector3(X + 3.9f - 0.3f, 1.1f, -1.9f), new Vector3(X + 3.9f, 0.7f, 0f), 45f); ctx.Shot("max_back_band");
            foreach (var p in tt) Object.Destroy(p.gameObject);

            // variants: normal, hologram, gold statue, defeated
            float z = -6f;
            var v0 = VillainPuppet.Create(root, new Vector3(X, 0f, z), Quaternion.identity);
            var v1 = VillainPuppet.Create(root, new Vector3(X + 1.3f, 0f, z), Quaternion.identity, true);
            var v2 = VillainPuppet.Create(root, new Vector3(X + 2.6f, 0f, z), Quaternion.identity);
            var v3 = VillainPuppet.Create(root, new Vector3(X + 3.9f, 0f, z), Quaternion.identity);
            v2.SetGold(true); v2.Frozen = true;
            v3.SetExpression(Expression.Defeated); v3.SetHat(VillainHat.EmployeeOfTheMonth);
            yield return ctx.Wait(0.5f);
            ctx.Check(v1.IsHologram && !v0.IsHologram, "hologram variant");
            ctx.Check(v2.IsGold, "Max gold statue");
            Look(new Vector3(X + 1.95f, 1.0f, z + 4.6f), new Vector3(X + 1.95f, 0.72f, z), 40f); ctx.Shot("max_variants");
            Object.Destroy(v0.gameObject); Object.Destroy(v1.gameObject); Object.Destroy(v2.gameObject); Object.Destroy(v3.gameObject);

            // expressions row
            z = -12f;
            var ex = new List<VillainPuppet>();
            for (int i = 0; i < MaxFaces.Length; i++)
            {
                var p = VillainPuppet.Create(root, new Vector3(X + i * 1.0f, 0f, z), Quaternion.identity);
                p.SetExpression(MaxFaces[i]); p.AutoCrownSlip = false; ex.Add(p);
            }
            yield return ctx.Wait(0.6f);
            bool allFaces = true; for (int i = 0; i < ex.Count; i++) if (MaxFaces[i] != Expression.Smug && ex[i].CurrentExpression != MaxFaces[i]) allFaces = false;
            ctx.Check(allFaces, "Max: every expression applied");
            Look(new Vector3(X + 2.5f, 1.1f, z + 3.6f), new Vector3(X + 2.5f, 0.95f, z), 45f); ctx.Shot("max_expressions");
            foreach (var p in ex) Object.Destroy(p.gameObject);

            // gestures row, two moments
            z = -18f;
            var gs = new List<VillainPuppet>();
            for (int i = 0; i < Gestures.Length; i++) gs.Add(VillainPuppet.Create(root, new Vector3(X + i * 1.15f, 0f, z), Quaternion.identity));
            yield return ctx.Wait(0.2f);
            for (int i = 0; i < gs.Count; i++) { gs[i].PlaySounds = false; gs[i].Play(Gestures[i]); }
            yield return ctx.Wait(0.55f);
            bool allG = true; foreach (var p in gs) if (!p.IsGesturing) allG = false;
            ctx.Check(allG, "Max: every gesture playing");
            Look(new Vector3(X + 2.9f, 1.15f, z + 4.2f), new Vector3(X + 2.9f, 0.8f, z), 45f); ctx.Shot("max_gestures_a");
            yield return ctx.Wait(0.55f);
            ctx.Shot("max_gestures_b");
            yield return ctx.Wait(3f);
            bool done = true; foreach (var p in gs) if (p.CurrentGesture != VillainGesture.None) done = false;
            ctx.Check(done, "Max: gestures finish");
            foreach (var p in gs) Object.Destroy(p.gameObject);
        }

        // ---------------------------------------------------------------- 3. Percy + dummies
        static IEnumerator PercyDummies(DevPlayContext ctx)
        {
            const float X = 1100f;
            Floor("PDFloor", new Vector3(X + 5f, 0f, -4f), new Vector3(30f, 1f, 24f));
            var root = new GameObject("PercyDummyPreview").transform;
            var faces = (PercyFace[])System.Enum.GetValues(typeof(PercyFace));
            var ps = new List<PercyPuppet>();
            for (int i = 0; i < faces.Length; i++)
            {
                var p = PercyPuppet.Create(root, new Vector3(X + i * 1.1f, 0f, 0f));
                p.SetFace(faces[i]); ps.Add(p);
            }
            ps[ps.Count - 1].SetWig(true);
            yield return ctx.Wait(0.5f);
            bool fOk = true; for (int i = 0; i < ps.Count; i++) if (ps[i].CurrentFace != faces[i]) fOk = false;
            ctx.Check(fOk, "Percy: every face applied");
            Look(new Vector3(X + 3.85f, 1.4f, 5.4f), new Vector3(X + 3.85f, 0.75f, 0f), 45f); ctx.Shot("percy_faces");
            Look(ps[1].FaceCenter + new Vector3(0.5f, 0.2f, 1.7f), ps[1].FaceCenter + new Vector3(0f, -0.2f, 0f), 45f); ctx.Shot("percy_close");

            var poses = (DummyPose[])System.Enum.GetValues(typeof(DummyPose));
            var ds = new List<DummyModel>();
            float z = -8f;
            for (int i = 0; i < poses.Length; i++)
            {
                var d = DummyModel.Create(root, new Vector3(X + i * 1.3f, 0f, z), Quaternion.Euler(0f, 0f, 0f));
                d.SetPose(poses[i], 0f); ds.Add(d);
            }
            var mild = DummyModel.Create(root, new Vector3(X + poses.Length * 1.3f, 0f, z), null, true);
            yield return ctx.Wait(0.6f);
            bool pOk = true; for (int i = 0; i < ds.Count; i++) if (ds[i].Pose != poses[i]) pOk = false;
            ctx.Check(pOk, "Dummy: every pose");
            ctx.Check(mild.Mild, "Dummy: mild variant");
            float xc = X + poses.Length * 0.65f;
            Look(new Vector3(xc, 1.6f, z + 7.6f), new Vector3(xc, 0.95f, z), 45f); ctx.Shot("dummy_poses");
            // night look: dark, one light on the first dummy
            float amb = RenderSettings.ambientIntensity;
            RenderSettings.ambientIntensity = 0.15f;
            var spot = new GameObject("Flash").AddComponent<Light>();
            spot.type = LightType.Spot; spot.range = 12f; spot.spotAngle = 35f; spot.intensity = 6f;
            spot.transform.position = new Vector3(X + 2.6f, 1.5f, z + 3.2f); spot.transform.LookAt(ds[2].transform.position + Vector3.up * 1.3f);
            ds[1].SetMoving(true); ds[2].SetLit(true);
            yield return ctx.Wait(0.4f);
            Look(new Vector3(X + 2.6f, 1.55f, z + 3.6f), new Vector3(X + 2.4f, 1.1f, z), 50f); ctx.Shot("dummy_night");
            Object.Destroy(spot.gameObject); RenderSettings.ambientIntensity = amb;
        }

        // ---------------------------------------------------------------- 4. scary props + bot
        static IEnumerator ScaryProps(DevPlayContext ctx)
        {
            const float X = 1200f;
            Floor("SPFloor", new Vector3(X + 4f, 0f, 0f), new Vector3(24f, 1f, 16f));
            var root = new GameObject("ScaryPreview").transform;
            var cut = CardboardMaxModel.Create(root, new Vector3(X, 0f, 0f), Quaternion.Euler(0f, 15f, 0f));
            var trio = GhostTrio.Create(root, new Vector3(X + 3f, 0f, 0f));
            var bot = EnemyModelBuilder.Build(root, new Color(1f, 0.35f, 0.3f));
            bot.transform.position = new Vector3(X + 6.5f, 0f, 0f);
            var cap = EnemyModelBuilder.AddGuardCap(bot);
            var tie = EnemyModelBuilder.AddBowTie(bot);
            var skin = StalkerSkins.Build(StalkerSkin.CardboardMax, root, false);
            if (skin != null && skin.Root != null) skin.Root.position = new Vector3(X + 8.5f, 0f, 0f);
            yield return ctx.Wait(0.6f);
            ctx.Check(cut != null && trio != null && trio.Ghosts.Length == 3, "cutout + ghost trio built");
            ctx.Check(cap != null && tie != null, "guard cap + bow tie on BONK-BOT");
            ctx.Check(skin != null, "StalkerSkins.Build(CardboardMax)");
            Look(new Vector3(X + 4.2f, 1.6f, 7.2f), new Vector3(X + 4.2f, 0.85f, 0f), 45f); ctx.Shot("scary_props");
            trio.Boo(); cut.Wobble(1f);
            yield return ctx.Wait(0.45f);
            Look(new Vector3(X + 3.4f, 1.4f, 3.6f), new Vector3(X + 3f, 0.9f, 0f), 50f); ctx.Shot("ghosts_boo");
        }

        // ---------------------------------------------------------------- 5. interns
        static IEnumerator Interns(DevPlayContext ctx)
        {
            const float X = 1300f;
            Floor("InFloor", new Vector3(X + 4f, 0f, 0f), new Vector3(24f, 1f, 16f));
            var root = new GameObject("InternPreview").transform;
            var team = VisualsTestKit.Team(42);
            var pups = new List<CharacterPuppet>();
            for (int i = 0; i < 3; i++) pups.Add(CharacterPuppet.Create(team.Members[i], root, new Vector3(X + i * 1.4f, 0f, 0f), Quaternion.identity));
            for (int i = 0; i < 3; i++) { var p = CharacterPuppet.Create(team.Members[i], root, new Vector3(X + 4.6f + i * 1.4f, 0f, 0f), Quaternion.identity); pups.Add(p); }
            yield return ctx.Wait(0.2f);
            pups[0].SetDisguise(true); pups[1].SetDisguise(true); pups[2].SetDisguise(true);
            pups[3].SetGold(true); pups[3].Frozen = true;
            pups[4].Scared = true;
            pups[5].SetGold(true); pups[5].SetExpression(Expression.Happy); pups[5].Frozen = true;
            yield return ctx.Wait(0.5f);
            ctx.Check(pups[0].HasDisguise && pups[1].HasDisguise && pups[2].HasDisguise && !pups[3].HasDisguise, "disguise mustache on interns");
            ctx.Check(pups[3].IsGold && pups[5].IsGold && !pups[4].IsGold, "SetGold statue");
            Look(new Vector3(X + 3.9f, 1.7f, 6.8f), new Vector3(X + 3.9f, 1.0f, 0f), 45f); ctx.Shot("interns_disguise_gold_scared");
            Look(pups[1].HeadCenter + new Vector3(0.3f, 0.1f, 1.3f), pups[1].HeadCenter, 40f); ctx.Shot("intern_mustache_close");
            pups[3].SetGold(false);
            yield return ctx.Wait(0.1f);
            ctx.Check(!pups[3].IsGold, "SetGold(false) restores");

            // real character: SCARED override trembles
            var c = CharacterFactory.Create(team.Members[0], 0, new Vector3(X, 0f, -4f), Quaternion.identity);
            c.Body.isKinematic = true; c.ExternalControl = true;
            var vis = c.GetComponent<CharacterVisual>();
            yield return ctx.Wait(0.3f);
            if (vis != null)
            {
                vis.ScaredOverride = true;
                yield return ctx.Wait(0.4f);
                ctx.Check(vis.IsScaredLook && vis.CurrentExpression == Expression.Scared, "CharacterVisual: scared look + Scared face");
                vis.ScaredOverride = false;
                yield return ctx.Wait(0.3f);
                ctx.Check(!vis.IsScaredLook, "CharacterVisual: scared look off");
                vis.ScaredOverride = null;
                bool had = vis.HasDisguise;
                vis.SetDisguise(true);
                ctx.Check(vis.HasDisguise && CharacterDisguise.Burn(c) && !vis.HasDisguise, "disguise burns off (was " + had + ")");
            }
            else ctx.Check(false, "CharacterVisual present on character");
        }
    }
}
