// SHIFTERS - DevPlay: campaign decor kit gallery (EnvKit.Decor.Bank / Night / Tower) in each theme's lighting.
//   devplay.sh <clone> rdecor -devSetup Shifters.RenderingDecorTests.Gallery -devSeconds 5
// Per piece: renderer / collider / vertex counts, material sanity, text facing, documented colliders; one contact sheet
// (3 x N close-ups) + one overview per theme in $SP/shots/rdecor/.
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Shifters
{
    public static class RenderingDecorTests
    {
        struct Piece
        {
            public string Name; public float Slot; public float FocusY; public float Dist; public float CamY; public int Colliders; public bool Front; public int MaxRenderers;
            public System.Func<Transform, Vector3, GameObject> Build;
        }

        static Piece P(string n, float slot, float focusY, float dist, int colliders, System.Func<Transform, Vector3, GameObject> b, bool front = true, float camY = -1f, int maxR = 12)
            => new Piece { Name = n, Slot = slot, FocusY = focusY, Dist = dist, Colliders = colliders, Build = b, Front = front, CamY = camY, MaxRenderers = maxR };

        static List<Piece> BankPieces() => new List<Piece>
        {
            P("MarbleColumn", 3f, 3f, 8f, 1, (t, p) => EnvKit.Decor.Bank.MarbleColumn(t, p), false),
            P("MarbleColumnRound", 3f, 3f, 8f, 1, (t, p) => EnvKit.Decor.Bank.MarbleColumn(t, p, null, 6f, 1.2f, true), false),
            P("TellerCounter", 8f, 2.2f, 8.5f, 2, (t, p) => EnvKit.Decor.Bank.TellerCounter(t, p)),
            P("VelvetRope", 3.5f, 0.6f, 3.2f, 0, (t, p) => EnvKit.Decor.Bank.VelvetRope(t, p, null, 2.5f, "PLEASE WAIT ALONE")),
            P("VelvetRopeLine", 6f, 0.6f, 5f, 0, (t, p) => EnvKit.Decor.Bank.VelvetRopeLine(t, p + new Vector3(-2.5f, 0, 0), p + new Vector3(2.5f, 0, 0), 2.5f, "NEXT CUSTOMER (ONE)", Vector3.back)),
            P("VaultDoorDecor", 11f, 4.6f, 13f, 0, (t, p) => EnvKit.Decor.Bank.VaultDoorDecor(t, p + new Vector3(0, 4.6f, 0), null, 8f, 25f)),
            P("CoinPile", 3f, 0.35f, 3.5f, 0, (t, p) => EnvKit.Decor.Bank.CoinPile(t, p), false, 1.6f),
            P("SafeDepositWall", 5f, 1.7f, 5.5f, 1, (t, p) => EnvKit.Decor.Bank.SafeDepositWall(t, p)),
            P("Chandelier", 4f, 4.3f, 5f, 0, (t, p) => EnvKit.Decor.Bank.Chandelier(t, p + new Vector3(0, 6.5f, 0), null, 1.2f, 1.2f, 2, true), false, 3.5f),
            P("PenOnAChain", 2f, 1.05f, 0.75f, 1, (t, p) => { var g = new GameObject("PenDesk"); g.transform.SetParent(t, false); g.transform.localPosition = p; Kit.SolidBox(g.transform, "Desk", new Vector3(0, 0.5f, 0), new Vector3(0.8f, 1f, 0.5f), MatKey.Marble); EnvKit.Decor.Bank.PenOnAChain(g.transform, new Vector3(0, 1f, 0)); return g; }, true, 1.4f),
            P("CanisterRed", 3f, 3f, 7.5f, 1, (t, p) => EnvKit.Decor.Bank.Canister(t, p, null, UIColors.Hex(0xFF3B3B), "RED")),
            P("CanisterPlaid", 3f, 3f, 7.5f, 1, (t, p) => EnvKit.Decor.Bank.Canister(t, p, null, UIColors.Hex(0xB07CFF), "PLAID (EXPERIMENTAL)", 6f, 0.9f, 0.4f)),
        };

        static List<Piece> NightPieces() => new List<Piece>
        {
            P("Cubicle", 3.5f, 0.9f, 3.6f, 4, (t, p) => EnvKit.Decor.Night.Cubicle(t, p), true, 2.6f),
            P("CubicleLit", 3.5f, 0.9f, 3.6f, 4, (t, p) => EnvKit.Decor.Night.Cubicle(t, p, null, 2.4f, 2.4f, 1.8f, 1, true, true), true, 2.6f),
            P("CubicleChairDown", 3.5f, 0.9f, 3.6f, 4, (t, p) => EnvKit.Decor.Night.Cubicle(t, p, null, 2.4f, 2.4f, 1.8f, 2), true, 2.6f),
            P("CopierStack", 4f, 0.7f, 3.8f, 1, (t, p) => EnvKit.Decor.Night.CopierStack(t, p, null, 2), true, 1.9f),
            P("WaterCoolerDead", 1.6f, 0.9f, 2.4f, 1, (t, p) => EnvKit.Decor.Night.WaterCooler(t, p)),
            P("WaterCooler", 1.6f, 0.9f, 2.4f, 1, (t, p) => EnvKit.Decor.Night.WaterCooler(t, p, null, false)),
            P("Cobweb", 2.5f, 2.1f, 2.2f, 1, (t, p) => { var g = new GameObject("WebCorner"); g.transform.SetParent(t, false); g.transform.localPosition = p; Kit.SolidBox(g.transform, "Wall", new Vector3(0, 1.5f, 0.3f), new Vector3(2.2f, 3f, 0.1f), MatKey.Wall); EnvKit.Decor.Night.Cobweb(g.transform, new Vector3(-0.9f, 2.9f, 0.24f), null, 1f); return g; }),
            P("MoonWindow", 3f, 2.2f, 4.5f, 1, (t, p) => { var g = new GameObject("WindowWall"); g.transform.SetParent(t, false); g.transform.localPosition = p; Kit.SolidBox(g.transform, "Wall", new Vector3(0, 2f, 0.15f), new Vector3(2.8f, 4f, 0.1f), MatKey.Wall); EnvKit.Decor.Night.MoonWindow(g.transform, new Vector3(0, 0.8f, 0.08f), null, 1.6f, 2.8f, true); return g; }),
            P("Gravestones", 7f, 0.8f, 4.6f, 4, (t, p) => { var g = new GameObject("GraveRow"); g.transform.SetParent(t, false); g.transform.localPosition = p; for (int i = 0; i < 4; i++) EnvKit.Decor.Night.Gravestone(g.transform, new Vector3(-2.4f + i * 1.6f, 0, 0), null, null, i); return g; }, true, 1.7f),
            P("IronFence", 5f, 1f, 5f, 1, (t, p) => EnvKit.Decor.Night.IronFence(t, p), false),
            P("Mausoleum", 9f, 2.6f, 12f, 11, (t, p) => EnvKit.Decor.Night.Mausoleum(t, p + new Vector3(0, 0, 3f), null, 6f, 6f, 5f, null, 30f), true, 3.5f),
        };

        static List<Piece> TowerPieces() => new List<Piece>
        {
            P("BlackGlassPanel", 4f, 2f, 5.5f, 0, (t, p) => EnvKit.Decor.Tower.BlackGlassPanel(t, p)),
            P("PropagandaScreen", 5f, 2.6f, 6f, 0, (t, p) => EnvKit.Decor.Tower.PropagandaScreen(t, p + new Vector3(0, 3.2f, 0), null, 4f, 2.4f, null, true)),
            P("ThroneDais", 8f, 1.6f, 9f, 4, (t, p) => EnvKit.Decor.Tower.ThroneDais(t, p + new Vector3(0, 0, 1f)), true, 3.2f),
            P("VoteBoard", 6f, 2f, 5.5f, 0, (t, p) => EnvKit.Decor.Tower.VoteBoard(t, p + new Vector3(0, 2f, 0))),
            P("CitySkyline", 0f, 30f, 95f, 0, (t, p) => EnvKit.Decor.Tower.CitySkyline(t, new Vector3(0, 0, 60f)), true, 12f, 6),
        };

        public static IEnumerator Gallery(DevPlayContext ctx)
        {
            var cam = Camera.main;
            ctx.Check(cam != null, "main camera");
            if (cam == null) yield break;
            cam.farClipPlane = Mathf.Max(cam.farClipPlane, 400f);
            yield return Theme(ctx, cam, LevelTheme.Bank, BankPieces(), true); // warm-up pass (variants compile)
            yield return Theme(ctx, cam, LevelTheme.Bank, BankPieces(), false);
            yield return Theme(ctx, cam, LevelTheme.Night, NightPieces(), false);
            yield return Theme(ctx, cam, LevelTheme.Tower, TowerPieces(), false);
            WorldColor.Enabled = false;
            yield return ApiChecks(ctx);
        }

        static IEnumerator Theme(DevPlayContext ctx, Camera cam, LevelTheme theme, List<Piece> pieces, bool warmup)
        {
            Kit.Theme = theme;
            LevelLighting.Apply(theme);
            PostFx.SetTheme(theme);
            // the campaign look: world color of the chapters that use this theme (CAMPAIGN 6.1)
            WorldColor.Enabled = true;
            WorldColor.SetBase(theme == LevelTheme.Bank ? 0.3f : theme == LevelTheme.Night ? 0.6f : 0.8f);
            var root = new GameObject("DecorGallery_" + theme).transform;
            float total = 0f; foreach (var pc in pieces) total += pc.Slot;
            EnvKit.Floor(root, new Vector3(0f, 0f, 2f), new Vector3(total + 10f, 1f, 30f));
            var objs = new List<GameObject>(); var centers = new List<Vector3>();
            float x = -total * 0.5f; string tn = theme.ToString().ToLowerInvariant();
            foreach (var pc in pieces)
            {
                var c = new Vector3(x + pc.Slot * 0.5f, 0f, 0f); x += pc.Slot;
                if (pc.Slot <= 0f) c = Vector3.zero;
                int before = CountLights();
                var go = pc.Build(root, c);
                objs.Add(go); centers.Add(c);
                if (warmup) continue;
                var rs = go.GetComponentsInChildren<Renderer>(true);
                int verts = 0; foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true)) if (mf.sharedMesh != null) verts += mf.sharedMesh.vertexCount;
                int bad = 0; foreach (var r in rs) foreach (var m in r.sharedMaterials) if (m == null || m.shader == null || m.shader.name.Contains("InternalError") || m.name.StartsWith("Default-")) bad++;
                int cols = go.GetComponentsInChildren<Collider>(true).Length;
                int texts = go.GetComponentsInChildren<TMPro.TextMeshPro>(true).Length;
                int lights = CountLights() - before;
                ctx.Log(tn + " " + pc.Name + ": renderers=" + rs.Length + " (texts " + texts + ") colliders=" + cols + " verts=" + verts + " lights=" + lights);
                ctx.Check(rs.Length > 0 && rs.Length <= pc.MaxRenderers, tn + " " + pc.Name + ": renderer count " + rs.Length + " <= " + pc.MaxRenderers);
                ctx.Check(bad == 0, tn + " " + pc.Name + ": no missing materials");
                ctx.Check(cols == pc.Colliders, tn + " " + pc.Name + ": documented colliders (" + cols + " == " + pc.Colliders + ")");
                ctx.Check(verts < 65000, tn + " " + pc.Name + ": vertex count sane");
                if (pc.Front)
                {
                    bool facing = true;
                    foreach (var tmp in go.GetComponentsInChildren<TMPro.TextMeshPro>(true)) if (Vector3.Dot(-tmp.transform.forward, Vector3.back) < 0.9f) { facing = false; ctx.Log("  text '" + tmp.text + "' faces " + (-tmp.transform.forward)); }
                    ctx.Check(facing, tn + " " + pc.Name + ": texts readable from the viewer (-Z)");
                }
                foreach (var l in go.GetComponentsInChildren<Light>(true)) ctx.Check(l.GetComponent<DecorLight>() != null && l.shadows == LightShadows.None, tn + " " + pc.Name + ": light is DecorLight-marked and shadowless");
            }
            yield return ctx.Wait(warmup ? 1f : 0.3f);
            if (warmup) { Object.Destroy(root.gameObject); yield return null; yield break; }

            // contact sheet: 3 columns of close-ups
            int colsN = 3, rowsN = Mathf.CeilToInt(pieces.Count / (float)colsN), tw = 480, th = 300;
            var sheet = new Texture2D(tw * colsN, th * rowsN, TextureFormat.RGB24, false);
            var rt = RenderTexture.GetTemporary(tw, th, 24, RenderTextureFormat.ARGB32);
            var tile = new Texture2D(tw, th, TextureFormat.RGB24, false);
            for (int i = 0; i < pieces.Count; i++)
            {
                var pc = pieces[i]; var c = centers[i];
                var target = c + new Vector3(0, pc.FocusY, pc.Name == "Mausoleum" ? 3f : 0f);
                var eye = c + new Vector3(pc.Dist * 0.28f, pc.CamY > 0f ? pc.CamY : pc.FocusY + pc.Dist * 0.18f, -pc.Dist);
                RenderTestUtil.Place(cam, eye, target, 50f);
                var prev = cam.targetTexture; cam.targetTexture = rt; cam.Render(); cam.targetTexture = prev;
                var act = RenderTexture.active; RenderTexture.active = rt;
                tile.ReadPixels(new Rect(0, 0, tw, th), 0, 0); tile.Apply(); RenderTexture.active = act;
                int cx = (i % colsN) * tw, cy = (rowsN - 1 - i / colsN) * th;
                sheet.SetPixels(cx, cy, tw, th, tile.GetPixels());
            }
            sheet.Apply();
            RenderTexture.ReleaseTemporary(rt);
            if (!string.IsNullOrEmpty(ctx.Config.shotsDir))
            {
                Directory.CreateDirectory(ctx.Config.shotsDir);
                var path = Path.Combine(ctx.Config.shotsDir, "sheet_" + tn + ".png");
                File.WriteAllBytes(path, sheet.EncodeToPNG());
                ctx.Result.shots.Add(path);
                ctx.Log("sheet " + path + " (left-to-right, top-to-bottom: " + string.Join(", ", pieces.ConvertAll(p => p.Name)) + ")");
                float luma = RenderTestUtil.MeanLuma(path);
                ctx.Check(RenderTestUtil.MagentaFraction(path) < 0.001f, tn + ": sheet has no magenta");
                ctx.Check(luma > 0.04f && luma < 0.93f, tn + ": sheet exposure sane (" + luma.ToString("0.00") + ")");
            }
            Object.Destroy(sheet); Object.Destroy(tile);
            // overview
            if (theme == LevelTheme.Tower) RenderTestUtil.Place(cam, new Vector3(0, 6f, -22f), new Vector3(0, 8f, 30f), 60f);
            else RenderTestUtil.Place(cam, new Vector3(0, 9f, -total * 0.45f), new Vector3(0, 1.5f, 1f), 60f);
            yield return null;
            ctx.Shot("overview_" + tn);
            Object.Destroy(root.gameObject);
            yield return null;
        }

        static int CountLights() => Object.FindObjectsByType<Light>().Length;

        static IEnumerator ApiChecks(DevPlayContext ctx)
        {
            Kit.Theme = LevelTheme.Tower;
            var root = new GameObject("DecorApi").transform;
            var board = EnvKit.Decor.Tower.VoteBoard(root, Vector3.zero);
            var aye = board.transform.Find("Aye")?.GetComponent<TMPro.TextMeshPro>();
            ctx.Check(aye != null && aye.text == "AYE\n0", "VoteBoard starts AYE 0");
            EnvKit.Decor.Tower.SetTally(board, 3, 1);
            ctx.Check(aye != null && aye.text == "AYE\n3", "SetTally updates AYE");
            var nay = board.transform.Find("Nay")?.GetComponent<TMPro.TextMeshPro>();
            ctx.Check(nay != null && nay.text == "NAY\n1 (ME)", "SetTally NAY note");
            ctx.Check(board.transform.Find("AyeLamp2").GetComponent<MeshRenderer>().sharedMaterial == Kit.Mat(MatKey.LightOn), "SetTally lights the 3rd AYE lamp");
            EnvKit.Decor.Tower.SetTally(null, 1, 1);
            Kit.Theme = LevelTheme.Bank;
            var can = EnvKit.Decor.Bank.Canister(root, Vector3.zero, null, Color.red);
            ctx.Check(WorldColor.IsImmune(can.transform.Find("Liquid").GetChild(0).GetComponent<MeshRenderer>().sharedMaterial), "Canister liquid is grey-immune");
            ctx.Check(WorldColor.IsImmune(can.transform.Find("Gold").GetComponent<MeshRenderer>().sharedMaterial), "gold parts are grey-immune");
            EnvKit.Decor.Bank.SetCanisterFill(can, 0.25f);
            ctx.Check(Mathf.Abs(can.transform.Find("Liquid").localScale.y - 0.25f) < 1e-4f, "SetCanisterFill scales the liquid");
            EnvKit.Decor.Bank.SetCanisterFill(can, 0f);
            ctx.Check(!can.transform.Find("Liquid").GetChild(0).gameObject.activeSelf, "SetCanisterFill(0) hides the liquid");
            var vd = EnvKit.Decor.Bank.VaultDoorDecor(root, Vector3.zero, null, 8f, 0f, true);
            ctx.Check(vd.transform.Find("Hinge/Door/Wheel") != null && vd.transform.Find("Hinge/Door").GetComponent<BoxCollider>() != null, "VaultDoorDecor children + optional door collider");
            var col = EnvKit.Decor.Bank.MarbleColumn(root, new Vector3(20f, 0, 0));
            var tc = EnvKit.Decor.Bank.TellerCounter(root, new Vector3(40f, 0, 52.7f));
            Physics.SyncTransforms();
            ctx.Check(Physics.Raycast(new Vector3(20f, 3f, -5f), Vector3.forward, out var hit, 10f) && Mathf.Abs(hit.point.z + 0.6f) < 0.05f, "MarbleColumn collider is 1.2 m cover");
            ctx.Check(Physics.Raycast(new Vector3(40f, 1f, 45f), Vector3.forward, out hit, 10f) && Mathf.Abs(hit.point.z - 52f) < 0.05f, "TellerCounter face at pos.z - depth/2 (LEVELS2 CH04 layout)");
            ctx.Check(Physics.Raycast(new Vector3(40f, 4f, 45f), Vector3.forward, out hit, 10f) && hit.point.z > 52.5f, "TellerCounter glass collider above the counter");
            Object.Destroy(root.gameObject);
            yield return null;
        }
    }
}
