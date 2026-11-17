// =====================================================================================
//  SHIFTERS - L14Builder.Finale.cs   [SHIFT 14 - S2 filibuster, S3 vote, S4 catch + bench, S5 balcony, vista, annex]
// =====================================================================================
using UnityEngine;
using TMPro;

namespace Shifters
{
    public partial class L14Builder
    {
        // ------------------------------------------------------------------ S2 THE DEFENSE + THE FILIBUSTER
        void BuildS2()
        {
            _ctx.Checkpoint(V(5f, 0f, 19f), 0f);    // C2 (forced at "charges done"; walk-in disabled until then)
            // PRESS GALLERY x 10..16, z 27.6..36, top 3.0
            Slab(_geo, 10f, 16f, 27.6f, 36f, 3f, 3f, MatKey.BlackGlass, true);
            EnvKit.Railing(_geo, V(10f, 3f, 27.6f), V(10f, 3f, 30f), 1.1f);
            EnvKit.Railing(_geo, V(10f, 3f, 33f), V(10f, 3f, 36f), 1.1f);
            EnvKit.Railing(_geo, V(10f, 3f, 27.6f), V(12f, 3f, 27.6f), 1.1f);
            EnvKit.Railing(_geo, V(10f, 3f, 36f), V(16f, 3f, 36f), 1.1f);
            Sign(V(9.75f, 1.6f, 34.5f), 90f, "PRESS GALLERY: NO QUESTIONS. EVER.", 0.24f);
            // BROADCAST CONTROL desk + MUTE
            Solid(_geo, "BroadcastDesk", V(13f, 3.45f, 31.6f), V(3.2f, 0.9f, 0.8f), MatKey.MetalDark, 0.03f);
            Vis(_decor, "DeskScreens", V(13f, 4.25f, 31.85f), V(2.8f, 0.7f, 0.08f), MatKey.Screen, 0.01f);
            Text(_decor, V(13f, 4.25f, 31.8f), Quaternion.identity, "ON AIR: ME (9 H REMAINING)", 0.16f, UIColors.Danger, 2.7f);
            var mute = PuzzleKit.PushButton(_puz, V(13f, 3f, 30.5f), Yaw(180f), 9999f);
            _ctx.Mark("s2.mute", mute.gameObject);
            _ctx.MarkPoint("s2.muteStand", V(13f, 3f, 29.4f), 0f);
            Sign(V(13f, 5.2f, 30.6f), 0f, "MUTE: MOST PRESSED BUTTON IN HISTORY", 0.22f);
            _h.Mute = mute;
            // FOLDING STAIRS from the gallery's west edge down to x 4
            var fold = PuzzleKit.Signal(_puz, "s2.foldSignal");
            var ramp = PuzzleKit.FoldingRamp(_puz, V(10f, 3f, 31.5f), -90f, 3f, 6f, 3f, true, "STAIRS (FOLDED. LIKE HIS ARGUMENTS.)", fold);
            _ctx.Mark("s2.fold", ramp.gameObject);
            _h.FoldSignal = fold; _h.Fold = ramp;
            var decal = Kit.Make("FoldDecal", _decor, V(7f, 0f, 31.5f));
            Vis(decal.transform, "Stripe", V(0f, 0.006f, 0f), V(6f, 0.012f, 3f), MatKey.HazardStripes, 0f);
            Text(decal.transform, V(0f, 0.03f, -1.8f), Quaternion.Euler(90f, 0f, 0f), "STAIRS UNFOLDING", 0.3f, UIColors.AccentYellow, 5f);
            _h.FoldDecal = decal;
            _ctx.MarkPoint("s2.foldTop", V(10.9f, 3f, 31.5f));
            _ctx.MarkPoint("s2.foldFoot", V(3.3f, 0f, 31.5f));
            // THE WORD STAIR (falling LetterBlocks) + POPULAR (the catch mat) + HUMBLE (on the railing post)
            var w = new L14Word[8];
            w[0] = Word(0, "I", V(14f, 0.25f, 15f), V(4f, 0.5f, 2f), Vector3.back);
            w[1] = Word(1, "AM", V(14f, 0.5f, 17f), V(4f, 1f, 2f), Vector3.back);
            w[2] = Word(2, "VERY", V(14f, 0.75f, 19f), V(4f, 1.5f, 2f), Vector3.back);
            w[3] = Word(3, "POPULAR", V(-2f, 0.4f, 31.5f), V(3f, 0.8f, 2f), Vector3.back, false);
            w[4] = Word(4, "AND", V(14f, 1.0f, 22.6f), V(4f, 2f, 2f), Vector3.back);
            w[5] = Word(5, "TALL", V(14f, 1.25f, 24.6f), V(4f, 2.5f, 2f), Vector3.back);
            w[6] = Word(6, "AND", V(14f, 1.5f, 26.6f), V(4f, 3f, 2f), Vector3.back);
            w[7] = Word(7, "HUMBLE", V(10.3f, 4.45f, 29f), V(0.6f, 0.7f, 1.4f), Vector3.right);
            w[7].gameObject.AddComponent<L14Humble>();
            _ctx.Mark("s2.humble", w[7].gameObject);
            _h.Words = w;
            FloorText(V(14f, 0f, 20.8f), "DRAMATIC PAUSE\n1.6 M", 0.2f, UIColors.Speed, 0f, 3.5f);
            // speech clouds the words fall out of
            SpeechCloud(V(14f, 13.6f, 21f), V(5.2f, 1.6f, 15f), "BLAH BLAH BLAH ME ME ME BLAH");
            SpeechCloud(V(-2f, 13.4f, 31.5f), V(3.8f, 1.2f, 2.8f), "...");
            SpeechCloud(V(10.3f, 17.0f, 29f), V(2.2f, 1.0f, 2.2f), "...");
            // bot marks (S2)
            _ctx.MarkPoint("s2.laneS", V(-3f, 0f, 9.5f));
            _ctx.MarkPoint("s2.laneS2", V(5f, 0f, 10.0f));
            _ctx.MarkPoint("s2.stairFoot", V(14f, 0f, 12.6f), 0f);
            _ctx.MarkPoint("s2.step.I", V(14f, 0.5f, 15.2f));
            _ctx.MarkPoint("s2.step.AM", V(14f, 1.0f, 17.1f));
            _ctx.MarkPoint("s2.step.VERY", V(14f, 1.5f, 18.9f));
            _ctx.MarkPoint("s2.pause.takeoff", V(14f, 1.5f, 19.55f), 0f);
            _ctx.MarkPoint("s2.pause.landing", V(14f, 2.0f, 22.6f), 0f);
            _ctx.MarkPoint("s2.step.TALL", V(14f, 2.5f, 24.6f));
            _ctx.MarkPoint("s2.step.AND2", V(14f, 3.0f, 26.6f));
            // the defense table + witness stand inside the dock (hidden at the fold)
            var props = Kit.Make("DockProps", _decor, Vector3.zero).transform;
            Vis(props, "DefenseTable", V(-2f, 1.4f, 38.4f), V(1.0f, 0.8f, 0.5f), MatKey.Wood);
            Text(props, V(-2f, 1.45f, 38.13f), Quaternion.identity, "DEFENSE: ME", 0.12f, Gold, 1f);
            Vis(props, "WitnessStand", V(2f, 1.5f, 38.4f), V(0.9f, 1.0f, 0.5f), MatKey.Wood);
            Text(props, V(2f, 1.6f, 38.13f), Quaternion.identity, "WITNESS: ME", 0.12f, Gold, 1f);
            _h.DockProps = props.gameObject;
            _h.DefenseSpot = Kit.Make("DefenseSpot", _play, V(-2f, 1f, 39.0f), Yaw(180f)).transform;
            _h.WitnessSpot = Kit.Make("WitnessSpot", _play, V(2f, 1f, 39.0f), Yaw(180f)).transform;
            // dock spotlight (shadowless, monologue lighting)
            var spotGo = Kit.Make("DockSpot", _ctx.Lights, V(0f, 9.5f, 35.5f), Quaternion.LookRotation(V(0f, 1.2f, 39.5f) - V(0f, 9.5f, 35.5f)));
            var spot = spotGo.AddComponent<Light>();
            spot.type = LightType.Spot; spot.range = 16f; spot.spotAngle = 38f; spot.intensity = 6f; spot.color = UIColors.Hex(0xFFE2A8);
            spot.shadows = LightShadows.None;
            spot.enabled = false;
            _h.DockSpot = spot;
            // ghosts in the gallery of the public
            var ghosts = StoryKit.GhostTrio(_decor, V(-21f, 6f, 30f), Yaw(-90f), new[] { "MARKETING", "IT", "BOWLING LEAGUE" });
            _h.Ghosts = ghosts != null ? ghosts.transform : null;
            Text(_decor, V(-19.2f, 8.4f, 30f), Yaw(-90f), "BOO = DISAPPROVAL\n<size=60%>(NOT SPOOKY)</size>", 0.3f, UIColors.Info, 4f);
        }

