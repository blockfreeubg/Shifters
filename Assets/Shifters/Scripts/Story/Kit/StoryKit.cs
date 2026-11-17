// =====================================================================================
//  SHIFTERS - StoryKit.cs   [owned by STORY_KIT]
//  Story props for levels (CAMPAIGN §18.2). Kit.cs conventions: built entirely inside the factory (edit-mode safe),
//  ProcMesh.Cached meshes, positions LOCAL to 'parent', signature (Transform parent, Vector3 pos, Quaternion? rot, ...).
//  COLLIDER-FREE unless noted (only LetterBlock has a static box collider; ScareKit.PhoneCall has an interaction trigger).
//
//  FACING (same as every EnvKit mounted piece, see EnvKitSigns.cs): 'rot' = the VIEWER'S LOOK. The front / readable
//  face of every StoryKit piece points along rot * Vector3.back; Quaternion.identity (or null) = readable from the -Z
//  side. Get rot from EnvKit.FacingViewer(dirTowardViewer) / EnvKit.FacingViewerAt(piecePos, viewerPos) /
//  EnvKit.WallMountFacing(wallNormal). The returned root's transform.forward points OUT of the front.
//  'pos' is the floor point under the piece (statues, Percy, posters, tombstones, taunt spots) except MeTvScreen
//  (pos = screen center, it floats) and ScriptTrigger (center of the box).
//  This file: shared helpers + MeTvScreen, HoloPoster, VillainStatue, PercyMachine, ScriptTrigger, HologramTauntSpot,
//  LineSpot. StoryKit.Park.cs: Tombstone, TombstoneRow, GhostTrio. StoryKit.Finale.cs: ExhibitProp, TallyBoard,
//  LetterBlock, FoundersStatue, CitizenCrowd.
// =====================================================================================
using TMPro;
using UnityEngine;

namespace Shifters
{
    public static partial class StoryKit
    {
        // ------------------------------------------------------------------ shared helpers
        public static readonly Color HoloGold = new Color(0.91f, 0.72f, 0.23f, 0.8f);

        static GameObject Root(string name, Transform p, Vector3 pos, Quaternion? rot) =>
            Kit.Make(name, p, pos, EnvKit.ViewToFace(rot ?? Quaternion.identity));

        static MeshRenderer Part(Transform parent, string name, Mesh mesh, Material mat, Vector3 lp, Quaternion? lr = null, Vector3? ls = null, bool shadows = true)
        {
            var go = ProcMesh.Part(name, parent, mesh, mat, lp, lr, ls, shadows);
            return go != null ? go.GetComponent<MeshRenderer>() : null;
        }

        static TextMeshPro Text(Transform parent, string text, Vector3 lp, Vector2 area, Color c, TMP_FontAsset font, float maxH) =>
            EnvKit.AddText(parent, text, lp, area, c, font, maxH);

        static Mesh Sphere(float r) => ProcMesh.Cached(EnvMeshes.Key("story_sph", r), () => ProcMesh.Sphere(r, 12, 8));
        static Mesh Cyl(float r, float h) => ProcMesh.Cached(EnvMeshes.Key("story_cyl", r, h), () => ProcMesh.Cylinder(r, h, 20, true));
        static Mesh Ring(float major, float minor) => ProcMesh.Cached(EnvMeshes.Key("story_ring", major, minor), () => ProcMesh.Torus(major, minor, 24, 6));

        /// <summary>White lit material: vertex colors of StoryKitMeshes show through.</summary>
        public static Material Painted => MaterialLibrary.Colored(Color.white, 0.3f);
        public static Material StatueStone => MaterialLibrary.Colored(new Color(0.64f, 0.66f, 0.69f), 0.12f);
        public static Material HoloMaterial(Color c) => MaterialLibrary.Hologram(c);
        static Material Glow(Color c, float g = 2f) => MaterialLibrary.Unlit(c, g);

        /// <summary>Accent color per story act (1 factory gold, 2 bank green, 3 night violet, 4 tower red).</summary>
        public static Color ActColor(int act)
        {
            switch (act)
            {
                case 2: return StoryKitMeshes.Hex(0x3DDC97);
                case 3: return StoryKitMeshes.Hex(0xB07CFF);
                case 4: return StoryKitMeshes.Hex(0xE8394B);
                default: return StoryKitMeshes.Hex(0xE8B83A);
            }
        }

