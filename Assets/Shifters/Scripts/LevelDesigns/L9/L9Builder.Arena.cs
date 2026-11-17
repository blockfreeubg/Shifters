// SHIFTERS - L9Builder.Arena.cs   [LEVEL 9]  the PORKCHOP arena (pillars, loading bays, C4-C6), the boss, the van and the exit
using UnityEngine;

namespace Shifters
{
    public partial class L9Builder
    {
        // Bait spots: in each pillar's shadow (as seen from the centre), 1.9 m clear of the loading bay's face so the follow
        // camera (behind the player, away from the pig) rises over the 1.6 m bay instead of pushing into the player's head.
        static readonly Vector3[] BaitPos = { new Vector3(-9.6f, 0f, 128.9f), new Vector3(9.6f, 0f, 128.9f), new Vector3(9.6f, 0f, 145.1f), new Vector3(-9.6f, 0f, 145.1f) };
        static readonly Vector3[] SidePos = { new Vector3(-5.2f, 0f, 128.0f), new Vector3(5.2f, 0f, 128.0f), new Vector3(9.2f, 0f, 141.8f), new Vector3(-9.2f, 0f, 141.8f) };
        static readonly string[] BayNames = { "A", "B", "C", "D" };

        void BuildArena(LevelBuildContext ctx)
        {
            var g = ctx.Section("Arena", Vector3.zero);
            var d = ctx.Decor;
            var c = ArenaCenter;
            var arena = BossKit.Arena(ctx.Gameplay, c, new Vector2(30f, 30f), 0f, false);
            ctx.Mark("boss.arena", arena.gameObject);
            ctx.Platform(g, new Vector3(0f, 0f, 137.05f), new Vector3(31.2f, 0.6f, 31.1f), MatKey.Floor, true, "ArenaFloor");
            // ---- walls (6 m): south opening x -6..6 from the dock, north door x -2.3..2.3, west breach z 133..141
            float h = ArenaWallH;
            Wall(g, -15.6f, 121.7f, -6f, 121.7f, h);
            Wall(g, 6f, 121.7f, 15.6f, 121.7f, h);
            Wall(g, -15.6f, 152.6f, -2.8f, 152.6f, h);
            Wall(g, 2.8f, 152.6f, 15.6f, 152.6f, h);
            Wall(g, 15.6f, 121.7f, 15.6f, 152.6f, h, 0f, MatKey.WallAlt);
            Wall(g, -15.6f, 121.7f, -15.6f, 133f, h, 0f, MatKey.WallAlt);
            Wall(g, -15.6f, 141f, -15.6f, 152.6f, h, 0f, MatKey.WallAlt);
            var breach = Wall(g, -15.6f, 133f, -15.6f, 141f, h, 0f, MatKey.Concrete);
            breach.name = "WestBreach";
            Sign(d, new Vector3(-15.35f, 3.4f, 137f), Vector3.left, "STAFF ONLY\n(PIGS ESPECIALLY)", 3f, 1.2f, SignStyle.Warning);
            // Porkchop's pen behind the breach
            Floor(g, -20f, -15.6f, 133f, 141f, 0f, MatKey.Concrete);
            Wall(g, -20f, 133f, -15.8f, 133f, h); Wall(g, -20f, 141f, -15.8f, 141f, h); Wall(g, -20f, 133f, -20f, 141f, h);
            Sign(d, new Vector3(-19.75f, 3f, 137f), Vector3.left, "PORKCHOP'S PEN\nFEEDING: NEVER", 3f, 1.2f, SignStyle.Danger);
            Kit.BoxVisual(d, "DockHeader", new Vector3(0f, 5.6f, 121.7f), new Vector3(12.4f, 0.8f, 0.4f), MatKey.MetalDark, 0.03f);
            Sign(d, new Vector3(0f, 5.6f, 121.45f), Vector3.forward, "LOADING DOCK — MIND THE HAM", 6f, 0.75f, SignStyle.Warning);
            Reveal(d, new Vector3(0f, 3.2f, 123f), PerspectiveMask.Intellect, "CLUE: PORKCHOP ONLY SEES WHO YOU CONTROL");

            // ---- pillars: CHARGE HERE (PLEASE)
            for (int i = 0; i < PillarPos.Length; i++)
            {
                var p = PillarPos[i];
                EnvKit.Decor.Bank.MarbleColumn(g, p, null, h, 2f, false, true);
                Kit.BoxVisual(d, "ChargeStripes", p + Vector3.up * 1.25f, new Vector3(2.06f, 0.5f, 2.06f), MatKey.HazardStripes, 0.01f);
                var nz = new Vector3(0f, 0f, Mathf.Sign(c.z - p.z));
                var nx = new Vector3(Mathf.Sign(c.x - p.x), 0f, 0f);
                EnvKit.Sign(d, p + nz * 1.04f + Vector3.up * 2.6f, EnvKit.WallMountFacing(nz), "REINFORCED PILLAR\nCHARGE HERE (PLEASE)", new Vector2(1.9f, 0.9f), SignStyle.Warning);
                EnvKit.Sign(d, p + nx * 1.04f + Vector3.up * 2.6f, EnvKit.WallMountFacing(nx), "PIGS HATE\nPILLARS", new Vector2(1.6f, 0.8f), SignStyle.Info);
                ctx.MarkPoint("pillar." + i, p);
                // bait spot decal behind the pillar (as seen from the centre)
                var b = BaitPos[i];
                PuzzleVisuals.Cyl(d, "BaitSpot", b + Vector3.up * 0.31f, 0.55f, 0.02f, MatKey.HazardYellow, null, 20, false);
                ctx.Label(d, b + new Vector3(0f, 0.34f, 0f), Quaternion.Euler(90f, 0f, 0f), "BAIT\nHERE", 0.26f, Ink, 1f);
            }
            for (int i = 0; i < 3; i++) { ctx.MarkPoint("p" + (i + 1) + ".bait", BaitPos[i]); ctx.MarkPoint("p" + (i + 1) + ".side", SidePos[i]); }

            // ---- loading bays (safe zones, raised 1.6 m, stairs on their inner side) + C4 / C5 / C6
            for (int i = 0; i < BayPos.Length; i++)
            {
                var bp = BayPos[i];
                Floor(g, bp.x - 3.5f, bp.x + 3.5f, bp.z - 2.5f, bp.z + 2.5f, BayTop, MatKey.FloorAlt, BayTop);
                BossKit.BreakArea(arena.transform, new Vector3(bp.x, BayTop, bp.z) - c, new Vector2(7f, 5f));
                bool south = bp.z < c.z;
                float sx = Mathf.Sign(bp.x) * 13.25f;
                EnvKit.Stairs(g, new Vector3(sx, 0f, south ? 130.2f : 143.8f), south ? Vector3.back : Vector3.forward, 2.5f, 0.2f, 0.4f, 8);
                ctx.Mark("boss.safe." + i, ctx.MarkPoint("bay." + i, bp).gameObject);
                ctx.MarkPoint("park." + i, new Vector3(Mathf.Sign(bp.x) * 11f, 0f, south ? 131.5f : 142.5f));
                var wallZ = south ? 121.95f : 152.35f;
                Sign(d, new Vector3(bp.x, BayTop + 2.2f, wallZ), south ? Vector3.back : Vector3.forward,
                     "LOADING BAY " + BayNames[i] + "\nPIGS CAN'T CLIMB", 3.2f, 1.1f, SignStyle.Info);
            }
            ctx.Checkpoint(BayPos[0], 0f);      // C4 (P1)
            ctx.Checkpoint(BayPos[1], 0f);      // C5 (P2)
            ctx.Checkpoint(BayPos[2], 180f);    // C6 (P3)

            // ---- PORKCHOP
            var root = Kit.Make("Porkchop", arena.transform, Vector3.zero, Yaw(180f));
            var rb = root.AddComponent<Rigidbody>();
            rb.isKinematic = true; rb.useGravity = false;
            var boss = root.AddComponent<L9PorkchopBoss>();
            var model = BossModels.Porkchop(root.transform, Vector3.zero, Quaternion.identity, false);
            var hb = model.gameObject.AddComponent<BoxCollider>();
            hb.center = new Vector3(0f, 2.05f, 0f); hb.size = new Vector3(1.7f, 2.3f, 3.2f);   // narrower than the flanks: the belly plug sticks out of it
            boss.Model = model; boss.ModelRoot = model.transform; boss.Body = root.transform; boss.Arena = arena;
            boss.Center = c;
            boss.Pillars = (Vector3[])PillarPos.Clone();
            boss.BurstFrom = new Vector3(-17.8f, 0f, 137f);
            boss.WestBreach = breach;
            var plugNode = model.Part("BellyPlug");
            var snoutNode = model.Part("Snout");
            boss.Plug = BossKit.WeakPointShoot(plugNode != null ? plugNode : model.transform, Vector3.zero, 0.5f, boss, 1 << 0);
            boss.Snout = BossKit.WeakPointPunch(snoutNode != null ? snoutNode : model.transform, new Vector3(0f, 0f, 0.3f), boss, 1 << 1, 7);
            // polish 11-17: the kit floats "PUNCH HERE" above the plate, where it read mirrored whenever Porkchop turned away
            // (sign sweep); it now sits on the plate face, which hides it from behind.
            var snoutTxt = boss.Snout != null ? boss.Snout.transform.Find("Label") : null;
            var snoutTmp = snoutTxt != null ? snoutTxt.GetComponent<TMPro.TextMeshPro>() : null;
            if (snoutTmp != null)
            {
                snoutTxt.localPosition = new Vector3(0f, 0f, 0.14f);
                snoutTmp.text = "PUNCH\nHERE";
                snoutTmp.color = new Color32(21, 22, 26, 255);
                snoutTmp.enableAutoSizing = true; snoutTmp.fontSizeMin = 0.5f; snoutTmp.fontSizeMax = 2.6f;
                snoutTmp.lineSpacing = -18f;
                snoutTmp.rectTransform.sizeDelta = new Vector2(0.9f, 0.7f);
            }
            var earT = Kit.Make("EarPortTerminal", model.transform, new Vector3(2.45f, 0f, 0.7f), Yaw(90f));
            boss.Ear = BossKit.WeakPointHack(earT.transform, Vector3.zero, Quaternion.identity, boss, 1 << 2);
            boss.EarTerminal = earT.transform;
            var port = model.Part("EarPort");
            if (port != null)
            {
                var a = model.transform.InverseTransformPoint(earT.transform.position + Vector3.up * 1.3f);
                var e = model.transform.InverseTransformPoint(port.position);
                Kit.BoxVisual(model.transform, "EarCable", (a + e) * 0.5f, new Vector3(0.07f, 0.07f, Vector3.Distance(a, e)), MatKey.Rubber, 0.01f, Quaternion.LookRotation(e - a), false);
            }
            Reveal(earT.transform, new Vector3(0f, 2.1f, 0f), PerspectiveMask.Intellect, "EAR PORT — HACK WHEN DIZZY (IQ 7)");
            Reveal(snoutNode != null ? snoutNode : model.transform, new Vector3(0f, 1.0f, 0.4f), PerspectiveMask.Strength, "CRACKED SNOUT — PUNCH");
            Reveal(plugNode != null ? plugNode : model.transform, new Vector3(0f, 0.8f, 0.2f), PerspectiveMask.Weapon, "BELLY PLUG — SHOOT");
            Reveal(model.transform, new Vector3(0f, 4.4f, 0f), PerspectiveMask.Speed, "HAM CHARGE: 10 M/S AFTER THE TELL");
            boss.Configure();
            ctx.Mark("boss", root);
            ctx.Mark("boss.weak.plug", boss.Plug.gameObject);
            ctx.Mark("boss.weak.snout", boss.Snout.gameObject);
            ctx.Mark("boss.weak.ear", boss.Ear.gameObject);

            ctx.Tutorial(new Vector3(0f, 1.5f, 124.6f), new Vector3(12f, 3f, 5f), "ch08_bait", "BAIT AND SWITCH",
                "Porkchop charges whoever you control. Stand in front of a pillar, then step aside.", new[] { "TAB", "E" }, false);
            ctx.Hint(new Vector3(0f, 2f, 137f), new Vector3(30f, 4f, 30f),
                "Bait it into a pillar, then hit the weak spot for this phase: belly (gun), snout (Brute), ear (Brainiac).", "Pigs hate pillars.");

            // ---- dressing: ME-TV, hall light, poster
            var tv = StoryKit.MeTvScreen(d, new Vector3(-8.5f, 4.3f, 152.3f), Look(Vector3.forward), 3.6f);
            ctx.Mark("story.metv2", tv);
            Panel(d, new Vector3(8f, 5.2f, 152.35f), Vector3.forward, Gold, true, 2.6f, 22f);
            Panel(d, new Vector3(-15.35f, 5f, 128f), Vector3.left, AlarmRed);
            Panel(d, new Vector3(15.35f, 5f, 146f), Vector3.right, AlarmRed);
            StoryKit.HoloPoster(d, new Vector3(5.5f, 0f, 151.2f), Look(Vector3.forward), StoryKitLines.Poster(7), 2);
            EnvKit.Decor.Bank.CoinPile(d, new Vector3(-13f, BayTop, 151f), null, 1f, 0.5f, 5);
            EnvKit.Decor.Bank.CoinPile(d, new Vector3(13.5f, BayTop, 123f), null, 0.8f, 0.4f, 6);

            // ---- the exit: north door (opens when the bank is broken) -> getaway van -> LevelExit
            var door = PuzzleKit.Door(ctx.Puzzles, new Vector3(0f, 0f, 152.6f), null, DoorMode.SlideUp, 4.6f, 4.2f, boss);
            ctx.Mark("arena.door", door.gameObject);
            Floor(g, -4.5f, 4.5f, 152.9f, 182f, 0f, MatKey.Concrete);
            ctx.Exit(new Vector3(0f, 0f, 153.4f), 0f);
            var exhibit = StoryKit.ExhibitProp(ctx.Props, new Vector3(0f, 0f, 146f), "B");
            boss.Exhibit = exhibit;
            ctx.Mark("exhibit.b", exhibit);
            boss.Van = BuildVan(d);
            EnvKit.VoidBackdrop(ctx.Decor, LevelTheme.Bank, new Bounds(new Vector3(0f, 0f, 85f), new Vector3(60f, 20f, 200f)), 9);
        }

