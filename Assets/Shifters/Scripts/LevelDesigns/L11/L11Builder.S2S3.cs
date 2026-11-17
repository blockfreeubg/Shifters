// =====================================================================================
//  SHIFTERS - L11Builder.S2S3.cs   [SHIFT 10]  S2 the graves + ghost bridge, S3 the mausoleum, backdrop
// =====================================================================================
using System.Collections.Generic;
using UnityEngine;

namespace Shifters
{
    public partial class L11Builder
    {
        // ================================================================== S2 THE GRAVES
        void BuildS2(LevelBuildContext ctx)
        {
            var s2 = ctx.Section("S2_TheGraves", Vector3.zero);
            float ph = PathHalf;

            // lawns either side (top y 0, 2.5 m thick = the pit side walls), far bank widened for C2
            Slab(s2, -12f, -ph, 22f, GapFar, 0f, PitDepth, MatKey.Foliage, false, "LawnW");
            Slab(s2, ph, 12f, 22f, GapFar, 0f, PitDepth, MatKey.Foliage, false, "LawnE");
            Slab(s2, -12f, -4f, GapFar, ZoneZ1, 0f, PitDepth, MatKey.Foliage, false, "LawnFarW");
            Slab(s2, 4f, 12f, GapFar, ZoneZ1, 0f, PitDepth, MatKey.Foliage, false, "LawnFarE");
            // the path, broken by three graves and the bridge gap
            var segs = new List<Vector2> { new Vector2(22f, GraveZ[0] - GraveLen * 0.5f) };
            for (int i = 0; i < GraveZ.Length; i++)
            {
                float z1 = GraveZ[i] + GraveLen * 0.5f;
                float z2 = i + 1 < GraveZ.Length ? GraveZ[i + 1] - GraveLen * 0.5f : GapNear;
                segs.Add(new Vector2(z1, z2));
            }
            foreach (var sg in segs) Slab(s2, -ph, ph, sg.x, sg.y, 0f, PitDepth, MatKey.Concrete, true, "Path");
            Slab(s2, -4f, 4f, GapFar, ZoneZ1, 0f, PitDepth, MatKey.Concrete, true, "FarBank");
            // pit floor: soft foam (shown under every grave and the bridge gap)
            Slab(s2, -ph, ph, 22f, GapFar, -PitDepth, 0.5f, MatKey.Rubber, false, "FoamFloor");

            // fences along the path (3 m, unclimbable), then out around the far bank
            Fence(s2, -ph - 0.1f, 22f, -ph - 0.1f, GapFar); Fence(s2, ph + 0.1f, 22f, ph + 0.1f, GapFar);
            Fence(s2, -ph - 0.1f, GapFar, -4f, GapFar); Fence(s2, ph + 0.1f, GapFar, 4f, GapFar);
            Fence(s2, -4f, GapFar, -4f, ZoneZ1); Fence(s2, 4f, GapFar, 4f, ZoneZ1);

            ctx.MarkPoint("s2.start", new Vector3(0f, 0f, 23.2f));
            L11Kit.Arch(s2, new Vector3(0f, 0f, 22.4f), ph + 0.15f, 4.3f, "MIND THE GAP.\n<size=70%>AND THE GRAVE.</size>", new Vector2(4.4f, 1.1f), SignStyle.Warning);
            EnvKit.Sign(s2, new Vector3(-ph - 0.05f, 2f, 23.6f), LookWest, "PLEASE DO NOT DISTURB\nTHE DISBANDED", new Vector2(2.6f, 0.8f), SignStyle.Info);

            // ---- three open graves
            var crumbles = new List<CrumblingPlatform>();
            for (int i = 0; i < GraveZ.Length; i++)
            {
                float zc = GraveZ[i], z0 = zc - GraveLen * 0.5f, z1 = zc + GraveLen * 0.5f;
                var tile = PuzzleKit.CrumblingPlatform(ctx.Hazards, new Vector3(0f, 0f, zc), Quaternion.identity, new Vector3(ph * 2f - 0.04f, 0.3f, GraveLen - 0.06f));
                tile.name = "FreshSoil_" + (i + 1);
                tile.ShakeTime = 0.18f; tile.RespawnTime = 3f;
                if (tile.Visual != null)
                {
                    var soil = MaterialLibrary.Colored(L11Kit.SoilColor, 0.05f);
                    foreach (var r in tile.Visual.GetComponentsInChildren<Renderer>(true)) r.sharedMaterial = soil;
                    ctx.Label(tile.Visual, new Vector3(0f, 0.17f, 0f), EnvKit.ViewerLooking(Vector3.down, Vector3.forward), "FRESH SOIL", 0.34f, new Color(0.75f, 0.62f, 0.45f), 4f);
                }
                crumbles.Add(tile);
                ctx.Mark("s2.grave" + (i + 1), tile.gameObject);
                PuzzleKit.FoamPit(ctx.Hazards, new Vector3(0f, -PitDepth + 0.5f, zc), null, new Vector3(ph * 2f, 1f, GraveLen), new Vector3(0f, 0f, 23.2f));
                EdgeGlowX(s2, -ph, ph, z0 - 0.07f); EdgeGlowX(s2, -ph, ph, z1 + 0.07f);
                EnvKit.Sign(s2, new Vector3(ph + 0.05f, 1.4f, zc), LookEast, "SHALLOW GRAVE\n<size=70%>(SOFT)</size>", new Vector2(1.6f, 0.7f), SignStyle.Warning);
                for (int l = 0; l < 3; l++)
                {
                    ctx.MarkPoint("s2.g" + (i + 1) + ".takeoff." + Lane[l], new Vector3(LaneX[l], 0f, z0 - 0.45f));
                    ctx.MarkPoint("s2.g" + (i + 1) + ".landing." + Lane[l], new Vector3(LaneX[l], 0f, z1 + 0.8f));
                }
            }

            // ---- handshake hands (Spikes reskin) on 0.5 m edge strips
            var hands = new List<Spikes>();
            for (int s = -1; s <= 1; s += 2)
            {
                var sp = PuzzleKit.Spikes(ctx.Hazards, new Vector3(s * (ph - 0.25f), 0f, (HandsZ0 + HandsZ1) * 0.5f), Quaternion.identity,
                    new Vector2(0.5f, HandsZ1 - HandsZ0), 1.5f, 2f, s < 0 ? 0f : 1.75f);
                sp.name = "HandshakeHands";
                sp.Damage = 10f;
                L11Kit.HandshakeSkin(sp, HandsZ1 - HandsZ0);
                hands.Add(sp);
                ctx.Mark(s < 0 ? "s2.handsW" : "s2.handsE", sp.gameObject);
            }
            var hfx = Kit.Make("L11HandshakeFx", s2, new Vector3(0f, 0f, 42.5f)).AddComponent<L11HandshakeFx>();
            hfx.Hands = hands.ToArray();
            L11Kit.Arch(s2, new Vector3(0f, 0f, 38.6f), ph + 0.15f, 4.3f, "FIRM HANDSHAKES AHEAD", new Vector2(4.4f, 0.8f), SignStyle.Warning);

            // ---- THE GHOST BRIDGE
            var zone = PuzzleKit.ZoneTrigger(ctx.Puzzles, new Vector3(0f, -0.5f, (ZoneZ0 + ZoneZ1) * 0.5f), null, new Vector3(9f, 4f, ZoneZ1 - ZoneZ0),
                ZoneFilter.WholeTeam, 1, false, "");
            zone.name = "GhostWatchZone";
            ctx.Mark("s2.ghostZone", zone.gameObject);
            var bridge = L11Kit.GhostBridge(ctx.Puzzles, new Vector3(0f, -0.02f, GapNear), GapFar - GapNear, 4f, zone);
            bridge.OffDelay = 1.5f;
            ctx.Mark("s2.ghostBridge", bridge.gameObject);
            PuzzleKit.FoamPit(ctx.Hazards, new Vector3(0f, -PitDepth + 0.5f, (GapNear + GapFar) * 0.5f), null, new Vector3(ph * 2f, 1f, GapFar - GapNear), new Vector3(0f, 0f, 43.4f));
            EdgeGlowX(s2, -ph, ph, GapNear - 0.07f, MatKey.GhostGlow); EdgeGlowX(s2, -4f, 4f, GapFar + 0.07f, MatKey.GhostGlow);
            EdgeGlowX(s2, -ph, ph, ZoneZ0, MatKey.GhostGlow);
            ctx.Label(s2, new Vector3(0f, 0.03f, ZoneZ0 + 0.5f), EnvKit.ViewerLooking(Vector3.down, Vector3.forward), "GHOST WATCH ZONE", 0.34f, L11Kit.GhostBlue, 4.5f);
            EnvKit.Sign(s2, new Vector3(ph + 0.05f, 2.2f, 44.2f), LookEast, "GHOST BRIDGE\n<size=70%>SERVICE FOR GROUPS ONLY</size>", new Vector2(2.4f, 0.9f), SignStyle.Info);
            ctx.MarkPoint("s2.bridgeNear", new Vector3(0f, 0f, 43.4f));
            for (int l = 0; l < 3; l++)
            {
                ctx.MarkPoint("s2.lane." + Lane[l] + ".near", new Vector3(BridgeLaneX[l], 0f, GapNear - 0.7f));
                ctx.MarkPoint("s2.farBank." + Lane[l], new Vector3(BridgeLaneX[l], 0f, GapFar + 1.8f));
            }
            var trioW = StoryKit.GhostTrio(s2, new Vector3(-4.7f, 0f, 48.5f), LookWest, new[] { "MARKETING", "IT", "BOWLING LEAGUE" });
            var trioE = StoryKit.GhostTrio(s2, new Vector3(4.7f, 0f, 48.5f), LookEast, new[] { "THE BAND", "GROUP CHAT", "TUG-OF-WAR" });
            ctx.Mark("s2.ghostsW", trioW); ctx.Mark("s2.ghostsE", trioE);
            var watch = Kit.Make("L11GhostWatch", s2, new Vector3(0f, 0f, 48.5f)).AddComponent<L11GhostWatch>();
            watch.Zone = zone; watch.Bridge = bridge; watch.NearEdgeZ = GapNear;
            watch.Trios = new[] { trioW.GetComponentInChildren<GhostTrio>(true), trioE.GetComponentInChildren<GhostTrio>(true) };
            _bridgeTrios = watch.Trios;
            PerspectiveKit.Clue(ctx.Puzzles, new Vector3(-1.6f, 1.8f, 44.8f), Quaternion.identity, "THE GHOSTS ONLY HELP TEAMS. STAY CLOSE.", 0.7f);
            PuzzleKit.LightPool(ctx.Puzzles, new Vector3(0f, 0f, 43.2f), 2.2f, false);

            ctx.Tutorial(new Vector3(0f, 1.5f, 41.8f), new Vector3(5f, 3f, 3.4f), "ch10_ghosts", "THE GHOSTS OF TEAMS PAST",
                "The Ghosts of Teams Past only help teams: keep all three near the bridge until everyone's across.", null, true);
            ctx.Hint(new Vector3(0f, 2f, 39f), new Vector3(8f, 4f, 34f),
                "Jump the graves. At the big gap, keep everyone near the edge so the ghosts build the bridge, then cross one by one and stay close.",
                "Don't wander off. The ghosts are watching.");
            ctx.Checkpoint(new Vector3(0f, 0f, 55f), 0f);

            // ---- dressing: tombstone rows on the lawns, lamp posts, a poster
            for (int s = -1; s <= 1; s += 2)
            {
                var face = s < 0 ? LookWest : LookEast;
                int k = 0;
                for (float z = 24.2f; z < 50f; z += 3.3f, k++)
                {
                    Grave(s2, new Vector3(s * 4.1f, 0f, z), face);
                    Grave(s2, new Vector3(s * 6.6f, 0f, z + 1.6f), face);
                    if (k % 3 == 1) Grave(s2, new Vector3(s * 9.2f, 0f, z + 0.8f), face);
                }
                L11Kit.LampPost(s2, new Vector3(s * 3.0f, 0f, s < 0 ? 29f : 34f));
                L11Kit.LampPost(s2, new Vector3(s * 4.6f, 0f, 56.2f));
            }
            StoryKit.HoloPoster(s2, new Vector3(-3.4f, 0f, 33.4f), LookWest, "TWO'S A CROWD. THREE'S A CRIME.", 3);
        }