        // ------------------------------------------------------------------ ME-TV
        /// <summary>Floating ME-TV billboard (16:9, 'width' m wide; pos = screen center). Idle: Max portrait, headline,
        /// cycling ticker, blinking LIVE dot. During a broadcast (GameEvents.BroadcastChanged) it shows VillainStudio's
        /// texture when visuals provides one. Component: MeTvScreenView (SetHeadline). Collider-free.</summary>
        public static GameObject MeTvScreen(Transform p, Vector3 pos, Quaternion? rot, float width = 4f)
        {
            float w = Mathf.Max(1f, width), h = w * 0.5625f;
            var root = Root("MeTvScreen", p, pos, rot);
            var t = root.transform;
            float f = Mathf.Max(0.06f, w * 0.022f);
            Kit.BoxVisual(t, "Housing", new Vector3(0f, 0f, -0.13f), new Vector3(w + f * 4f, h + f * 4f, 0.22f), MatKey.MetalDark, 0.04f);
            Kit.BoxVisual(t, "FrameT", new Vector3(0f, h * 0.5f + f * 0.5f, 0f), new Vector3(w + f * 2f, f, 0.07f), MatKey.Gold, 0.01f, null, false);
            Kit.BoxVisual(t, "FrameB", new Vector3(0f, -h * 0.5f - f * 0.5f, 0f), new Vector3(w + f * 2f, f, 0.07f), MatKey.Gold, 0.01f, null, false);
            Kit.BoxVisual(t, "FrameL", new Vector3(-w * 0.5f - f * 0.5f, 0f, 0f), new Vector3(f, h, 0.07f), MatKey.Gold, 0.01f, null, false);
            Kit.BoxVisual(t, "FrameR", new Vector3(w * 0.5f + f * 0.5f, 0f, 0f), new Vector3(f, h, 0.07f), MatKey.Gold, 0.01f, null, false);
            var screen = Part(t, "Screen", ProcMesh.CachedBox(new Vector3(w, h, 0.02f), 0f), Glow(StoryKitMeshes.Hex(0x1B2233), 1f), Vector3.zero, null, null, false);
            // ticker strip + texts
            float th = h * 0.13f;
            Part(t, "TickerStrip", ProcMesh.CachedBox(new Vector3(w, th, 0.01f), 0f), Glow(StoryKitMeshes.Hex(0xB8182A), 1.2f), new Vector3(0f, -h * 0.5f + th * 0.5f, 0.016f), null, null, false);
            var ticker = Text(t, StoryKitLines.Ticker[0], new Vector3(0f, -h * 0.5f + th * 0.5f, 0.024f), new Vector2(w * 0.94f, th * 0.8f), Color.white, ShiftersFonts.Label, th * 0.7f);
            var logo = Text(t, "ME-TV", new Vector3(-w * 0.36f, h * 0.41f, 0.02f), new Vector2(w * 0.24f, h * 0.13f), StoryKitLines.MaxColor, ShiftersFonts.Display, h * 0.12f);
            var live = Part(t, "LiveDot", Sphere(Mathf.Max(0.04f, h * 0.025f)), Glow(new Color(1f, 0.15f, 0.15f), 2.5f), new Vector3(w * 0.33f, h * 0.41f, 0.03f), null, null, false);
            Text(t, "LIVE", new Vector3(w * 0.4f, h * 0.41f, 0.02f), new Vector2(w * 0.1f, h * 0.09f), Color.white, ShiftersFonts.Display, h * 0.08f);
            var headline = Text(t, "SHIFT CHANGE IN: NEVER.", new Vector3(w * 0.17f, h * 0.02f, 0.02f), new Vector2(w * 0.52f, h * 0.5f), Color.white, ShiftersFonts.Display, h * 0.14f);
            float k = h * 0.74f / 1.45f;
            var portrait = Part(t, "Portrait", StoryKitMeshes.MaxFigure(false), Glow(Color.white, 1f), new Vector3(-w * 0.24f, -h * 0.5f + th + 0.01f, 0.03f), null, new Vector3(k, k, k * 0.06f), false);
            // hover pods + antenna
            for (int s = -1; s <= 1; s += 2)
            {
                Part(t, "Pod", Cyl(0.16f, 0.22f), Kit.Mat(MatKey.MetalLight), new Vector3(s * w * 0.32f, -h * 0.5f - f * 2f - 0.14f, -0.13f));
                Part(t, "PodGlow", Ring(0.15f, 0.035f), Glow(new Color(0.4f, 0.9f, 1f), 2.5f), new Vector3(s * w * 0.32f, -h * 0.5f - f * 2f - 0.27f, -0.13f), null, null, false);
            }
            Part(t, "Antenna", Cyl(0.025f, 0.6f), Kit.Mat(MatKey.MetalLight), new Vector3(w * 0.42f, h * 0.5f + f * 2f + 0.3f, -0.13f));
            Part(t, "AntennaTip", Sphere(0.06f), Glow(new Color(1f, 0.2f, 0.2f), 2f), new Vector3(w * 0.42f, h * 0.5f + f * 2f + 0.62f, -0.13f), null, null, false);
            var view = root.AddComponent<MeTvScreenView>();
            view.Screen = screen; view.Portrait = portrait != null ? portrait.gameObject : null; view.Ticker = ticker; view.Logo = logo;
            view.Headline = headline; view.LiveDot = live;
            return root;
        }