        L14Word Word(int i, string text, Vector3 center, Vector3 size, Vector3 nudge, bool stair = true)
        {
            // stair words + HUMBLE read from the hall (west); POPULAR reads from the south (the catch side)
            bool west = i != 3;
            var rot = west ? Yaw(90f) : Quaternion.identity;
            Vector3 local = west ? V(size.z, size.y, size.x) : size;
            var go = StoryKit.LetterBlock(_puz, center, west ? EnvKit.FacingViewer(Vector3.left) : EnvKit.FacingViewer(Vector3.back), text, local);
            go.name = "Word_" + i + "_" + text;
            var rb = go.GetComponent<Rigidbody>(); if (rb == null) rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true; rb.useGravity = false; rb.interpolation = RigidbodyInterpolation.Interpolate;
            var word = go.AddComponent<L14Word>();
            word.Index = i; word.Word = text; word.RestCenter = center; word.FootSize = size; word.NudgeDir = nudge; word.Body = rb;
            word.BlipPitch = 0.62f;
            if (i != 7)
            {
                var sh = Kit.Make("Shadow_" + text, _decor, V(center.x, 0.012f, center.z)).transform;
                Vis(sh, "Decal", Vector3.zero, V(size.x, 0.01f, size.z), MatKey.Shadow, 0f);
                word.Shadow = sh;
            }
            var lab = Kit.Make("Incoming_" + text, _decor, V(center.x - (west ? size.x * 0.5f + 0.1f : 0f), center.y + size.y * 0.5f + 1.6f, center.z - (west ? 0f : size.z * 0.5f + 0.1f)));
            Text(lab.transform, Vector3.zero, west ? Yaw(90f) : Quaternion.identity, "INCOMING WORD\n<size=60%>LANDS IN 1.5 s</size>", 0.26f, UIColors.AccentYellow, 3.5f);
            word.IncomingLabel = lab;
            _ctx.Mark("s2.word." + L14Hearing.WordNames[i], go);
            return word;
        }

