// SHIFTERS - L9Builder.Chase.cs   [LEVEL 9]  the coin plant hall, S0 tram platform, the ChaseTrack and its three stations
using UnityEngine;

namespace Shifters
{
    public partial class L9Builder
    {
        // ================================================================== HALL + COIN RIVER
        void BuildHall(LevelBuildContext ctx)
        {
            var g = ctx.Section("Hall", Vector3.zero);
            var d = ctx.Decor;
            // the coin river: one glowing gold sheet with coin piles, a rail bed down the middle (visual only)
            Kit.BoxVisual(d, "CoinRiver", new Vector3(0f, RiverY, 55f), new Vector3(32f, 0.1f, 136f), MatKey.Gold, 0f, null, false);
            Kit.BoxVisual(d, "RiverBed", new Vector3(0f, RiverY - 0.6f, 55f), new Vector3(32.2f, 1f, 136.2f), MatKey.MetalDark, 0f, null, false);
            for (int i = 0; i < 14; i++)
            {
                float z = -8f + i * 9.3f;
                float x = (i % 2 == 0 ? -1f : 1f) * (5.5f + (i * 37 % 7));
                if (z > 80f && z < 92f && x > 0f) x += 4f;   // keep the pylon jump clear
                EnvKit.Decor.Bank.CoinPile(d, new Vector3(x, RiverY + 0.02f, z), Yaw(i * 47f), 1.2f + (i % 3) * 0.4f, 0.55f, i);
            }
            // rail bed + rails (the pig trots on it); broken by the gorge z 92..102
            RailBed(d, -45f, 92f);
            RailBed(d, 102f, EndZ + 3.6f);
            var river = new GameObject("CoinRiver_Bounce").AddComponent<L9CoinRiver>();
            river.transform.SetParent(ctx.Hazards, false);
            river.Area = new Bounds(new Vector3(0f, -3f, 55f), new Vector3(34f, 6f, 138f));
            river.SurfaceY = BedTop - 0.1f;
            river.FallbackPoint = new Vector3(0f, 0.2f, -4f);
            _river = river;

            // side walls of the plant (no ceiling: camera room)
            Wall(g, -16.2f, -12f, -16.2f, 121.7f, WallH, RiverY, MatKey.WallAlt);
            Wall(g, 16.2f, -12f, 16.2f, 121.7f, WallH, RiverY, MatKey.WallAlt);
            Wall(g, -16.2f, -12f, 16.2f, -12f, WallH, RiverY, MatKey.WallAlt);
            // coin silos, deposit walls and columns along the walls (decor only)
            var gold = UIColors.Hex(0xE8B33A);
            string[] silo = { "COINS (SORTED)", "COINS (UNSORTED)", "COINS (EMOTIONAL)", "PENNIES (DO NOT ASK)" };
            for (int i = 0; i < 4; i++)
            {
                float z = 14f + i * 28f;
                float x = i % 2 == 0 ? -14.2f : 14.2f;
                EnvKit.Decor.Bank.Canister(d, new Vector3(x, RiverY, z), null, gold, silo[i], 7f, 1.3f, 0.65f + 0.08f * i, false);
            }
            for (int i = 0; i < 3; i++)
            {
                float z = 4f + i * 40f;
                EnvKit.Decor.Bank.SafeDepositWall(d, new Vector3(-16f, RiverY + 1.5f, z + 18f), Look(Vector3.left), 10, 7, 0.45f, false, i == 0 ? "DEPOSIT BOXES" : null);
                EnvKit.Decor.Bank.SafeDepositWall(d, new Vector3(16f, RiverY + 1.5f, z), Look(Vector3.right), 10, 7, 0.45f, false, i == 1 ? "SAFE DEPOSITS (UNSAFE)" : null);
            }
            for (int i = 0; i < 5; i++)
            {
                float z = -2f + i * 26f;
                EnvKit.Decor.Bank.MarbleColumn(d, new Vector3(-15.2f, RiverY, z), null, WallH, 1.1f, true, false);
                EnvKit.Decor.Bank.MarbleColumn(d, new Vector3(15.2f, RiverY, z + 13f), null, WallH, 1.1f, true, false);
            }
            // alarm beacons on the walls (emissive; the warning beacon has its own decor-light flag)
            for (int i = 0; i < 4; i++)
                Panel(d, new Vector3(i % 2 == 0 ? -15.9f : 15.9f, 4.6f, 10f + i * 30f), i % 2 == 0 ? Vector3.left : Vector3.right, AlarmRed);
            // the joke signs along the ride
            Sign(d, new Vector3(-15.95f, 2.8f, 44f), Vector3.left, "COIN RIVER: NO SWIMMING\n(YOU WILL BOUNCE)", 4.2f, 1.6f, SignStyle.Warning);
            Sign(d, new Vector3(15.95f, 3.2f, 74f), Vector3.right, "PIGGY BANK:\nNOW 400% PIGGIER", 3.8f, 1.6f, SignStyle.Danger);
            StoryKit.HoloPoster(d, new Vector3(-5f, 0f, 34.8f), Look(new Vector3(-1f, 0f, 1f)), StoryKitLines.Poster(4), 2);   // faces the oncoming tram (levels-2)
            StoryKit.HoloPoster(d, new Vector3(-7f, 0f, 90.5f), Look(new Vector3(-1f, 0f, 1f)), StoryKitLines.Poster(6), 2);
        }