        // ------------------------------------------------------------------ holo-poster
        /// <summary>Free-standing propaganda hologram (§24.4 texts in StoryKitLines.Posters): emitter puck + 1.3 x 1.8 m
        /// panel (top at ~2.45 m). act 1..4 picks the accent color. pos = floor point. Collider-free.</summary>
        public static GameObject HoloPoster(Transform p, Vector3 pos, Quaternion? rot, string text, int act)
        {
            var root = Root("HoloPoster", p, pos, rot);
            var t = root.transform;
            var accent = ActColor(act);
            Part(t, "Puck", Cyl(0.34f, 0.12f), Kit.Mat(MatKey.MetalDark), new Vector3(0f, 0.06f, 0f));
            Part(t, "PuckGlow", Ring(0.3f, 0.03f), Glow(accent, 2.5f), new Vector3(0f, 0.125f, 0f), null, null, false);
            Part(t, "Stem", Cyl(0.035f, 0.42f), Kit.Mat(MatKey.MetalLight), new Vector3(0f, 0.33f, 0f), null, null, false);
            const float pw = 1.3f, ph = 1.8f, cy = 1.45f;
            var a = accent; a.a = 0.32f;
            Part(t, "Panel", ProcMesh.CachedBox(new Vector3(pw, ph, 0.02f), 0f), HoloMaterial(a), new Vector3(0f, cy, 0f), null, null, false);
            var g = Glow(accent, 1.8f);
            Part(t, "EdgeT", ProcMesh.CachedBox(new Vector3(pw, 0.035f, 0.03f), 0f), g, new Vector3(0f, cy + ph * 0.5f, 0f), null, null, false);
            Part(t, "EdgeB", ProcMesh.CachedBox(new Vector3(pw, 0.035f, 0.03f), 0f), g, new Vector3(0f, cy - ph * 0.5f, 0f), null, null, false);
            Part(t, "EdgeL", ProcMesh.CachedBox(new Vector3(0.035f, ph, 0.03f), 0f), g, new Vector3(-pw * 0.5f, cy, 0f), null, null, false);
            Part(t, "EdgeR", ProcMesh.CachedBox(new Vector3(0.035f, ph, 0.03f), 0f), g, new Vector3(pw * 0.5f, cy, 0f), null, null, false);
            Text(t, "MINISTRY OF ME", new Vector3(0f, cy + ph * 0.42f, 0.02f), new Vector2(pw * 0.9f, 0.12f), accent, ShiftersFonts.Label, 0.1f);
            float k = 0.5f / 1.45f;
            Part(t, "Emblem", StoryKitMeshes.MaxFigure(false), HoloMaterial(HoloGold), new Vector3(0f, cy + ph * 0.14f, 0.02f), null, new Vector3(k, k, k * 0.2f), false);
            // dark smoked strip behind the slogan: keeps the white text readable on bright / pastel level backgrounds
            Part(t, "SloganBack", ProcMesh.CachedBox(new Vector3(pw * 0.94f, ph * 0.42f, 0.004f), 0f), MaterialLibrary.Transparent(new Color(0.03f, 0.035f, 0.08f, 0.86f)), new Vector3(0f, cy - ph * 0.18f, 0.013f), null, null, false);
            Text(t, (text ?? "").ToUpperInvariant(), new Vector3(0f, cy - ph * 0.18f, 0.02f), new Vector2(pw * 0.88f, ph * 0.36f), Color.white, ShiftersFonts.Display, 0.2f);
            Text(t, "- MAXIMILIAN ME, SUPREME SOLE EXECUTIVE", new Vector3(0f, cy - ph * 0.43f, 0.02f), new Vector2(pw * 0.9f, 0.08f), accent, ShiftersFonts.Label, 0.065f);
            return root;
        }