        void SpeechCloud(Vector3 c, Vector3 size, string text)
        {
            var t = Kit.Make("SpeechCloud", _decor, c).transform;
            Vis(t, "Cloud", Vector3.zero, size, MatKey.White, Mathf.Min(0.6f, size.y * 0.45f));
            for (int i = 0; i < 3; i++)
                Sphere(t, "Puff", V((i - 1) * size.x * 0.28f, size.y * 0.45f, (i - 1) * size.z * 0.22f), size.y * 0.55f, MatKey.White);
            Text(t, V(-size.x * 0.5f - 0.03f, 0f, 0f), Yaw(90f), text, Mathf.Min(0.5f, size.y * 0.35f), UIColors.Hex(0x30343A), size.z * 0.95f);
            Text(t, V(0f, -size.y * 0.5f - 0.03f, 0f), Quaternion.Euler(-90f, 0f, 0f), "SPEECH CLOUD", 0.3f, UIColors.Hex(0x30343A), size.x);
        }

        // ------------------------------------------------------------------ S3 THE VOTE
        void BuildS3()
        {
            _ctx.Checkpoint(V(-6f, 0f, 20f), 0f);   // C3 (forced at MUTE)
            var vote = PuzzleKit.Signal(_puz, "s3.voteSignal");
            _ctx.Mark("s3.vote", vote.gameObject);
            _h.VoteSignal = vote;
            float[] xs = { -6f, 0f, 6f };
            for (int n = 0; n < 3; n++)
            {
                float x = xs[n];
                var plate = Kit.Make("VoterPlate" + (n + 1), _puz, V(x, 0f, 25f)).transform;
                Vis(plate, "Rim", V(0f, 0.006f, 0f), V(2f, 0.012f, 2f), MatKey.Gold, 0f);
                Vis(plate, "Inner", V(0f, 0.01f, 0f), V(1.7f, 0.012f, 1.7f), MatKey.PuzzleBody, 0f);
                Text(plate, V(0f, 0.03f, -0.35f), Quaternion.Euler(90f, 0f, 0f), "VOTER " + (n + 1), 0.26f, Gold, 1.7f);
                Text(plate, V(0f, 0.03f, 0.35f), Quaternion.Euler(90f, 0f, 0f), "ONE INTERN, ONE VOTE", 0.12f, Gold, 1.7f);
                _h.Plates[n] = plate;
                _ctx.MarkPoint("s3.v" + (n + 1), V(x, 0f, 25f));
                _ctx.MarkPoint("s3.v" + (n + 1) + ".stand", V(x, 0f, 25.45f), 0f);
                // lectern + AYE key (5 s, re-pressable)
                Solid(_geo, "Lectern" + (n + 1), V(x, 0.6f, 27.25f), V(1.2f, 1.2f, 0.5f), MatKey.Wood, 0.03f);
                Vis(_decor, "LecternTop", V(x, 1.22f, 27.2f), V(1.3f, 0.05f, 0.65f), MatKey.Gold, 0.01f);
                var aye = PuzzleKit.PushButton(_puz, V(x, 0f, 26.72f), Yaw(180f), 5f);
                _ctx.Mark("s3.aye" + (n + 1), aye.gameObject);
                // AYE label on the pedestal's south (player) face, below its lamp (was boxed in between pedestal and lectern)
                Text(_decor, V(x, 0.33f, 26.44f), Quaternion.identity, "AYE", 0.2f, UIColors.Success, 0.46f);
                _h.Aye[n] = aye;
                // the bribe envelope (west end of the lectern)
                var env = Kit.Make("BribeEnvelope" + (n + 1), _puz, V(x - 0.45f, 1.27f, 27.25f));
                Vis(env.transform, "Envelope", Vector3.zero, V(0.42f, 0.04f, 0.28f), MatKey.Gold, 0.005f);
                Text(env.transform, V(0f, 0.03f, 0f), Quaternion.Euler(90f, 0f, 0f), "CONFIDENTIAL", 0.05f, Color.black, 0.4f);
                Kit.Trigger(env.transform, "EnvelopeVolume", V(0f, 0f, -0.3f), V(0.6f, 0.5f, 0.9f));
                var e = env.AddComponent<L14Envelope>();
                e.Lectern = n + 1; e.Home = env.transform.position;
                _h.Envelopes[n] = e;
                _ctx.Mark("s3.bribe" + (n + 1), env);
                _ctx.MarkPoint("s3.bribe" + (n + 1) + ".stand", V(x - 0.45f, 0f, 25.95f), 0f);
            }
            // Max's NAY button on the dock's front rail (outside the bars)
            // bot marks (S3)
            _ctx.MarkPoint("s3.laneE1", V(7.0f, 0f, 11.6f));
            _ctx.MarkPoint("s3.laneE2", V(7.0f, 0f, 21.5f));
            _ctx.MarkPoint("s3.v2.approach", V(1.6f, 0f, 25.0f));
            _ctx.MarkPoint("s3.laneW", V(-9.6f, 0f, 23.4f));
        }