        L9CoinRiver _river;

        void RailBed(Transform d, float z0, float z1)
        {
            float cz = (z0 + z1) * 0.5f, len = z1 - z0;
            Kit.BoxVisual(d, "RailBed", new Vector3(0f, (BedTop + RiverY) * 0.5f, cz), new Vector3(2.6f, BedTop - RiverY, len), MatKey.Concrete, 0.02f);
            for (int s = -1; s <= 1; s += 2)
                Kit.BoxVisual(d, "Rail", new Vector3(s * 0.85f, BedTop + 0.06f, cz), new Vector3(0.12f, 0.12f, len), MatKey.MetalLight, 0.01f, null, false);
            Kit.BoxVisual(d, "Sleepers", new Vector3(0f, BedTop + 0.01f, cz), new Vector3(2.2f, 0.02f, len), MatKey.Wood, 0f, null, false);
        }

        // ================================================================== S0 TRAM PLATFORM
        void BuildS0(LevelBuildContext ctx)
        {
            var g = ctx.Section("S0_TramPlatform", Vector3.zero);
            var d = ctx.Decor;
            Pier(g, -8f, 8f, -10f, 2f);
            Pier(g, -8f, -2f, 2f, 9.2f);     // boarding wings beside the docked tram (deck x -2..2, z 2..9)
            Pier(g, 2f, 8f, 2f, 9.2f);
            ctx.SetSpawn(new Vector3(0f, 0f, -4.6f), 0f);   // (spec -6: moved up so the follow camera clears the vault wall)
            ctx.Checkpoint(new Vector3(0f, 0f, -2.5f), 0f);                 // C1
            // the vault behind you (you robbed it in SHIFT 07): open door, alarms
            Wall(g, -8f, -10.2f, 8f, -10.2f, 6f);
            EnvKit.Decor.Bank.VaultDoorDecor(d, new Vector3(0f, 3.6f, -9.9f), Look(Vector3.back), 4.4f, 0f, false, "VAULT 7 · CLOSED FOR ROBBERY");
            // the decor door has no collider and bulges 0.5 m off the wall right where the spawn follow camera sits: give
            // the camera (and bodies) a stop flush with the door face, kept between the name plate below and the light panel above
            Kit.Make("VaultDoorStop", g, new Vector3(0f, 3.3f, -9.72f)).AddComponent<BoxCollider>().size = new Vector3(4.3f, 3.8f, 0.5f);   // door disc y 1.5..5.7; label plate below, light panel above stay clear
            Sign(d, new Vector3(-5.2f, 3.4f, -9.95f), Vector3.back, "PORKCHOP — DO NOT FEED\nAFTER MIDNIGHT (OR EVER)", 4f, 1.4f, SignStyle.Danger);
            EnvKit.Poster(d, new Vector3(5.4f, 2.6f, -9.95f), Look(Vector3.back), "PIGGY BANK", "NOW 400% PIGGIER");
            Panel(d, new Vector3(0f, 5.6f, -9.95f), Vector3.back, AlarmRed, true, 2.6f, 16f);
            EnvKit.Decor.WarningBeacon(d, new Vector3(-7.4f, 0f, 1.4f), true);
            EnvKit.Decor.WarningBeacon(d, new Vector3(7.4f, 0f, 1.4f), true);
            PostSign(d, new Vector3(4.4f, 2.3f, 0.9f), Vector3.forward, "TRAM: MAX 3 PASSENGERS.\nMIN 3 PASSENGERS.", 3.4f, 1.1f);
            PostSign(d, new Vector3(-7.6f, 1.7f, -4f), Vector3.left, "COIN RIVER: NO SWIMMING\n(YOU WILL BOUNCE)", 3.0f, 1.0f, 0f, SignStyle.Warning);
            ctx.Label(d, new Vector3(0f, 0.02f, 1.2f), Quaternion.Euler(90f, 0f, 0f), "ALL ABOARD", 0.5f, Gold, 4f);
            EnvKit.Decor.Bank.VelvetRopeLine(d, new Vector3(-6.8f, 0f, -8.5f), new Vector3(-6.8f, 0f, 0.5f), 2.5f, null, null);
            EnvKit.Decor.Bank.CoinPile(d, new Vector3(6.2f, 0f, -7.5f), null, 0.9f, 0.45f, 3);
            ctx.Tutorial(new Vector3(0f, 1.5f, -3f), new Vector3(16f, 3f, 14f), "ch08_chase", "ALL ABOARD",
                "ALL ABOARD: the tram only moves with everyone on it. Clear each obstacle before Porkchop catches up.",
                new[] { "TAB", "WASD" }, true);
            // ME-TV on a gantry over the track
            Kit.BoxVisual(d, "Gantry", new Vector3(0f, 7.3f, 15.6f), new Vector3(12f, 0.5f, 0.5f), MatKey.MetalDark, 0.03f);
            for (int s = -1; s <= 1; s += 2) Kit.BoxVisual(d, "GantryLeg", new Vector3(s * 6f, (7.3f + RiverY) * 0.5f, 15.6f), new Vector3(0.4f, 7.3f - RiverY, 0.4f), MatKey.MetalDark, 0.03f);
            var tv = StoryKit.MeTvScreen(d, new Vector3(0f, 5.6f, 15.4f), Look(Vector3.forward), 4.4f);
            ctx.Mark("story.metv", tv);
        }