        // ------------------------------------------------------------------ statue
        /// <summary>Grey stone Max statue on a plinth. height = figure height incl. crown (the plinth adds 0.35 x that
        /// below it; the 1.20 m villain is height 1.45). vandalStage 0 pristine, 1 painted mustache + graffiti, 2 ghost sheet,
        /// 3 toppled (lies in front of the empty plinth). Child "Figure" (+ hidden child "Sunglasses" used by the L3 flicker
        /// scare: StoryKit.StatueSunglasses(statue)). pos = floor point. Collider-free.</summary>
        public static GameObject VillainStatue(Transform p, Vector3 pos, Quaternion? rot, float height, int vandalStage)
        {
            float k = Mathf.Max(0.2f, height) / 1.45f;
            int st = Mathf.Clamp(vandalStage, 0, 3);
            var root = Root("VillainStatue_" + st, p, pos, rot);
            var t = root.transform;
            float ph = 0.35f * k;
            Kit.BoxVisual(t, "Plinth", new Vector3(0f, ph * 0.5f, 0f), new Vector3(0.95f * k, ph, 0.8f * k), MatKey.Concrete, 0.03f * k);
            Kit.BoxVisual(t, "PlinthCap", new Vector3(0f, ph + 0.02f * k, 0f), new Vector3(1.05f * k, 0.04f * k, 0.9f * k), MatKey.Concrete, 0.01f * k);
            Text(t, st == 3 ? "UNDER NEW\nMANAGEMENT" : "OUR GLORIOUS LEADER\nHE DID IT HIMSELF", new Vector3(0f, ph * 0.5f, 0.4f * k + 0.005f),
                new Vector2(0.85f * k, ph * 0.7f), StoryKitMeshes.Hex(0x2A2D33), ShiftersFonts.Display, ph * 0.25f);
            var fig = Kit.Make("Figure", t, new Vector3(0f, ph + 0.04f * k, 0f), Quaternion.identity);
            fig.transform.localScale = Vector3.one * k;
            Part(fig.transform, "Stone", StoryKitMeshes.MaxFigure(true), StatueStone, Vector3.zero);
            var sg = Kit.Make("Sunglasses", fig.transform, Vector3.zero, Quaternion.identity);
            Part(sg.transform, "Shades", ProcMesh.CachedBox(new Vector3(0.32f, 0.08f, 0.035f), 0.01f), Kit.Mat(MatKey.Black), new Vector3(0f, 1.035f, 0.232f));
            Part(sg.transform, "Bridge", ProcMesh.CachedBox(new Vector3(0.36f, 0.018f, 0.025f), 0f), Kit.Mat(MatKey.Black), new Vector3(0f, 1.07f, 0.226f));
            sg.SetActive(false);
            if (st >= 1)
            {
                var pink = MaterialLibrary.Colored(StoryKitMeshes.Hex(0xFF5FA2), 0.5f);
                var paint = Kit.Make("Paint", fig.transform, Vector3.zero, Quaternion.identity).transform;
                for (int s = -1; s <= 1; s += 2)
                {
                    Part(paint, "Stache", Sphere(0.1f), pink, new Vector3(s * 0.13f, 0.92f, 0.23f), Quaternion.Euler(0f, 0f, s * 20f), new Vector3(1.8f, 0.45f, 0.5f), false);
                    Part(paint, "Curl", Sphere(0.05f), pink, new Vector3(s * 0.27f, 0.97f, 0.2f), null, null, false);
                }
                Text(t, "TEAMS\nRULE!", new Vector3(0f, ph * 0.5f, -0.4f * k - 0.005f), new Vector2(0.8f * k, ph * 0.7f), StoryKitMeshes.Hex(0x2EC4B6), ShiftersFonts.Display, ph * 0.3f)
                    .transform.localRotation = Quaternion.identity; // back side: readable from behind (+Z of a flipped text = -Z side)
            }
            if (st == 2)
                Part(fig.transform, "GhostSheet", StoryKitMeshes.Ghost(new Color(0.96f, 0.96f, 0.94f)), Painted, new Vector3(0f, 0.02f, 0f), null, new Vector3(1.3f, 1.02f, 1.2f));
            if (st == 3)
            {
                // lying on its back in front of the plinth, head toward the plinth
                fig.transform.localPosition = new Vector3(0.15f * k, 0.45f * k, 2.05f * k);
                fig.transform.localRotation = Quaternion.Euler(-90f, 0f, 8f);
            }
            return root;
        }