        // ------------------------------------------------------------------ S4 THE CATCH + THE BENCH
        void BuildS4()
        {
            // DAIS x -5..5, z 36..44, top 1.0 + full-width front ramp z 33.5..36
            Slab(_geo, -5f, 5f, 36f, 44f, 1f, 1f, MatKey.Marble, true);
            EnvKit.Ramp(_geo, V(0f, 0f, 33.5f), V(0f, 1f, 36f), 10f, MatKey.Marble);
            Vis(_decor, "DaisRunner", V(0f, 1.006f, 40f), V(10f, 0.012f, 8f), MatKey.Velvet, 0f);
            // THE DOCK: gold cage with a barred roof (nobody can be thrown in) around the THRONE OF ONE
            var cage = Kit.Make("DockCage", _puz, V(0f, 0f, 0f));
            var ct = cage.transform;
            var crb = cage.AddComponent<Rigidbody>(); crb.isKinematic = true; crb.useGravity = false;
            void Bar(Vector3 c, Vector3 s) => Vis(ct, "Bar", c, s, MatKey.Gold, 0.01f);
            for (int i = 0; i <= 10; i++) { float x = -3f + i * 0.6f; Bar(V(x, 2.5f, 37.5f), V(0.08f, 3f, 0.08f)); Bar(V(x, 2.5f, 41.5f), V(0.08f, 3f, 0.08f)); }
            for (int i = 1; i < 7; i++) { float z = 37.5f + i * 0.5714f; Bar(V(-3f, 2.5f, z), V(0.08f, 3f, 0.08f)); Bar(V(3f, 2.5f, z), V(0.08f, 3f, 0.08f)); }
            Bar(V(0f, 4f, 37.5f), V(6.1f, 0.12f, 0.12f)); Bar(V(0f, 4f, 41.5f), V(6.1f, 0.12f, 0.12f));
            Bar(V(-3f, 4f, 39.5f), V(0.12f, 0.12f, 4.1f)); Bar(V(3f, 4f, 39.5f), V(0.12f, 0.12f, 4.1f));
            for (int i = 1; i < 6; i++) Bar(V(-3f + i, 4.02f, 39.5f), V(0.06f, 0.06f, 4f));
            foreach (var (c, s) in new[] { (V(0f, 2.5f, 37.5f), V(6.1f, 3f, 0.12f)), (V(0f, 2.5f, 41.5f), V(6.1f, 3f, 0.12f)), (V(-3f, 2.5f, 39.5f), V(0.12f, 3f, 4f)), (V(3f, 2.5f, 39.5f), V(0.12f, 3f, 4f)), (V(0f, 4.05f, 39.5f), V(6.1f, 0.1f, 4.1f)) })
            { var bc = cage.AddComponent<BoxCollider>(); bc.center = c; bc.size = s; }
            Text(ct, V(0f, 4.45f, 37.42f), Quaternion.identity, "THE DOCK (FORMERLY THE THRONE ROOM)", 0.22f, Gold, 6f);
            _h.Cage = ct;
            // Max's NAY button on the dock's front rail (outside the bars; lifts away with the cage at the vote)
            var nay = Kit.Make("NayButton", ct, V(1.5f, 2.2f, 37.25f));
            Vis(nay.transform, "Cap", Vector3.zero, V(0.5f, 0.2f, 0.5f), MatKey.WarningRed, 0.06f);
            Vis(ct, "NayPost", V(1.5f, 1.55f, 37.25f), V(0.15f, 1.1f, 0.15f), MatKey.MetalDark, 0.01f);
            nay.AddComponent<BoxCollider>().size = V(0.5f, 0.25f, 0.5f);
            _h.Nay = nay.AddComponent<L14NayButton>();
            _ctx.Mark("s3.nay", nay);
            Text(ct, V(1.5f, 2.75f, 37.2f), Quaternion.identity, "NAY BUTTON\n<size=60%>(ALWAYS PRESSED)</size>", 0.14f, UIColors.Danger, 1.6f);
            // THE THRONE OF ONE -> BENCH FOR THREE (collider-free; only Max sits here until the fold)
            var throne = Kit.Make("ThroneOfOne", _decor, V(0f, 1f, 39.5f), Yaw(180f)).transform;
            Vis(throne, "Seat", V(0f, 0.45f, 0f), V(1.25f, 0.18f, 0.9f), MatKey.Gold);
            Vis(throne, "Base", V(0f, 0.2f, 0f), V(1.1f, 0.4f, 0.8f), MatKey.Velvet);
            Vis(throne, "Back", V(0f, 1.4f, -0.5f), V(1.25f, 1.9f, 0.16f), MatKey.Gold);
            Vis(throne, "Cushion", V(0f, 1.3f, -0.4f), V(1.0f, 1.3f, 0.06f), MatKey.Velvet, 0.01f);
            _h.ThroneBack = Text(throne, V(0f, 2.05f, -0.36f), Yaw(180f), "ONE", 0.36f, UIColors.Hex(0x30343A), 1.2f);
            _h.Plaque = Text(throne, V(0f, 0.5f, 0.47f), Yaw(180f), "THRONE OF ONE\n<size=70%>(CAPACITY: ME)</size>", 0.11f, Color.black, 1.2f);
            Vis(throne, "PlaquePlate", V(0f, 0.5f, 0.455f), V(1.0f, 0.24f, 0.02f), MatKey.SignWhite, 0.005f);
            for (int s = -1; s <= 1; s += 2)
            {
                var side = Kit.Make(s < 0 ? "SideSeatL" : "SideSeatR", throne, Vector3.zero).transform;
                Vis(side, "Seat", V(0f, 0.42f, -0.02f), V(1.15f, 0.16f, 0.85f), MatKey.Gold);
                Vis(side, "Base", V(0f, 0.18f, 0f), V(1.0f, 0.36f, 0.75f), MatKey.Velvet);
                Vis(side, "Back", V(0f, 1.0f, -0.5f), V(1.15f, 1.2f, 0.14f), MatKey.Gold);
                // throne-local +X is world -X (yaw 180)
                if (s < 0) _h.SideSeatR = side; else _h.SideSeatL = side;
            }
            var hp = StoryKit.HoloPoster(_decor, V(4.2f, 1f, 42.6f), null, "ONE WORLD. ONE RULER. ONE CHAIR.", 4);
            _h.ThronePosterSticker = Text(_decor, V(4.2f, 1.6f, 42.4f), Quaternion.identity, "...THREE CHAIRS.", 0.22f, UIColors.Success, 1.4f);
            _h.ThronePosterSticker.gameObject.SetActive(false);
            _h.MaxHome = Kit.Make("MaxHome", _play, V(0f, 1.05f, 39.6f), Yaw(180f)).transform;
            _h.MaxSpot = Kit.Make("MaxSpot", _play, V(-4.2f, 1f, 37.0f), Yaw(135f)).transform;
            _ctx.MarkPoint("story.max", V(-4.2f, 1f, 37.0f), 135f);
            // the 2-TON GAVEL (collider-free), pivot (0, 10, 42.5); rest -65 deg (hidden behind the wall), slam +20 deg = the throne seat
            var gav = Kit.Make("GavelPivot", _decor, V(0f, 10f, 42.5f), Quaternion.Euler(-65f, 0f, 0f)).transform;
            Vis(gav, "Handle", V(0f, -4.3f, 0f), V(0.35f, 8.4f, 0.35f), MatKey.Wood, 0.05f);
            Vis(gav, "Head", V(0f, -9f, 0f), V(1.4f, 1.2f, 2.6f), MatKey.Wood, 0.12f);
            for (int s = -1; s <= 1; s += 2) Vis(gav, "Band", V(0f, -9f, s * 0.95f), V(1.5f, 1.3f, 0.18f), MatKey.Gold, 0.04f);
            Text(gav, V(0.72f, -9f, 0f), Yaw(-90f), "2 TONS\n<size=60%>(OF JUSTICE)</size>", 0.3f, Gold, 2.4f);
            _h.Gavel = gav;
            // POPULAR lands here: the catch mat + the gold CATCH RING (shown during the flight)
            var catchPt = Kit.Make("CatchPoint", _play, V(-2f, 0.8f, 31.5f)).transform;
            _h.CatchPoint = catchPt;
            _ctx.MarkPoint("s4.catch", V(-2f, 0.8f, 31.5f));
            var ring = Kit.Make("CatchRing", _decor, V(-2f, 0f, 31.5f));
            for (int i = 0; i < 20; i++)
            {
                float a = i * Mathf.PI * 2f / 20f;
                Vis(ring.transform, "Seg", V(Mathf.Sin(a) * L14Hearing.CatchRadius, 0.03f, Mathf.Cos(a) * L14Hearing.CatchRadius), V(0.12f, 0.05f, 0.62f), MatKey.Gold, 0f, Yaw(a * Mathf.Rad2Deg));
            }
            var col = Kit.Make("Beam", ring.transform, V(0f, 4f, 0f));
            ProcMesh.AddRenderer(col, ProcMesh.Cached("L14CatchBeam", () => ProcMesh.Cylinder(0.5f, 8f, 16, true)), Kit.Mat(MatKey.Hologram), false);
            Text(ring.transform, V(0f, 2.6f, -1.9f), Quaternion.identity, "CATCH HIM HERE!\n<size=60%>MAX LANDS IN 2.6 s</size>", 0.4f, Gold, 4f);
            _h.CatchRing = ring;
            // MUTED sprite above the dock
            var muted = Kit.Make("MutedSign", _decor, V(0f, 3.3f, 39.3f));
            Vis(muted.transform, "Panel", Vector3.zero, V(1.6f, 0.5f, 0.05f), MatKey.WarningRed, 0.02f);
            Text(muted.transform, V(0f, 0f, -0.04f), Quaternion.identity, "[MUTED]", 0.28f, Color.white, 1.5f);
            _h.MutedSign = muted;
            // THE BENCH FOR THREE (three seats, open after the fold)
            var bench = PuzzleKit.Signal(_puz, "s4.benchSignal");
            _ctx.Mark("s4.bench", bench.gameObject);
            _h.BenchSignal = bench;
            float[] sx = { -1.6f, 0f, 1.6f };
            for (int i = 0; i < 3; i++)
            {
                var root = Kit.Make("BenchSeat" + (i + 1), _puz, V(sx[i], 1f, 39.5f), Yaw(180f));
                var seatT = Kit.Make("Seat", root.transform, V(0f, 0.04f, -0.45f)).transform;
                var exitT = Kit.Make("Exit", _play, V(sx[i], 1f, 38.3f), Yaw(180f)).transform;
                Kit.Trigger(root.transform, "InteractVolume", V(0f, 1f, 0.2f), V(1.4f, 2f, 1.4f));
                var seat = root.AddComponent<L14BenchSeat>();
                seat.Seat = seatT; seat.ExitPoint = exitT;
                _h.Seats[i] = seat;
                _ctx.Mark("s4.seat" + (i + 1), root);
                _ctx.MarkPoint("s4.seatStand" + (i + 1), V(sx[i], 1f, 38.3f), 0f);
            }
            // clerk's desk + Percy in a tiny judge wig
            Solid(_geo, "ClerksDesk", V(-8f, 0.6f, 38f), V(2f, 1.2f, 1f), MatKey.Wood, 0.03f);
            Text(_decor, V(-8f, 0.75f, 37.48f), Quaternion.identity, "CLERK OF THE COURT\n<size=60%>(COFFEE MACHINE)</size>", 0.16f, Gold, 1.9f);
            var percy = StoryKit.PercyMachine(_decor, V(-8f, 1.2f, 38.1f), null);
            _ctx.Mark("story.percy", percy);
            _h.PercySpot = percy != null ? percy.transform : null;
            // bot marks (S4)
            _ctx.MarkPoint("s4.catch.lane", V(-1.7f, 0f, 26.0f));
            _ctx.MarkPoint("s4.catch.S", V(-2f, 0f, 29.9f), 0f);
            _ctx.MarkPoint("s4.catch.SW", V(-3.0f, 0f, 30.0f), 0f);
            _ctx.MarkPoint("s4.catch.SE", V(-0.9f, 0f, 30.0f), 0f);
            _ctx.MarkPoint("s4.lane.W0", V(-7.7f, 0f, 27.3f));   // west of lectern 1 (>= 0.8 m clear)
            _ctx.MarkPoint("s4.lane.W1", V(-5.6f, 0f, 31f));
            _ctx.MarkPoint("s4.popularPark", V(-2f, 0f, 31.3f));
            _ctx.MarkPoint("s4.parkLane", V(-3.0f, 0f, 22.8f));
            _ctx.MarkPoint("s4.lane.W2", V(-3.0f, 1f, 37.6f));
            _ctx.MarkPoint("s4.lane.E", V(0.8f, 0f, 29.6f));
            _ctx.MarkPoint("s4.ramp.C", V(0.4f, 0.5f, 34.8f));
            _ctx.MarkPoint("s4.lane.E2", V(4.0f, 0f, 30.0f));
            _ctx.MarkPoint("s4.ramp.E", V(2.8f, 0.5f, 34.8f));
        }