        L9Van BuildVan(Transform d)
        {
            var root = Kit.Make("GetawayVan", d, new Vector3(0f, 0f, 178f));
            var t = root.transform;
            Kit.BoxVisual(t, "Body", new Vector3(0f, 1.85f, 3.2f), new Vector3(3.1f, 2.7f, 6.2f), MatKey.SignWhite, 0.12f);
            Kit.BoxVisual(t, "Stripe", new Vector3(0f, 1.3f, 3.2f), new Vector3(3.14f, 0.35f, 6.1f), MatKey.SafetyOrange, 0.02f);
            Kit.BoxVisual(t, "Cab", new Vector3(0f, 1.55f, 7.2f), new Vector3(2.9f, 2.1f, 1.9f), MatKey.SafetyOrange, 0.15f);
            Kit.BoxVisual(t, "Windshield", new Vector3(0f, 2.1f, 8.12f), new Vector3(2.5f, 0.8f, 0.05f), MatKey.Glass, 0.01f, null, false);
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = 0; sz < 2; sz++)
                    Kit.BoxVisual(t, "Wheel", new Vector3(sx * 1.45f, 0.45f, sz == 0 ? 1.2f : 6.4f), new Vector3(0.4f, 0.9f, 0.9f), MatKey.Rubber, 0.2f);
            Kit.BoxVisual(t, "BackDoors", new Vector3(0f, 1.85f, 0.07f), new Vector3(2.9f, 2.5f, 0.06f), MatKey.MetalLight, 0.02f, null, false);
            _ctx.Label(t, new Vector3(0f, 2.3f, 0.02f), Quaternion.identity, "PERCY'S PERCOLATED\nDELIVERIES", 0.3f, Ink, 2.6f);
            _ctx.Label(t, new Vector3(0f, 1.3f, 0.02f), Quaternion.identity, "DEFINITELY NOT A GETAWAY VAN", 0.16f, Ink, 2.6f);
            _ctx.Label(t, new Vector3(1.57f, 2.2f, 3.2f), Look(Vector3.left), "PERCY'S PERCOLATED DELIVERIES\nWE BREW. YOU GO.", 0.3f, Ink, 5.6f);
            _ctx.Label(t, new Vector3(-1.57f, 2.2f, 3.2f), Look(Vector3.right), "PERCY'S PERCOLATED DELIVERIES\nWE BREW. YOU GO.", 0.3f, Ink, 5.6f);
            var van = root.AddComponent<L9Van>();
            van.From = new Vector3(0f, 0f, 178f);
            van.To = new Vector3(0f, 0f, 158.2f);
            return van;
        }
    }
}