        /// <summary>The hidden "Sunglasses" child of a VillainStatue (null if none).</summary>
        public static GameObject StatueSunglasses(GameObject statue)
        {
            if (statue == null) return null;
            var fig = statue.transform.Find("Figure");
            var sg = fig != null ? fig.Find("Sunglasses") : null;
            return sg != null ? sg.gameObject : null;
        }

        // ------------------------------------------------------------------ Percy
        /// <summary>Percy the Percolator (0.8 x 1.3 x 0.6 m) with a screen face, BREW lamp and carafe. Has a PROXIMITY talk
        /// zone (PercyMachineView polls the active intern's distance; no collider): first approach shows FirstLine (default
        /// a wink), later approaches cycle Lines with a cooldown, as non-blocking StoryLines (speaker PERCY) + steam puff.
        /// pos = floor point.</summary>
        public static GameObject PercyMachine(Transform p, Vector3 pos, Quaternion? rot)
        {
            var root = Root("PercyMachine", p, pos, rot);
            var t = root.transform;
            // the canonical Percy (visuals: same model as the ME-TV PiP / cutscenes); own mesh only if it fails
            PercyPuppet pup = null;
            try { pup = PercyPuppet.Create(t, Vector3.zero, Quaternion.identity); } catch (System.Exception e) { Debug.LogWarning("[StoryKit] PercyPuppet: " + e.Message); }
            if (pup != null && pup.FaceText != null)
            {
                pup.UseUnscaledTime = false;
                var pv = root.AddComponent<PercyMachineView>();
                pv.Puppet = pup; pv.Face = null;
                return root;
            }
            if (pup != null) Kit.SafeDestroy(pup.gameObject);
            Part(t, "Body", StoryKitMeshes.PercyBody(), Painted, Vector3.zero);
            var face = Text(t, "(o_o)", new Vector3(0f, 0.98f, 0.324f), new Vector2(0.34f, 0.2f), new Color(0.49f, 1f, 0.6f), ShiftersFonts.Display, 0.12f);
            var lampOff = Glow(new Color(0.35f, 0.05f, 0.05f), 1f);
            var lamp = Part(t, "BrewLamp", Sphere(0.035f), lampOff, new Vector3(0.3f, 1.12f, 0.31f), null, null, false);
            Text(t, "BREW", new Vector3(0.3f, 1.05f, 0.312f), new Vector2(0.14f, 0.05f), StoryKitMeshes.Hex(0x2A2420), ShiftersFonts.Label, 0.04f);
            Text(t, "PERCOLATOR P-3RCY", new Vector3(0f, 0.66f, 0.315f), new Vector2(0.48f, 0.075f), StoryKitMeshes.Hex(0x2A2420), ShiftersFonts.Label, 0.06f);
            Part(t, "Carafe", Cyl(0.11f, 0.22f), Kit.Mat(MatKey.Glass), new Vector3(0f, 0.38f, 0.17f), null, null, false);
            Part(t, "Coffee", Cyl(0.1f, 0.12f), MaterialLibrary.Colored(StoryKitMeshes.Hex(0x3B2414), 0.6f), new Vector3(0f, 0.33f, 0.17f), null, null, false);
            var v = root.AddComponent<PercyMachineView>();
            v.Face = face; v.Lamp = lamp; v.LampOff = lampOff; v.LampOn = Glow(new Color(1f, 0.15f, 0.1f), 3f);
            return root;
        }

