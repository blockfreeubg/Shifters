// =====================================================================================
//  SHIFTERS - L14Builder.cs   [SHIFT 14 - ALL IN FAVOR? THE IMPEACHMENT + EPILOGUE, LevelIndex 13, L14_AllInFavor]
//  THE HALL OF ONE on ME TOWER's top floor: black glass, gold trim, a thousand grey seats behind glass, one throne.
//    S0 public entrance (x -6..6, z -8..0)         C1   spawn, ME-TV, metal detector
//    THE HALL (x -16..16, z 0..44, ONE floor collider, 10 m walls, glass at x +-16)
//      S1 the charges (three exhibits)  S2 the filibuster (word stair + press gallery)  -> C2 (forced)
//      S3 the vote (three plates + AYE keys)                                             -> C3 (forced)
//      S4 the catch (POPULAR) + the bench for three (the dais z 36..44, top 1.0)
//    S5 the balcony of three (z 44..52, top 1.0) + the vista;  S6 orientation annex (C16 set, x 60..72)
//  Level director: L14Hearing. Nothing in this chapter can hurt anyone: no KillZone, hazard or enemy.
//  Section files: L14Builder.cs (hall, S0, S1), L14Builder.Finale.cs (S2..S6, vista).
// =====================================================================================
using UnityEngine;
using TMPro;

namespace Shifters
{
    public partial class L14Builder : ILevelBuilder
    {
        public int LevelIndex => 13;
        public const float WallH = 10f;

        LevelBuildContext _ctx;
        L14Hearing _h;
        Transform _geo, _decor, _puz, _play;

        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);
        static Quaternion Yaw(float d) => Quaternion.Euler(0f, d, 0f);
        static Color Gold => VillainLook.Gold;

        public void Build(LevelBuildContext ctx)
        {
            _ctx = ctx;
            _geo = ctx.Geometry; _decor = ctx.Decor; _puz = ctx.Puzzles; _play = ctx.Gameplay;
            var dir = Kit.Make("L14Hearing", ctx.Gameplay, Vector3.zero);
            _h = dir.AddComponent<L14Hearing>();
            BuildShell();
            BuildS0();
            BuildS1();
            BuildS2();
            BuildS3();
            BuildS4();
            BuildS5();
            BuildVista();
            BuildAnnex();
            BuildHints();
        }

        // ------------------------------------------------------------------ helpers
        GameObject Slab(Transform p, float x0, float x1, float z0, float z1, float top, float h, MatKey mat = MatKey.Marble, bool trim = true)
            => EnvKit.Floor(p, V((x0 + x1) * 0.5f, top, (z0 + z1) * 0.5f), V(x1 - x0, h, z1 - z0), mat, trim);

        GameObject Wall(Transform p, float x0, float z0, float x1, float z1, float h = WallH, float y = 0f, MatKey mat = MatKey.BlackGlass)
            => EnvKit.Wall(p, V(x0, y, z0), V(x1, y, z1), h, 0.4f, mat, true);

        static MeshRenderer Vis(Transform p, string n, Vector3 c, Vector3 s, MatKey m, float bevel = 0.03f, Quaternion? r = null)
            => Kit.BoxVisual(p, n, c, s, m, bevel, r, false);

        static BoxCollider Solid(Transform p, string n, Vector3 c, Vector3 s, MatKey m, float bevel = 0.03f) => Kit.SolidBox(p, n, c, s, m, bevel);

        /// <summary>World-space TMP text, readable by a viewer looking along rot's forward (identity: viewer looks +Z).</summary>
        public static TextMeshPro Text(Transform parent, Vector3 localPos, Quaternion localRot, string text, float height, Color color, float maxWidth = 6f)
        {
            var go = Kit.Make("Txt_" + (text.Length > 12 ? text.Substring(0, 12) : text).Replace("\n", " "), parent, localPos, localRot);
            var tmp = go.AddComponent<TextMeshPro>();
            var f = ShiftersFonts.Display;
            if (f != null) tmp.font = f;
            tmp.text = text;
            tmp.fontSize = height * 10f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.color = color;
            tmp.rectTransform.sizeDelta = new Vector2(maxWidth, height * 3f);
            var r = go.GetComponent<MeshRenderer>();
            if (r != null) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return tmp;
        }