        // ------------------------------------------------------------------ S5 THE BALCONY OF THREE
        void BuildS5()
        {
            Slab(_geo, -5f, 5f, 44f, 52f, 1f, 1.6f, MatKey.Marble, true);
            for (int s = -1; s <= 1; s += 2)
            {
                Solid(_geo, "Parapet", V(s * 5f, 2.3f, 48f), V(0.15f, 2.6f, 8f), MatKey.Glass, 0f);
                Vis(_decor, "ParapetRail", V(s * 5f, 3.62f, 48f), V(0.25f, 0.08f, 8.1f), MatKey.Gold, 0.01f);
            }
            Solid(_geo, "ParapetN", V(0f, 2.3f, 52f), V(10.15f, 2.6f, 0.15f), MatKey.Glass, 0f);
            Vis(_decor, "ParapetRailN", V(0f, 3.62f, 52f), V(10.3f, 0.08f, 0.25f), MatKey.Gold, 0.01f);
            LevelKit.SafetyNet(_geo, V(0f, -8f, 48f), V(20f, 6f, 16f), V(0f, 1f, 42f), 180f);
            var doorSig = PuzzleKit.Signal(_puz, "s5.doorSignal");
            var door = PuzzleKit.Door(_puz, V(0f, 1f, 44f), null, DoorMode.SlideUp, 4f, 4f, doorSig);
            _ctx.Mark("s5.balconyDoor", door.gameObject);
            _h.DoorSignal = doorSig;
            Sign(V(0f, 5.6f, 43.75f), 0f, "BALCONY (AFTER THE SUNRISE)", 0.22f);
            // gold gazebo "BALCONY OF THREE"
            for (int sx = -1; sx <= 1; sx += 2) for (int sz = -1; sz <= 1; sz += 2)
                Vis(_decor, "GazeboPost", V(sx * 2.4f, 2.6f, 48.5f + sz * 1.9f), V(0.18f, 3.2f, 0.18f), MatKey.Gold, 0.02f);
            Vis(_decor, "GazeboRoof", V(0f, 4.3f, 48.5f), V(5.4f, 0.25f, 4.4f), MatKey.Gold, 0.05f);
            Vis(_decor, "GazeboCap", V(0f, 4.7f, 48.5f), V(3.2f, 0.5f, 2.6f), MatKey.Gold, 0.15f);
            Text(_decor, V(0f, 3.75f, 46.25f), Quaternion.identity, "BALCONY OF THREE\n<size=60%>CAPACITY: EXACTLY THREE</size>", 0.3f, Gold, 5f);
            var exit = _ctx.Exit(V(0f, 1f, 48.5f), 0f);
            _h.Exit = exit != null ? exit.GetComponentInChildren<LevelExit>(true) : null;
            _ctx.MarkPoint("s5.lane.W", V(-3.6f, 1f, 40.8f));
            _ctx.MarkPoint("s5.lane.E", V(3.6f, 1f, 40.8f));
            _ctx.MarkPoint("s5.lane.W2", V(-3.6f, 1f, 42.7f));
            _ctx.MarkPoint("s5.lane.E2", V(3.6f, 1f, 42.7f));
            _ctx.MarkPoint("s5.front.W", V(-1.0f, 1f, 42.9f));
            _ctx.MarkPoint("s5.front.E", V(1.0f, 1f, 42.9f));
            _ctx.MarkPoint("s5.front.C", V(0f, 1f, 42.9f));
            _ctx.MarkPoint("s5.door.W", V(-1.0f, 1f, 45.4f));
            _ctx.MarkPoint("s5.door.E", V(1.0f, 1f, 45.4f));
            _ctx.MarkPoint("s5.door.C", V(0f, 1f, 45.8f));
        }