        // ------------------------------------------------------------------ script trigger
        /// <summary>Invisible box (center/size local to parent, axis-aligned to the parent) that raises
        /// GameEvents.StoryBeat(scriptOrBeatId) when an Alive intern enters (StoryPlayer plays the script with that id if it
        /// exists). Polls team positions at 10 Hz - NO collider. Configure Who (AnyIntern / ActiveIntern / WholeTeam).</summary>
        public static global::Shifters.ScriptTrigger ScriptTrigger(Transform p, Vector3 center, Vector3 size, string scriptOrBeatId, bool once = true)
        {
            var go = Kit.Make("ScriptTrigger_" + scriptOrBeatId, p, center, Quaternion.identity);
            var st = go.AddComponent<global::Shifters.ScriptTrigger>();
            st.Id = scriptOrBeatId; st.Size = size; st.Once = once;
            return st;
        }

        // ------------------------------------------------------------------ hologram taunt spot
        /// <summary>Floor emitter + a hidden 1.95 m gold hologram of Max. When the active intern comes within 5 m (or when
        /// the StoryBeat 'TriggerBeat' fires, if set on the StoryLineSpot), Max flickers in, faces the intern and says the
        /// line (lineId resolved with StoryKitLines.Resolve; literal text also works), then fades. Once per level attempt.
        /// pos = floor point. Collider-free.</summary>
        public static GameObject HologramTauntSpot(Transform p, Vector3 pos, Quaternion? rot, string lineId)
        {
            var root = Root("HologramTauntSpot", p, pos, rot);
            var t = root.transform;
            Part(t, "Emitter", Cyl(0.38f, 0.06f), Kit.Mat(MatKey.MetalDark), new Vector3(0f, 0.03f, 0f));
            Part(t, "EmitterGlow", Ring(0.34f, 0.03f), Glow(StoryKitLines.MaxColor, 2.5f), new Vector3(0f, 0.065f, 0f), null, null, false);
            var holo = Kit.Make("Hologram", t, new Vector3(0f, 0.07f, 0f), Quaternion.identity);
            Part(holo.transform, "Max", StoryKitMeshes.MaxFigure(false), HoloMaterial(HoloGold), Vector3.zero, null, Vector3.one * 1.35f, false);
            holo.SetActive(false);
            var spot = root.AddComponent<StoryLineSpot>();
            spot.LineId = lineId; spot.Text = StoryKitLines.Resolve(lineId); spot.Hologram = holo; spot.Radius = 5f;
            spot.SpeakerIsPercy = false;
            return root;
        }

        /// <summary>(additive) Invisible proximity line: when the active intern comes within 'radius' the line is shown once
        /// per attempt (Percy by default, as a radio/PiP broadcast line). No visuals, no collider.</summary>
        public static StoryLineSpot LineSpot(Transform p, Vector3 pos, float radius, string lineIdOrText, bool percy = true, bool radio = true)
        {
            var go = Kit.Make("LineSpot", p, pos, Quaternion.identity);
            var spot = go.AddComponent<StoryLineSpot>();
            spot.LineId = lineIdOrText; spot.Text = StoryKitLines.Resolve(lineIdOrText); spot.Radius = radius;
            spot.SpeakerIsPercy = percy; spot.Radio = radio;
            return spot;
        }

        // ------------------------------------------------------------------ team helpers (runtime)
        internal static ShifterCharacter Active => CharacterManager.Instance != null ? CharacterManager.Instance.Active : null;

        /// <summary>Calls visit(c) for every Alive team member.</summary>
        internal static void ForEachAlive(System.Action<ShifterCharacter> visit)
        {
            var cm = CharacterManager.Instance;
            if (cm == null || cm.Team == null) return;
            for (int i = 0; i < cm.Team.Count; i++)
            {
                var c = cm.Team[i];
                if (c != null && c.IsAlive) visit(c);
            }
        }
    }
}