        /// <summary>Text lying on the floor, readable by someone walking along +Z (or the given yaw).</summary>
        TextMeshPro FloorText(Vector3 pos, string text, float h, Color c, float yaw = 0f, float w = 5f)
            => Text(_decor, pos + Vector3.up * 0.02f, Quaternion.Euler(90f, yaw, 0f), text, h, c, w);

        /// <summary>Sign readable by a viewer looking along 'viewYaw' (0 = +Z: north-wall sign; 90 = east wall; -90 = west wall).</summary>
        GameObject Sign(Vector3 pos, float viewYaw, string text, float letter = 0.38f, Transform parent = null)
            => _ctx.Sign(parent != null ? parent : _decor, pos, viewYaw, text, letter);

        static GameObject Sphere(Transform p, string n, Vector3 c, float r, MatKey m)
        {
            var go = Kit.Make(n, p, c);
            ProcMesh.AddRenderer(go, ProcMesh.Cached("L14Sphere_" + r.ToString("0.00"), () => ProcMesh.Sphere(r, 14, 9)), Kit.Mat(m), false);
            return go;
        }

        // ------------------------------------------------------------------ THE HALL OF ONE (shell)
        void BuildShell()
        {
            var g = _ctx.Section("Hall", Vector3.zero);
            // ONE floor collider for S1-S4 (y 0)
            Slab(_geo, -16f, 16f, 0f, 44f, 0f, 0.6f, MatKey.Marble, false);
            // velvet aisle + gold inlay (decor, flush)
            Vis(_decor, "Aisle", V(0f, 0.006f, 17f), V(3f, 0.012f, 34f), MatKey.Velvet, 0f);
            for (int s = -1; s <= 1; s += 2) Vis(_decor, "AisleGold", V(s * 1.6f, 0.008f, 17f), V(0.12f, 0.016f, 34f), MatKey.Gold, 0f);
            // south wall with the 6 m entrance (x -3..3)
            Wall(_geo, -16f, 0f, -3f, 0f); Wall(_geo, 3f, 0f, 16f, 0f);
            Vis(_decor, "EntranceLintel", V(0f, 8f, 0f), V(6f, 4f, 0.4f), MatKey.BlackGlass);
            // side walls: solid ends + full-height glass (z 2..42) in front of the seat tiers
            for (int s = -1; s <= 1; s += 2)
            {
                float x = s * 16f;
                Wall(_geo, x, 0f, x, 2f); Wall(_geo, x, 42f, x, 44f);
                Solid(_geo, "HallGlass", V(x, WallH * 0.5f, 22f), V(0.2f, WallH, 40f), MatKey.Glass, 0f);
                for (int i = 0; i <= 10; i++) Vis(_decor, "Mullion", V(x, WallH * 0.5f, 2f + i * 4f), V(0.25f, WallH, 0.18f), MatKey.Gold, 0.02f);
                Vis(_decor, "GlassRail", V(x, 1.1f, 22f), V(0.3f, 0.12f, 40f), MatKey.Gold, 0.02f);
                BuildTiers(s);
            }
            // north wall: door gap x -2..2 (sill 1.0, 4 m tall); the outer thirds are glass above 6 m (clears at sunrise)
            Wall(_geo, -16f, 44f, -2f, 44f, 6f); Wall(_geo, 2f, 44f, 16f, 44f, 6f);
            Solid(_geo, "NorthUpperC", V(0f, 8f, 44f), V(12f, 4f, 0.4f), MatKey.BlackGlass);
            Solid(_geo, "NorthLintel", V(0f, 5.5f, 44f), V(4f, 1f, 0.4f), MatKey.BlackGlass);
            Solid(_geo, "NorthUpperL", V(-4f, 7f, 44f), V(4f, 2f, 0.4f), MatKey.BlackGlass);
            Solid(_geo, "NorthUpperR", V(4f, 7f, 44f), V(4f, 2f, 0.4f), MatKey.BlackGlass);
            var glassL = EnvKit.Decor.Tower.BlackGlassPanel(_decor, V(-11f, 6f, 44.05f), null, 10f, 4f, 5, 2, true);
            var glassR = EnvKit.Decor.Tower.BlackGlassPanel(_decor, V(11f, 6f, 44.05f), null, 10f, 4f, 5, 2, true);
            _h.NorthGlass = new[] { glassL, glassR };
            // gold crown moulding along the top of the walls
            Vis(_decor, "CrownS", V(0f, WallH, 0f), V(32.6f, 0.3f, 0.7f), MatKey.Gold);
            Vis(_decor, "CrownN", V(0f, WallH, 44f), V(32.6f, 0.3f, 0.7f), MatKey.Gold);
            // a ring of ceiling beams (open sky: the red pre-dawn becomes the sunrise)
            for (int i = 0; i < 5; i++) Vis(_decor, "Beam", V(0f, WallH + 0.2f, 4f + i * 9f), V(32f, 0.4f, 0.5f), MatKey.BlackGlass);
            // neon + tally board + posters on the north wall
            _h.Neon = Text(_decor, V(0f, 6.0f, 43.75f), Quaternion.identity, "SHIFT CHANGE IN: NEVER", 0.55f, UIColors.Danger, 9f);
            var tally = StoryKit.TallyBoard(_decor, V(0f, 3.34f, 43.75f), null);
            tally.transform.localScale = Vector3.one * 1.6f;
            _ctx.Mark("story.tally", tally);
            _h.TallyBoard = tally;
            // above the balcony door's gantry cap (top y 9.46) and below the crown moulding (y 9.85), just in front of the cap face
            Text(_decor, V(0f, 9.64f, 43.66f), Quaternion.identity, "ARTICLE 3, CLAUSE B: THE ACCUSED MAY NOT VOTE. — M. ME, INTERN #0, 1999", 0.18f, UIColors.TextMuted, 11f);
            Sign(V(-10f, 3.4f, 43.7f), 0f, "MOTIONS PASSED: 4,812 · ALL 1–0", 0.3f);
            EnvKit.Poster(_decor, V(10f, 3.2f, 43.75f), Quaternion.identity, "EMPLOYEE OF THE MONTH", "ME (AGAIN)", new Vector2(1.6f, 2.1f));
            // ARTICLE 3 poster with its sticky note (drops at the vote)
            Vis(_decor, "ArticlePanel", V(-6.5f, 3.2f, 43.75f), V(2.0f, 1.4f, 0.06f), MatKey.Poster);
            _h.ArticlePoster = Text(_decor, V(-6.5f, 3.2f, 43.70f), Quaternion.identity, "ARTICLE 3\nDOES NOT EXIST.", 0.22f, UIColors.Hex(0x30343A), 1.8f);
            var note = Kit.Make("StickyNote", _decor, V(-6.1f, 3.05f, 43.66f));
            Vis(note.transform, "Paper", Vector3.zero, V(0.55f, 0.45f, 0.02f), MatKey.HazardYellow, 0f);
            Text(note.transform, V(0f, 0f, -0.02f), Quaternion.identity, "(IT DOES\nNOT. - M)", 0.08f, Color.black, 0.5f);
            _h.StickyNote = note;
        }