        GhostTrio[] _bridgeTrios;

        // ================================================================== S3 THE MAUSOLEUM
        void BuildS3(LevelBuildContext ctx)
        {
            var s3 = ctx.Section("S3_TheMausoleum", Vector3.zero);
            float mz = MausoleumZ, md = MausoleumDepth;
            Slab(s3, -6f, 6f, PlazaZ0, mz, 0f, 0.6f, MatKey.Concrete, true, "Plaza");
            Slab(s3, -5.4f, 5.4f, mz, mz + md, 0f, 0.6f, MatKey.Concrete, false, "HallFloor");
            Fence(s3, -6f, PlazaZ0, -6f, mz); Fence(s3, 6f, PlazaZ0, 6f, mz);
            Fence(s3, -4f, PlazaZ0, -6f, PlazaZ0); Fence(s3, 4f, PlazaZ0, 6f, PlazaZ0);

            // the RECENTLY REPRINTED row (fills from the campaign's LostLog at runtime)
            var row = StoryKit.TombstoneRow(s3, new Vector3(-5.0f, 0f, 60.6f), LookWest, 6);
            ctx.Mark("s3.ownGrave", row);
            var rowView = row.GetComponent<TombstoneRowView>();
            EnvKit.Sign(s3, new Vector3(-5.95f, 2.4f, 60.6f), LookWest, "RECENTLY REPRINTED", new Vector2(3.0f, 0.6f), SignStyle.Info);
            var own = Kit.Make("L11OwnGrave", s3, new Vector3(-5f, 0f, 60.6f)).AddComponent<L11OwnGrave>();
            own.Row = rowView;
            if (rowView != null && rowView.Texts != null && rowView.Texts.Length >= 2)
            {   // edit-mode defaults (replaced from the LostLog at Start when the campaign has Lost interns)
                if (rowView.Texts[0] != null) rowView.Texts[0].text = "RESERVED FOR YOU.\n- MANAGEMENT";
                if (rowView.Texts[1] != null) rowView.Texts[1].text = "HERE LIES NOBODY.\nGOOD JOB.\n- PERCY";
            }

            // the mausoleum
            var mau = L11Kit.Mausoleum(s3, new Vector3(0f, 0f, mz), 5.5f, md, 6.6f, "ME-MORIAL");
            ctx.Mark("s3.mausoleumBuilding", mau);
            var doors = mau.AddComponent<L11MausoleumDoors>();
            doors.DoorL = mau.transform.Find("DoorL"); doors.DoorR = mau.transform.Find("DoorR"); doors.OpenZ = PlazaZ0 + 0.5f;
            EnvKit.Sign(s3, new Vector3(0f, 5.55f, mz - 0.3f), Quaternion.identity, "THE TEAM-BUILDING RETREAT, 1999.\n<size=75%>WE DON'T TALK ABOUT IT.</size>", new Vector2(4.2f, 1.0f), SignStyle.Info);

            // C09 tableau: young Max (intern number zero) falling backwards; three interns facing away toward the coffee
            var faller = StoryKit.VillainStatue(s3, new Vector3(-3.6f, 0f, mz + 1.7f), null, 1.45f, 0);
            var fig = faller.transform.Find("Figure");
            if (fig != null)
            {
                fig.localRotation = Quaternion.Euler(-22f, 0f, 0f);
                foreach (var r in fig.GetComponentsInChildren<Renderer>(true)) if (r.sharedMaterial == StoryKit.StatueStone) r.sharedMaterial = L11Kit.MoonMarble;
            }
            foreach (var tmp in faller.GetComponentsInChildren<TMPro.TextMeshPro>(true))
                if (fig == null || !tmp.transform.IsChildOf(fig)) tmp.text = "INTERN No. 0\nTHE TRUST FALL";
            ctx.Mark("s3.statueFaller", faller);
            for (int i = 0; i < 3; i++)
                L11Kit.InternStatue(s3, new Vector3(-4.5f + 0.9f * i, 0f, mz + 4.0f), Quaternion.identity, 0.95f + 0.05f * (i % 2));
            L11Kit.CoffeeStatue(s3, new Vector3(-3.6f, 0f, mz + 5.4f), Yaw(180f));
            ctx.MarkPoint("story.statue", new Vector3(-3.6f, 1.2f, mz + 3.2f));
            ctx.MarkPoint("story.mausoleum", new Vector3(0.9f, 2.3f, mz + 0.8f));
            EnvKit.Sign(s3, new Vector3(4.95f, 2.2f, mz + 2.6f), LookEast, "TEAM-BUILDING RETREAT, 1999.\nTHE TRUST FALL.\nNOBODY CAUGHT HIM.", new Vector2(2.4f, 1.3f), SignStyle.Info);
            EnvKit.Sign(s3, new Vector3(3.9f, 3.0f, mz + md - 0.3f), Quaternion.identity, "CRYPT TUNNEL\n<size=70%>>> HQ ATRIUM</size>", new Vector2(2.0f, 1.0f), SignStyle.Exit);
            for (int i = 0; i < 5; i++)
                ProcMesh.Part("Candle", s3, ProcMesh.Cached("l11_candle", () => ProcMesh.Cylinder(0.05f, 0.3f, 8, true)), Kit.Mat(MatKey.LightOn),
                    new Vector3(4.6f, 0.15f, mz + 1.2f + i * 0.6f), null, null, false);
            PuzzleKit.LightPool(ctx.Puzzles, new Vector3(0f, 0f, mz + 2.4f), 2.2f, false);

            var trig = StoryKit.ScriptTrigger(s3, new Vector3(0f, 1.5f, mz + 1.2f), new Vector3(4.6f, 3f, 2.4f), "ch10.mausoleum", true);
            ctx.Mark("s3.mausoleum", trig.gameObject);
            ctx.MarkPoint("s3.door", new Vector3(0f, 0f, mz - 1.2f));

            // GUN bonus: the hidden bell on the roof corner (shootable from the plaza and from C2)
            var belfry = Kit.Make("Belfry", s3, new Vector3(4.4f, 6.8f, mz + 0.4f)).transform;
            for (int s = -1; s <= 1; s += 2)
                Kit.BoxVisual(belfry, "Post", new Vector3(s * 0.55f, 1.0f, 0f), new Vector3(0.14f, 2.0f, 0.14f), MatKey.Concrete, 0.02f);
            Kit.BoxVisual(belfry, "Cap", new Vector3(0f, 2.05f, 0f), new Vector3(1.4f, 0.16f, 0.5f), MatKey.Concrete, 0.02f);
            var bell = PuzzleKit.ShootTarget(belfry, new Vector3(0f, 1.25f, 0f), Yaw(180f), ShootTargetMode.Momentary, 2f, false, "");
            bell.name = "HiddenBell";
            ProcMesh.Part("BellShell", bell.transform, ProcMesh.Cached("l11_bell", () => ProcMesh.Cone(0.32f, 0.5f, 14)), Kit.Mat(MatKey.Gold), new Vector3(0f, 0.05f, 0f), null, null, false);
            ctx.Mark("s3.bell", bell.gameObject);
            var trioQ = StoryKit.GhostTrio(s3, new Vector3(4.4f, 0f, 60.8f), LookEast, new[] { "SUGGESTION BOX", "HIGH-FIVE", "3-LEGGED RACE" });
            var chord = Kit.Make("L11BellChord", s3, new Vector3(4.4f, 0f, 60.8f)).AddComponent<L11BellChord>();
            chord.Bell = bell;
            var all = new List<GhostTrio>();
            if (_bridgeTrios != null) all.AddRange(_bridgeTrios);
            all.Add(trioQ.GetComponentInChildren<GhostTrio>(true));
            chord.Trios = all.ToArray();
            StoryKit.HoloPoster(s3, new Vector3(5.3f, 0f, 58.2f), LookEast, "SOLO IS THE NEW TOGETHER.", 3);

            ctx.Exit(new Vector3(0f, 0f, ExitZ), 0f);
        }