        // ------------------------------------------------------------------ VISTA (collider-free, z > 52)
        void BuildVista()
        {
            var v = _ctx.Section("Vista", Vector3.zero);
            EnvKit.Decor.Tower.CitySkyline(v, V(0f, -26f, 70f), null, 260f, 14, false, 1.6f, null);
            string[] acts = { "THE FACTORY", "THE LAB", "EXAM ISLANDS", "THE BANK", "OLD HQ" };
            MatKey[] mats = { MatKey.MetalRust, MatKey.PuzzleAccent, MatKey.Foliage, MatKey.Marble, MatKey.Concrete };
            for (int i = 0; i < 5; i++)
            {
                float x = -80f + i * 40f, z = 150f + (i % 2) * 20f;
                Vis(v, "Act_" + acts[i], V(x, -10f, z), V(22f, 26f, 14f), mats[i], 0.4f);
                Text(v, V(x, 4.5f, z - 7.1f), Quaternion.identity, acts[i], 2.6f, UIColors.Hex(0xFFE9B0), 22f);
            }
            var statue = StoryKit.VillainStatue(v, V(0f, -8f, 84f), null, 16f, 0);
            _h.MaxStatue = statue != null ? statue.transform : null;
            Vis(v, "StatueRoof", V(0f, -8.5f, 84f), V(30f, 1f, 22f), MatKey.BlackGlass, 0.1f);
            // the founders' statue rises at "story.statue" (ShiftocracyEnding spawns it there), scaled up for the vista
            var riser = Kit.Make("FoundersRiser", v, V(0f, -8f, 76f));
            riser.transform.localScale = Vector3.one * 5f;
            _ctx.Mark("story.statue", riser);
            _h.StatueRiser = riser.transform;
            riser.SetActive(false);
            // three hidden decree sets (rise 1.5 s on c15.vista.<archetype>)
            _h.VistaSets[0] = VistaBrute(v); _h.VistaSets[1] = VistaSprinter(v); _h.VistaSets[2] = VistaBrainiac(v);
        }