        void BuildTiers(int side)
        {
            float sx = side;
            // stepped grey seat tiers (decor, never enterable: the glass is in front)
            for (int k = 0; k < 3; k++)
            {
                float x0 = 17f + k * 3f, top = 1.5f * (k + 1);
                Vis(_decor, "Tier", V(sx * (x0 + 1.5f), top * 0.5f, 22f), V(3f, top, 40f), MatKey.BlackGlass, 0.02f);
                Vis(_decor, "TierEdge", V(sx * (x0 + 0.05f), top + 0.03f, 22f), V(0.1f, 0.06f, 40f), MatKey.Gold, 0f);
            }
            Vis(_decor, "TierBack", V(sx * 26.2f, 6f, 22f), V(0.4f, 12f, 44f), MatKey.BlackGlass);
            var crowdRoot = Kit.Make(side > 0 ? "CrowdE" : "CrowdW", _decor, V(sx * 21.5f, 0f, 22f), Yaw(side > 0 ? 90f : -90f)).transform;
            for (int k = 0; k < 3; k++)
            {
                float top = 1.5f * (k + 1), z0 = -4.5f + k * 3f;
                var area = new Bounds(V(0f, top + 0.9f, z0 + 1.7f), V(38f, 1.8f, 2.2f));
                StoryKit.CitizenCrowd(crowdRoot, area, 100, 17 + k + (side > 0 ? 0 : 7));
            }
            // a front row of friendly Team-Building Dummies with "AYE?" cards
            for (int i = 0; i < 4; i++)
            {
                float z = 8f + i * 8f;
                var dpos = V(sx * 17.8f, 1.5f, z);
                var face = Yaw(side > 0 ? -90f : 90f);
                DummyModel.Create(_decor, dpos, face, false);
                var card = Kit.Make("AyeCard", _decor, dpos + V(-sx * 0.45f, 1.45f, 0f), face).transform;
                Vis(card, "Card", Vector3.zero, V(0.55f, 0.4f, 0.03f), MatKey.SignWhite, 0.01f);
                Text(card, V(0f, 0f, 0.025f), Quaternion.Euler(0f, 180f, 0f), "AYE?", 0.16f, Color.black, 0.5f);
            }
        }