        // ================================================================== backdrop
        void BuildBackdrop(LevelBuildContext ctx)
        {
            var d = ctx.Decor;
            L11Kit.Moon(d, new Vector3(-70f, 62f, 170f), 9f);
            int seed = 0;
            for (int s = -1; s <= 1; s += 2)
            {
                for (float z = 24f; z < 52f; z += 7.5f) L11Kit.DeadTree(d, new Vector3(s * (10.5f + (seed % 2)), 0f, z + seed % 3), 4.6f + seed % 3 * 0.6f, ++seed);
                L11Kit.DeadTree(d, new Vector3(s * 11f, -0.3f, 0f), 5.2f, ++seed);
                L11Kit.DeadTree(d, new Vector3(s * 9.5f, -0.3f, 64f), 5.8f, ++seed);
            }
            // outer ground so the fog has something to sit on
            // (never over the pits: strips outside the playable slabs only)
            for (int s = -1; s <= 1; s += 2)
            {
                Kit.BoxVisual(d, "OuterGround", new Vector3(s * 24f, -0.35f, -1.5f), new Vector3(32f, 0.2f, 47f), MatKey.Foliage, 0f, null, false);
                Kit.BoxVisual(d, "OuterGround", new Vector3(s * 26f, -0.35f, 53.5f), new Vector3(28f, 0.2f, 63f), MatKey.Foliage, 0f, null, false);
                Kit.BoxVisual(d, "OuterGround", new Vector3(s * 6f, -0.35f, 18.5f), new Vector3(4f, 0.2f, 7f), MatKey.Foliage, 0f, null, false);
                Kit.BoxVisual(d, "OuterGround", new Vector3(s * 9f, -0.35f, 61f), new Vector3(6f, 0.2f, 8f), MatKey.Foliage, 0f, null, false);
            }
            Kit.BoxVisual(d, "OuterGround", new Vector3(0f, -0.35f, 86f), new Vector3(24f, 0.2f, 29f), MatKey.Foliage, 0f, null, false);
            Kit.BoxVisual(d, "OuterGround", new Vector3(0f, -0.35f, -20f), new Vector3(16f, 0.2f, 19f), MatKey.Foliage, 0f, null, false);
            // distant rows of grey stones
            for (int s = -1; s <= 1; s += 2)
                for (int i = 0; i < 6; i++)
                    Grave(d, new Vector3(s * (15f + (i % 2) * 2.5f), -0.25f, 8f + i * 9f), s < 0 ? LookWest : LookEast);
        }
    }
}