        GameObject VistaBrute(Transform v)
        {
            var root = Kit.Make("Vista_Brute", v, Vector3.zero);
            var t = root.transform;
            for (int i = -1; i <= 1; i++)
            {
                float x = i * 40f, z = 120f;
                Vis(t, "BreadB", V(x, -12f, z), V(16f, 5f, 12f), MatKey.Wood, 1.2f);
                Vis(t, "Lettuce", V(x, -8.6f, z), V(17f, 1.6f, 13f), MatKey.Foliage, 0.6f);
                Vis(t, "Cheese", V(x, -7.2f, z), V(16.5f, 1.2f, 12.5f), MatKey.HazardYellow, 0.3f);
                Vis(t, "Tomato", V(x, -5.9f, z), V(15.5f, 1.4f, 11.5f), MatKey.WarningRed, 0.5f);
                Vis(t, "BreadT", V(x, -2.6f, z), V(16f, 5.2f, 12f), MatKey.Wood, 2.2f);
                Text(t, V(x, 3f, z - 6.2f), Quaternion.identity, "PUSH", 3.2f, UIColors.Strength, 16f);
            }
            Text(t, V(0f, 10f, 118f), Quaternion.identity, "DECREE: SANDWICHES FOR EVERYONE", 2.6f, UIColors.AccentYellow, 60f);
            return root;
        }

        GameObject VistaSprinter(Transform v)
        {
            var root = Kit.Make("Vista_Sprinter", v, Vector3.zero);
            var t = root.transform;
            for (int i = 0; i < 5; i++)
            {
                float x = -60f + i * 30f, z = 108f + (i % 2) * 10f;
                Vis(t, "Banner", V(x, -6f, z), V(6f, 28f, 0.3f), MatKey.SafetyOrange, 0.05f);
                for (int k = 0; k < 4; k++) Text(t, V(x, 4f - k * 6f, z - 0.2f), Quaternion.identity, "»", 5f, Color.white, 6f);
            }
            Vis(t, "Stripe1", V(-3f, -0.2f, 96f), V(1.2f, 0.1f, 60f), MatKey.SafetyOrange, 0f);
            Vis(t, "Stripe2", V(3f, -0.2f, 96f), V(1.2f, 0.1f, 60f), MatKey.SafetyOrange, 0f);
            Text(t, V(0f, 11f, 110f), Quaternion.identity, "DECREE: NO WALKING IN THE HALLWAYS (RUN)", 2.6f, UIColors.Speed, 70f);
            return root;
        }