        // ------------------------------------------------------------------ S0 THE PUBLIC ENTRANCE
        void BuildS0()
        {
            Slab(_geo, -6f, 6f, -8f, 0f, 0f, 0.6f, MatKey.Marble, true);
            Wall(_geo, -6f, -8f, 6f, -8f, 6f); Wall(_geo, -6f, -8f, -6f, 0f, 6f); Wall(_geo, 6f, -8f, 6f, 0f, 6f);
            Wall(_geo, -6f, 0f, -3f, 0f, 6f); Wall(_geo, 3f, 0f, 6f, 0f, 6f);
            _ctx.SetSpawn(V(0f, 0f, -6f), 0f);
            _ctx.Checkpoint(V(0f, 0f, -3f), 0f);
            var tv = StoryKit.MeTvScreen(_decor, V(5.75f, 3f, -4f), Yaw(90f), 3.2f);
            var tvv = tv != null ? tv.GetComponentInChildren<MeTvScreenView>(true) : null;
            if (tvv != null) tvv.SetHeadline("ME-TV LIVE: MY TRIAL (I'M WINNING)");
            _ctx.Mark("story.metv", tv);
            Text(_decor, V(5.7f, 4.95f, -4f), Yaw(90f), "ME-TV LIVE: MY TRIAL (I'M WINNING)", 0.24f, Gold, 4f);
            // METAL DETECTOR arch (collider-free, 4.4 m wide)
            var det = Kit.Make("MetalDetector", _decor, V(0f, 0f, -1f)).transform;
            for (int s = -1; s <= 1; s += 2) Vis(det, "Post", V(s * 2.3f, 1.5f, 0f), V(0.3f, 3f, 0.5f), MatKey.MetalLight);
            Vis(det, "Top", V(0f, 3.1f, 0f), V(4.9f, 0.3f, 0.5f), MatKey.MetalLight);
            Vis(det, "Lamp", V(0f, 3.3f, 0f), V(0.4f, 0.15f, 0.3f), MatKey.LightRed);
            Text(det, V(0f, 3.1f, -0.27f), Quaternion.identity, "TEAMWORK DETECTOR", 0.2f, Color.black, 4.5f);
            _h.Detector = det;
            Sign(V(0f, 4.6f, -0.25f), 0f, "HALL OF ONE: 1,000 SEATS. ONE VOTE.", 0.34f);
            Sign(V(-5.75f, 2.6f, -4.5f), -90f, "BREAK AREA: THE WHOLE BUILDING (TODAY)", 0.26f);
            StoryKit.HoloPoster(_decor, V(-4.6f, 0f, -6.8f), null, "ONE WORLD. ONE RULER. ONE CHAIR.", 4);
            _ctx.Tutorial(V(0f, 1.5f, 1.5f), V(6f, 3f, 2f), "ch14_evidence", "PRESENT THE EVIDENCE",
                "PRESENT THE EVIDENCE: three exhibits, three different jobs. Send the right intern to each.", null, true);
        }