        // ================================================================== TRACK + STATIONS
        void BuildTrack(LevelBuildContext ctx)
        {
            var puz = ctx.Puzzles;
            var d = ctx.Decor;

            // ---- ST1 ROADBLOCK: VAULT BRICKS (punch from the side platform, or 4 shots from the deck)
            var s1 = ctx.Section("ST1_Roadblock", Vector3.zero);
            Pier(s1, -6f, -2f, 28f, 36f);
            var wall = PuzzleKit.CrackedWall(puz, new Vector3(0f, -0.9f, 35f), Yaw(180f), new Vector3(4.5f, 4.4f, 0.6f), 4);
            ctx.Mark("st1.wall", wall.gameObject);
            Sign(d, new Vector3(0f, 4.15f, 34.6f), Vector3.forward, "ROADBLOCK: VAULT BRICKS", 4.2f, 0.8f, SignStyle.Danger);
            Reveal(d, new Vector3(-1f, 2.6f, 34.4f), PerspectiveMask.Strength, "VAULT BRICKS — PUNCH");
            Reveal(d, new Vector3(1f, 2.6f, 34.4f), PerspectiveMask.Weapon, "4 SHOTS");
            ctx.MarkPoint("st1.hop", new Vector3(-3.3f, 0f, 30f));
            ctx.MarkPoint("st1.punch", new Vector3(-2.75f, 0f, 34.05f));
            ctx.Label(d, new Vector3(-4f, 0.02f, 32f), Quaternion.Euler(90f, 90f, 0f), "BRUTE HOP-OFF ZONE", 0.4f, Gold, 6f);
            PostSign(d, new Vector3(-5.3f, 2.2f, 29f), Vector3.forward, "STATION 1: ROADBLOCK", 2.6f, 0.7f);
            ctx.Hint(new Vector3(-1f, 1.5f, 31f), new Vector3(12f, 4f, 10f), "Brute: hop off and punch the bricks. Or shoot them 4 times.", "Bricks are just patient walls.");

            // ---- ST2 TOLL GATE: the deck's CONSOLE overrides it (IQ)
            var s2 = ctx.Section("ST2_TollGate", Vector3.zero);
            Pier(s2, -8f, -2f, 54f, 64f);
            ctx.Checkpoint(new Vector3(-5f, 0f, St2Z), 90f);                 // C2
            var tollRestore = new GameObject("TollPaid").AddComponent<L9Signal>();
            tollRestore.transform.SetParent(puz, false);

            // ---- ST3 DRAWBRIDGE: lever on the PYLON (turbo jump 5.6 m onto +0.5 m, or a living bridge + hop)
            var s3 = ctx.Section("ST3_Drawbridge", Vector3.zero);
            Pier(s3, -8f, -2f, 82f, 92f);
            ctx.Checkpoint(new Vector3(-5f, 0f, St3Z), 90f);                 // C3
            Kit.SolidBox(s3, "Pylon", new Vector3(9.1f, -0.75f, 87.5f), new Vector3(3f, 2.5f, 3f), MatKey.Concrete, 0.04f);
            Kit.BoxVisual(d, "PylonTopStripe", new Vector3(9.1f, 0.506f, 87.5f), new Vector3(2.9f, 0.01f, 2.9f), MatKey.HazardStripes, 0f, null, false);
            Kit.BoxVisual(d, "PylonPier", new Vector3(9.1f, (-2f + RiverY - 0.4f) * 0.5f, 87.5f), new Vector3(2.6f, -2f - RiverY + 0.4f + 0.1f, 2.6f), MatKey.Concrete, 0.03f);
            var lever = PuzzleKit.Lever(puz, new Vector3(9.9f, 0.5f, 88.4f), Yaw(-90f), 0, true, false, "DRAWBRIDGE DOWN!");
            ctx.Mark("st3.lever", lever.gameObject);
            ctx.MarkPoint("st3.pylonTop", new Vector3(8.9f, 0.5f, 87.3f));
            var catwalk = PuzzleKit.RetractableBridge(puz, new Vector3(7.6f, 0.5f, St3Z), Yaw(-90f), 5.6f, 2f, lever);
            ctx.Mark("st3.catwalk", catwalk.gameObject);
            var leafA = PuzzleKit.FoldingRamp(puz, new Vector3(0f, -0.45f, 92f), 0f, 4.6f, 5f, 0.01f, false, "DRAWBRIDGE RAISED\nFOR YOUR INCONVENIENCE", lever);
            var leafB = PuzzleKit.FoldingRamp(puz, new Vector3(0f, -0.45f, 102f), 180f, 4.6f, 5f, 0.01f, false, "", lever);
            ctx.Mark("st3.bridgeA", leafA.gameObject);
            ctx.Mark("st3.bridgeB", leafB.gameObject);
            PostSign(d, new Vector3(10.3f, 1.9f, 86.4f), Vector3.right, "LEVER: LOWERS\nTHE DRAWBRIDGE", 2.2f, 0.9f, 0.5f);
            Sign(d, new Vector3(6.2f, 1.1f, 86.0f), Vector3.right, "5.6 M — TURBO", 1.8f, 0.5f, SignStyle.Arrow);
            Reveal(d, new Vector3(5f, 1.6f, 87.4f), PerspectiveMask.Speed, "5.6 M — TURBO (OR A LIVING BRIDGE)");
            Reveal(d, new Vector3(9.9f, 1.8f, 88.4f), PerspectiveMask.Intellect, "ONE-SHOT LEVER: BRIDGE + CATWALK");
            PostSign(d, new Vector3(-5.5f, 2.2f, 83f), Vector3.forward, "STATION 3: DRAWBRIDGE", 2.6f, 0.7f);
            PostSign(d, new Vector3(-3.2f, 1.8f, 91.4f), new Vector3(-1f, 0f, 1f), "COIN RIVER GORGE:\nDEFINITELY NO SWIMMING", 2.8f, 0.9f, 0f, SignStyle.Warning);
            ctx.Hint(new Vector3(3f, 1.5f, St3Z), new Vector3(14f, 4f, 10f), "TURBO LEGS can jump to the pylon. Or bridge to it and hop.", "Pylons are for jumping. Allegedly.");

            // ---- DOCK
            var dock = ctx.Section("Dock", Vector3.zero);
            Pier(dock, -8f, -2f, 113f, 121.6f);
            Pier(dock, 2f, 8f, 113f, 121.6f);
            Kit.BoxVisual(d, "Bumper", new Vector3(0f, -0.3f, 122.1f), new Vector3(3.4f, 0.8f, 0.4f), MatKey.HazardStripes, 0.05f);

            // ---- the chase itself (bosses kit). Points are deck-top centres (local to Puzzles = world).
            var pursuer = Kit.Make("PorkchopPursuer", puz, Vector3.zero).transform;
            var pigModel = BossModels.Porkchop(pursuer, new Vector3(0f, BedTop, 0f), Quaternion.identity, false);
            var gate = PuzzleKit.Door(puz, new Vector3(0f, 0f, 64f), null, DoorMode.SlideUp, 4.6f, 4.4f, tollRestore);
            gate.Logic = SignalLogic.Any;
            ctx.Mark("st2.gate", gate.gameObject);
            for (int s = -1; s <= 1; s += 2)
                Kit.BoxVisual(d, "GatePier", new Vector3(s * 2.8f, (RiverY - 0.1f) * 0.5f, 64f), new Vector3(0.7f, -RiverY + 0.2f, 0.7f), MatKey.Concrete, 0.03f);
            Sign(d, new Vector3(0f, 5.3f, 63.6f), Vector3.forward, "TOLL", 2.4f, 0.9f, SignStyle.Danger);
            PostSign(d, new Vector3(-5.6f, 2.3f, 63.4f), Vector3.forward, "OVERRIDE TOLL? YOU HAVE NO COINS.\nYOU HAVE ALL THE COINS.", 4.2f, 1.0f);
            PostSign(d, new Vector3(-5.5f, 2.2f, 55f), Vector3.forward, "STATION 2: TOLL GATE", 2.6f, 0.7f);

            var stations = new[]
            {
                new ChaseStation(1, wall, 0, "ROADBLOCK"),
                new ChaseStation(2, null, 2, "TOLL GATE"),
                new ChaseStation(3, lever, 3, "DRAWBRIDGE"),
                new ChaseStation(4, null, 4, "DOCK"),        // the end: restores past C4 park the tram here
            };
            var track = new[] { new Vector3(0f, 0f, DockZ), new Vector3(0f, 0f, St1Z), new Vector3(0f, 0f, St2Z), new Vector3(0f, 0f, St3Z), new Vector3(0f, 0f, EndZ) };
            _track = BossKit.Chase(puz, track, stations, pursuer);
            _track.BustedBeat = "ch08.busted";
            _track.TripText = "PORKCHOP TRIPS ON A COIN!";
            _track.DeckSize = new Vector3(4f, 3f, 7f);
            ctx.Mark("chase", _track.gameObject);
            _deck = _track.Tram.Body.transform;
            var tram = BossModels.GetawayTram(_deck, Vector3.zero, Quaternion.identity, new Vector2(4f, 7f), true, -0.55f);
            ctx.Mark("tram", _track.Tram.gameObject);
            // the deck CONSOLE (front-left corner), live only at the toll gate
            var spot = tram.Part("ConsoleSpot");
            var cpos = spot != null ? _deck.InverseTransformPoint(spot.position) : new Vector3(-1.45f, 0f, 2.85f);
            cpos.y = 0f;
            var console = PuzzleKit.Terminal(_deck, cpos, Yaw(180f), Tuning.TerminalIQ, true, 6f, "TOLL OVERRIDDEN!");
            ctx.Mark("deck.console", console.gameObject);
            stations[1].Clear = console;
            gate.Inputs = new SignalSource[] { console, tollRestore };
            var idle = ctx.Label(_deck, cpos + new Vector3(0f, 1.75f, 0f), Quaternion.identity, "TOLL CONSOLE\n(ONLINE AT THE TOLL)", 0.2f, VaultTeal, 1.6f);
            Reveal(_deck, cpos + new Vector3(0f, 2.2f, 0f), PerspectiveMask.Intellect, "OVERRIDE TOLL (IQ 7)");
            ctx.Label(_deck, new Vector3(0f, 0.02f, -2.6f), Quaternion.Euler(90f, 0f, 0f), "ALL ABOARD", 0.42f, Gold, 3.4f);
            ctx.MarkPoint("s0.deck", new Vector3(0f, 0f, -0.8f), 0f, _deck);
            ctx.MarkPoint("deck.slot.0", new Vector3(-1.1f, 0f, -1.4f), 0f, _deck);
            ctx.MarkPoint("deck.slot.1", new Vector3(1.1f, 0f, -1.4f), 0f, _deck);
            ctx.MarkPoint("deck.slot.2", new Vector3(0f, 0f, -1.9f), 0f, _deck);
            ctx.MarkPoint("deck.edgeRight", new Vector3(1.7f, 0f, 0f), 90f, _deck);
            ctx.MarkPoint("deck.edgeLeft", new Vector3(-1.7f, 0f, 0f), -90f, _deck);
            ctx.MarkPoint("deck.front", new Vector3(-0.2f, 0f, 3.1f), 0f, _deck);
            for (int i = 0; i < 3; i++) ctx.MarkPoint("chase.station." + i, new Vector3(0f, 0f, i == 0 ? St1Z : i == 1 ? St2Z : St3Z));
            ctx.Hint(new Vector3(0f, 1.5f, St2Z), new Vector3(10f, 4f, 9f), "The tram's own console overrides the toll. Big brains only.", "Tolls hate smart people.");

            _river.Track = _track;
            var dir = _track.gameObject.AddComponent<L9ChaseDirector>();
            dir.Track = _track;
            dir.PigModel = pigModel.transform;
            dir.Pig = pigModel;
            dir.Wall = wall;
            dir.Console = console;
            dir.ConsoleIdleSign = idle != null ? idle.gameObject : null;
            dir.TollRestore = tollRestore;
            dir.ArenaCheckpoint = 4;
        }
    }
}