        GameObject VistaBrainiac(Transform v)
        {
            var root = Kit.Make("Vista_Brainiac", v, Vector3.zero);
            var t = root.transform;
            string[] boards = { "FREE HINT TODAY: LOOK UP", "BEIGE: UNDER REVIEW", "HOMEWORK: ABOLISHED" };
            for (int i = 0; i < 3; i++)
            {
                float x = -46f + i * 46f, z = 100f + (i == 1 ? 12f : 0f);
                Vis(t, "Board", V(x, 3f, z), V(26f, 10f, 0.6f), MatKey.Screen, 0.1f);
                Vis(t, "Pole", V(x, -12f, z + 0.5f), V(1.2f, 20f, 1.2f), MatKey.MetalDark, 0.1f);
                Text(t, V(x, 3f, z - 0.35f), Quaternion.identity, boards[i], 2.0f, UIColors.Intellect, 24f);
            }
            return root;
        }

        // ------------------------------------------------------------------ S6 ORIENTATION ANNEX (C16 set, off-path, unreachable)
        void BuildAnnex()
        {
            var a = _ctx.Section("S6_Annex", Vector3.zero);
            Slab(_geo, 60f, 72f, -6f, 6f, 0f, 0.6f, MatKey.Floor, true);
            EnvKit.Wall(_geo, V(60f, 0f, 6f), V(72f, 0f, 6f), 4f, 0.4f, MatKey.Wall, true);
            EnvKit.Wall(_geo, V(60f, 0f, -6f), V(60f, 0f, 6f), 4f, 0.4f, MatKey.Wall, true);
            EnvKit.Wall(_geo, V(72f, 0f, -6f), V(72f, 0f, 6f), 4f, 0.4f, MatKey.Wall, true);
            EnvKit.Wall(_geo, V(60f, 0f, -6f), V(64.5f, 0f, -6f), 4f, 0.4f, MatKey.Wall, true);
            EnvKit.Wall(_geo, V(67.5f, 0f, -6f), V(72f, 0f, -6f), 4f, 0.4f, MatKey.Wall, true);
            Vis(_decor, "AnnexDoor", V(66f, 1.4f, -6.05f), V(3f, 2.8f, 0.1f), MatKey.PuzzleAccent, 0.02f);
            Solid(_geo, "AnnexDesk", V(66f, 0.45f, 2.5f), V(2.4f, 0.9f, 1f), MatKey.Wood, 0.03f);
            Vis(_decor, "Nameplate", V(66f, 1.0f, 2.0f), V(1.2f, 0.22f, 0.06f), MatKey.SignWhite, 0.01f);
            Text(_decor, V(66f, 1.0f, 1.96f), Quaternion.identity, "INTERN #4: MAX", 0.13f, Color.black, 1.2f);
            EnvKit.Poster(_decor, V(66f, 2.4f, 5.75f), Quaternion.identity, "WELCOME TO SHIFTERS", "YOU HAVE THREE COWORKERS. TRY NOT TO LOSE THEM.", new Vector2(2.2f, 1.6f));
            _ctx.MarkPoint("story.annex.door", V(66f, 0f, -4.6f), 0f);
            _ctx.MarkPoint("story.annex.desk", V(66f, 0f, 3.4f), 180f);
            _ctx.MarkPoint("story.annex.cam", V(66f, 2.2f, -3.5f), 0f);
        }

        // ------------------------------------------------------------------ HINTS (real / vague)
        void BuildHints()
        {
            _ctx.Hint(V(9f, 2f, 9f), V(6f, 4f, 12f), "The Brute pushes the filing cabinet onto the Scale of Justice. It only weighs evidence.", "Heavy evidence needs heavy arms.");
            _ctx.Hint(V(-11.5f, 2f, 10.5f), V(9f, 4f, 7f), "The smart one stands by the shelf; another hops on their head and climbs up. Bring the Books to tray B.", "Teams are taller than people.");
            _ctx.Hint(V(-12.5f, 2f, 19f), V(7f, 4f, 5f), "Only a big brain (IQ 7+) opens the evidence locker.", "Laundry day. Ask the smart one.");
            _ctx.Hint(V(12f, 2f, 22f), V(8f, 5f, 16f), "Climb his words to the press gallery and press MUTE. Jump the dramatic pause.", "His lies are stairs.");
            _ctx.Hint(V(0f, 2f, 25.5f), V(16f, 4f, 4f), "One intern per voter plate (G = stay), then AYE, SHIFT, AYE, SHIFT, AYE within 5 s.", "All in favor? All of you.");
            _ctx.Hint(V(0f, 2.5f, 38.5f), V(10f, 4f, 5f), "Everybody sits on the bench (E). All three.", "Take a seat. Together.");
        }
    }
}