        // ------------------------------------------------------------------ S1 THE CHARGES
        void BuildS1()
        {
            // ---- A · THE GREY MEMO (STR): filing cabinet -> SCALE OF JUSTICE
            var cab = PuzzleKit.HeavyBlock(_puz, V(9f, 0f, 6f));
            cab.DisplayName = "filing cabinet";
            Text(cab.transform, V(0f, 0.35f, -0.81f), Quaternion.identity, "FILING CABINET\nCLASSIFIED (BY ME)", 0.15f, UIColors.AccentYellow, 1.5f);
            Text(cab.transform, V(-0.81f, 0.35f, 0f), Yaw(90f), "CLASSIFIED\n(BY ME)", 0.17f, UIColors.AccentYellow, 1.5f);
            _ctx.Mark("s1a.cabinet", cab.gameObject);
            for (int s = -1; s <= 1; s += 2) Vis(_decor, "PushLane", V(9f + s * 1.5f, 0.007f, 9.5f), V(0.1f, 0.014f, 7f), MatKey.Gold, 0f);
            FloorText(V(9f, 0f, 9.5f), "PUSH LANE ▲", 0.3f, Gold, 0f, 2.6f);
            var scaleGo = Kit.Make("ScaleOfJustice", _puz, V(9f, 0f, 13f));
            var st = scaleGo.transform;
            Vis(st, "Pad", V(0f, 0.008f, 0f), V(2.6f, 0.016f, 2.6f), MatKey.Gold, 0f);
            Vis(st, "PadInner", V(0f, 0.012f, 0f), V(2.3f, 0.016f, 2.3f), MatKey.PuzzleBody, 0f);
            Vis(st, "Column", V(2.0f, 1.25f, 0f), V(0.25f, 2.5f, 0.25f), MatKey.Gold);
            Vis(st, "Beam", V(2.0f, 2.5f, 0f), V(0.12f, 0.12f, 2.2f), MatKey.Gold);
            var panL = Kit.Make("PanL", st, V(2.0f, 2.0f, -1.0f)).transform;
            var panR = Kit.Make("PanR", st, V(2.0f, 2.0f, 1.0f)).transform;
            Vis(panL, "Pan", Vector3.zero, V(0.7f, 0.06f, 0.7f), MatKey.Gold);
            Vis(panR, "Pan", Vector3.zero, V(0.7f, 0.06f, 0.7f), MatKey.Gold);
            var scale = scaleGo.AddComponent<L14PropScale>();
            scale.PanL = panL; scale.PanR = panR;
            scale.Readout = Text(st, V(2.0f, 3.1f, 0f), Yaw(90f), "0 / 150 KG", 0.22f, Gold, 3f);
            Text(st, V(0f, 0.03f, -1.55f), Quaternion.Euler(90f, 0f, 0f), "SCALE OF JUSTICE (PROPS ≥ 150 KG)", 0.17f, Gold, 3f);
            _ctx.Mark("s1a.scale", scaleGo);
            _ctx.MarkPoint("s1a.scale.center", V(9f, 0f, 13f));
            _h.Cabinet = cab; _h.Scale = scale;
            Sign(V(15.7f, 3.4f, 8.0f), 90f, "THE SCALE WEIGHS EVIDENCE, NOT INTERNS", 0.26f);   // east glass wall: keeps the word stair (I, AM) clear
            Sign(V(9f, 2.6f, 4.0f), 0f, "FILING CABINET 160 KG — HEAVY LIFTER", 0.22f);
            PedestalPending("A", V(5.5f, 0f, 13f), out _h.PedestalA, out _h.PendingA);

            // ---- B · THE BOOKS (stack + carry): CLERK'S SHELF (top 2.5) -> EVIDENCE TRAY B
            Solid(_geo, "ClerksShelf", V(-15f, 1.25f, 10.5f), V(2f, 2.5f, 5f), MatKey.Wood, 0.03f);
            for (int i = 0; i < 3; i++) Vis(_decor, "ShelfBooks", V(-14.05f, 0.5f + i * 0.7f, 10.5f), V(0.14f, 0.45f, 4.4f), i == 1 ? MatKey.Velvet : MatKey.Cardboard, 0.02f);
            Sign(V(-13.95f, 1.35f, 12.2f), -90f, "CLERK'S SHELF: DO NOT CLIMB (CLIMB)", 0.2f);
            var books = PuzzleKit.Crate(_props(), V(-15.2f, 2.5f, 10.5f));
            books.name = "TheBooks";
            books.mass = 25f;
            var bg = books.GetComponent<Grabbable>();
            Text(books.transform, V(0f, 0f, -0.46f), Quaternion.identity, "THE BOOKS\n<size=60%>(COOKED)</size>", 0.16f, UIColors.AccentYellow, 0.85f);
            Text(books.transform, V(0.46f, 0f, 0f), Yaw(-90f), "THE BOOKS\n<size=60%>(COOKED)</size>", 0.16f, UIColors.AccentYellow, 0.85f);
            Text(books.transform, V(-0.46f, 0f, 0f), Yaw(90f), "THE BOOKS\n<size=60%>(COOKED)</size>", 0.16f, UIColors.AccentYellow, 0.85f);
            _ctx.Mark("s1b.books", books.gameObject);
            _ctx.MarkPoint("s1b.booksHome", V(-15.2f, 3.0f, 10.5f));
            _h.Books = books; _h.BooksHome = LevelMark.Find("s1b.booksHome") != null ? LevelMark.Find("s1b.booksHome").transform : null;
            scale.Props = new[] { cab.Body != null ? cab.Body : cab.GetComponent<Rigidbody>(), books };
            FloorText(V(-13.3f, 0f, 10.5f), "SMART ONE\nSTANDS HERE", 0.2f, UIColors.Intellect, 90f, 2f);
            Vis(_decor, "StackSpot", V(-13.3f, 0.006f, 10.5f), V(1.0f, 0.012f, 1.0f), MatKey.LightBlue, 0f);
            Text(_decor, V(-12.3f, 2.9f, 10.5f), Yaw(-90f), "SMART ONE ON THE BOTTOM:\nYOUR 1.5 M HEAD IS A STEP", 0.17f, UIColors.Intellect, 3.5f);
            var tray = PuzzleKit.PropSocket(_puz, V(-9f, 0f, 10.5f), null, bg);
            tray.IdleText = "EVIDENCE TRAY B";
            for (int s = -1; s <= 1; s += 2)
            {
                Vis(_decor, "TrayRim", V(-9f + s * 0.8f, 0.125f, 10.5f), V(0.08f, 0.25f, 1.68f), MatKey.Gold, 0.01f);
                Vis(_decor, "TrayRim", V(-9f, 0.125f, 10.5f + s * 0.8f), V(1.68f, 0.25f, 0.08f), MatKey.Gold, 0.01f);
            }
            Sign(V(-9f, 2.4f, 12.0f), 0f, "EVIDENCE TRAY B", 0.24f);
            _ctx.Mark("s1b.tray", tray.gameObject);
            _h.Tray = tray;
            Vis(_decor, "ThrowPad", V(-6.5f, 0.006f, 10.5f), V(1.4f, 0.012f, 1.4f), MatKey.HazardStripes, 0f);
            FloorText(V(-6.5f, 0f, 11.6f), "THROW FROM HERE\n(IF YOU MUST)", 0.16f, UIColors.Strength, 0f, 2.2f);
            _ctx.MarkPoint("s1b.throwPad", V(-6.5f, 0f, 10.5f));
            // bot marks (B)
            _ctx.MarkPoint("s1.laneW", V(-4.5f, 0f, 3.5f));
            _ctx.MarkPoint("s1.laneW2", V(-3.5f, 0f, 5.0f));
            _ctx.MarkPoint("s1b.stack", V(-13.3f, 0f, 10.5f), -90f);
            _ctx.MarkPoint("s1b.approach", V(-11.6f, 0f, 10.5f), -90f);
            _ctx.MarkPoint("s1b.shelfTop", V(-15.0f, 2.5f, 10.5f));
            _ctx.MarkPoint("s1b.shelfEdge", V(-14.35f, 2.5f, 12.0f));
            _ctx.MarkPoint("s1b.offShelf", V(-12.7f, 0f, 12.0f));
            _ctx.MarkPoint("s1b.trayStand", V(-10.35f, 0f, 10.5f), 90f);
            _ctx.MarkPoint("s1.laneE", V(4.5f, 0f, 2.5f));
            _ctx.MarkPoint("s0.doorE", V(1.6f, 0f, 0.6f));
            _ctx.MarkPoint("s0.doorW", V(-1.6f, 0f, 0.6f));
            _ctx.MarkPoint("s0.doorC", V(0f, 0f, 0.6f));
            _ctx.MarkPoint("s1a.pushStart", V(9f, 0f, 4.2f));

            // ---- C · THE BEDSHEET (IQ): EVIDENCE LOCKER + TERMINAL IQ 7
            Solid(_geo, "EvidenceLocker", V(-15.5f, 1.3f, 17.5f), V(1f, 2.6f, 3f), MatKey.MetalDark, 0.03f);
            var hinge = Kit.Make("LockerDoorHinge", _decor, V(-14.97f, 0f, 16.0f)).transform;
            Vis(hinge, "Door", V(0.03f, 1.25f, 1.5f), V(0.06f, 2.4f, 2.95f), MatKey.MetalLight, 0.02f);
            Text(hinge, V(0.07f, 1.5f, 1.5f), Yaw(-90f), "LAUNDRY\n<size=60%>(EVIDENCE)</size>", 0.22f, UIColors.Danger, 2.6f);
            _h.LockerDoor = hinge;
            Sign(V(-14.9f, 3.1f, 17.5f), -90f, "EVIDENCE LOCKER: LAUNDRY. DO NOT ASK.", 0.22f);
            var term = PuzzleKit.Terminal(_puz, V(-12f, 0f, 17.5f), Yaw(90f), 7, true);
            _ctx.Mark("s1c.terminal", term.gameObject);
            _ctx.MarkPoint("s1c.stand", V(-10.9f, 0f, 17.5f), -90f);
            Text(_decor, V(-11.95f, 2.15f, 17.5f), Yaw(-90f), "EVIDENCE LOCKER\n<size=60%>(PASSWORD HINT: IT'S ME)</size>", 0.18f, UIColors.Intellect, 2.6f);
            _h.Locker = term;
            EnvKit.Pipe(_decor, new[] { V(-15f, 2.45f, 18.6f), V(-14f, 2.45f, 20f), V(-13f, 2.45f, 21f) }, 0.04f, false, MatKey.MetalLight);
            var sStart = Kit.Make("SheetStart", _play, V(-14.4f, 0.4f, 17.5f), Yaw(90f)).transform;
            _h.SheetStart = sStart;
            PedestalPending("C", V(-13f, 0f, 21f), out _h.PedestalC, out _h.PendingC);
            _h.SheetEnd = _h.PedestalC;
        }

        Transform _props() => _ctx.Props;

        void PedestalPending(string id, Vector3 pos, out Transform at, out GameObject pending)
        {
            at = Kit.Make("Exhibit" + id + "Spot", _decor, pos).transform;
            pending = Kit.Make("Exhibit" + id + "Pending", _decor, pos);
            var t = pending.transform;
            Vis(t, "Stand", V(0f, 0.45f, 0f), V(0.6f, 0.9f, 0.6f), MatKey.Wood);
            Vis(t, "Top", V(0f, 0.915f, 0f), V(0.7f, 0.03f, 0.7f), MatKey.MetalDark);
            Text(t, V(0f, 1.35f, 0f), Quaternion.identity, "EXHIBIT " + id + "\n<size=60%>(PENDING)</size>", 0.2f, Gold, 1.6f);
            _ctx.MarkPoint("s1.exhibit" + id, pos);
        }
    }
}
