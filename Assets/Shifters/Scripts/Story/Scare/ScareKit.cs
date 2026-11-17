// =====================================================================================
//  SHIFTERS - ScareKit.cs   [owned by STORY_KIT]
//  Scripted spooky-funny scares (CAMPAIGN §10). Every cue goes through ScareDirector.TryPlay (once per attempt,
//  <= 1 per 30 s, <= 6 per chapter, never in a hazard / capture / timed window, stalkers frozen 2 s) and every scare
//  lands its PUNCHLINE within 3 s (slide whistle / sign / polite ghost). Kit conventions (positions local to 'p',
//  edit-mode safe). Collider-free except PhoneCall (a small interaction trigger on layer Ignore Raycast).
//    JumpScare      - box trigger (polled) -> a ScareKind popper springs up facing the trigger, stinger, punchline 1.1 s
//    FlickerReveal  - on StoryBeat(cueId) / Play() / proximity (ArmRadius): lights flicker, a figure is closer each flicker,
//                     then gone (or FinalReveal shown, e.g. the statue's sunglasses) + punchline
//    PhoneCall      - rings when an intern is near; E "Answer phone" -> breathing (not in MILD) -> Percy punchline
//    Blackout       - LevelLighting.SetOverride when rendering provides it, else ScareBlackoutDriver (dim lights/ambient
//                     + a dark screen veil under the HUD). MILD keeps it 30% brighter.
// =====================================================================================
using TMPro;
using UnityEngine;

namespace Shifters
{
    public static class ScareKit
    {
        // ------------------------------------------------------------------ factories
        /// <summary>Jump scare: trigger box (center/size local to p, axis-aligned) + popper (ScareKind) at popperPos,
        /// turned to face the trigger center. Fires once per attempt via ScareDirector; punchline after 1.1 s.</summary>
        public static GameObject JumpScare(Transform p, Vector3 triggerCenter, Vector3 triggerSize, Vector3 popperPos, ScareKind kind, string cueId)
        {
            var root = Kit.Make("JumpScare_" + cueId, p, Vector3.zero, Quaternion.identity);
            var face = EnvKit.ViewToFace(EnvKit.FacingViewerAt(popperPos, triggerCenter));
            var popper = Kit.Make("Popper", root.transform, popperPos, face);
            BuildPopper(popper.transform, kind, ScareDirector.Mild);
            var punch = EnvKit.AddText(popper.transform, Punchline(kind), new Vector3(0f, 2.15f, 0.3f), new Vector2(2.4f, 0.6f), Color.white, ShiftersFonts.Display, 0.26f);
            punch.outlineWidth = 0.2f; punch.outlineColor = new Color32(20, 20, 26, 255);
            punch.gameObject.SetActive(false);
            popper.SetActive(false);
            var v = root.AddComponent<JumpScareView>();
            v.CueId = cueId; v.Kind = kind; v.TriggerCenter = triggerCenter; v.TriggerSize = triggerSize;
            v.Popper = popper.transform; v.PopperRest = popperPos; v.PunchText = punch;
            return root;
        }

        /// <summary>Flicker reveal: a figure (a Mannequin popper by default) stands at positions[0] and is one step closer
        /// at each flicker (positions in order, local to p), interval clamped to 0.34..(2.6 s / steps) (<= 3 Hz, punchline
        /// < 3 s). Starts on StoryBeat(cueId), Play(), or when the active intern comes within ArmRadius of the root
        /// (set ArmRadius/ArmCenter on the view). Ends with the figure gone (or FinalReveal) + punchline sign.</summary>
        public static GameObject FlickerReveal(Transform p, Vector3[] positions, float interval, string cueId)
        {
            return FlickerReveal(p, positions, interval, cueId, null, null);
        }

        /// <summary>(additive) FlickerReveal with a custom figure (re-parented under the reveal, e.g. a VillainStatue) and
        /// an optional object enabled on the last flicker instead of the figure vanishing (e.g. StoryKit.StatueSunglasses).</summary>
        public static GameObject FlickerReveal(Transform p, Vector3[] positions, float interval, string cueId, GameObject figure, GameObject finalReveal, ScareKind kind = ScareKind.Mannequin)
        {
            if (positions == null || positions.Length == 0) positions = new[] { Vector3.zero };
            var root = Kit.Make("FlickerReveal_" + cueId, p, Vector3.zero, Quaternion.identity);
            Transform fig;
            if (figure != null) { fig = figure.transform; fig.SetParent(root.transform, false); fig.localPosition = positions[0]; }
            else
            {
                var target = positions.Length > 1 ? positions[positions.Length - 1] + (positions[positions.Length - 1] - positions[0]).normalized * 3f : positions[0] + Vector3.back;
                var go = Kit.Make("Figure", root.transform, positions[0], EnvKit.ViewToFace(EnvKit.FacingViewerAt(positions[0], target)));
                BuildPopper(go.transform, kind, ScareDirector.Mild);
                fig = go.transform;
            }
            var punch = EnvKit.AddText(root.transform, kind == ScareKind.Mannequin && figure == null ? "SMILE, YOU'RE ON CAMERA" : "...WAS THAT THERE BEFORE?",
                positions[positions.Length - 1] + new Vector3(0f, 2.4f, 0f), new Vector2(3f, 0.6f), Color.white, ShiftersFonts.Display, 0.3f);
            punch.outlineWidth = 0.2f; punch.outlineColor = new Color32(20, 20, 26, 255);
            punch.gameObject.SetActive(false);
            // polish (REQ levels-1): the punchline faces the camera (yaw only) so it never reads mirrored
            if (punch.GetComponent<PuzzleBillboard>() == null) punch.gameObject.AddComponent<PuzzleBillboard>();
            var v = root.AddComponent<FlickerRevealView>();
            v.CueId = cueId; v.Positions = positions; v.Interval = interval; v.Figure = fig; v.FinalReveal = finalReveal; v.PunchText = punch;
            if (finalReveal != null) finalReveal.SetActive(false);
            return root;
        }

        /// <summary>Ringing desk phone on a small side table (pos = floor point, the phone faces -Z / rotate the root).
        /// Rings when the active intern is within 10 m (or on StoryBeat(cueId + ".ring")); E "Answer phone" (IInteractable,
        /// small trigger collider) -> heavy breathing (skipped in MILD) -> Percy: "Sorry. Coffee machines don't have lungs.
        /// I'm just steaming." within 3 s.</summary>
        public static GameObject PhoneCall(Transform p, Vector3 pos, string cueId)
        {
            var root = Kit.Make("PhoneCall_" + cueId, p, pos, Quaternion.identity);
            var t = root.transform;
            Kit.BoxVisual(t, "Table", new Vector3(0f, 0.4f, 0f), new Vector3(0.7f, 0.8f, 0.55f), MatKey.Wood, 0.02f);
            Kit.BoxVisual(t, "Base", new Vector3(0f, 0.86f, 0f), new Vector3(0.3f, 0.1f, 0.24f), MatKey.Black, 0.02f);
            var handset = Kit.Make("Handset", t, new Vector3(0f, 0.94f, 0f), Quaternion.identity);
            Kit.BoxVisual(handset.transform, "Grip", Vector3.zero, new Vector3(0.34f, 0.05f, 0.08f), MatKey.Black, 0.02f);
            Kit.BoxVisual(handset.transform, "EarL", new Vector3(-0.15f, 0.02f, 0f), new Vector3(0.08f, 0.06f, 0.1f), MatKey.Black, 0.02f);
            Kit.BoxVisual(handset.transform, "EarR", new Vector3(0.15f, 0.02f, 0f), new Vector3(0.08f, 0.06f, 0.1f), MatKey.Black, 0.02f);
            var lamp = Kit.BoxVisual(t, "Lamp", new Vector3(0.1f, 0.915f, -0.12f), new Vector3(0.04f, 0.02f, 0.02f), MatKey.LightRed, 0f, null, false);
            var col = Kit.Trigger(t, "AnswerZone", new Vector3(0f, 0.9f, 0f), new Vector3(1.2f, 1.2f, 1.2f));
            var v = root.AddComponent<PhoneCallView>();
            v.CueId = cueId; v.Handset = handset.transform; v.Lamp = lamp; v.Zone = col;
            return root;
        }

        /// <summary>Lights out for 'seconds' (<= 1.2 s per flicker in scripted sequences). keepAccents = point/spot gameplay
        /// lights and emissive accents stay (only the sun + ambient go dark). MILD keeps it 30% brighter.</summary>
        public static void Blackout(float seconds, bool keepAccents = true)
        {
            if (!Application.isPlaying || seconds <= 0f) return;
            bool mild = ScareDirector.Mild;
            float floor = mild ? 0.34f : 0.04f;
            // rendering's override dims sun + ambient (not the sky); the veil sells the blackout on bright-sky levels.
            // polish: remember the level's own override first, so the blackout ENDS (it used to stay on for the rest of
            // the chapter: CH07's Reserve went black after the flicker scare).
            if (!ScareBlackoutDriver.Active) ScareBlackoutDriver.SaveLightingOverride();
            float ambFloor = mild ? 0.4f : 0.08f;
            bool ov = StoryKitLate.TrySetLightingOverride(floor, ambFloor, seconds);
            if (ov) ScareBlackoutDriver.NoteBlackoutOverride(floor, ambFloor);
            LastBlackoutUsedOverride = ov;
            ScareBlackoutDriver.Begin(seconds, keepAccents, ov ? 1f : floor, ov ? (mild ? 0.38f : 0.7f) : (mild ? 0.5f : 0.86f));
        }

        /// <summary>true when the last Blackout went through LevelLighting.SetOverride (rendering), false = local fallback.</summary>
        public static bool LastBlackoutUsedOverride { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { LastBlackoutUsedOverride = false; }

        // ------------------------------------------------------------------ poppers
        public static string Punchline(ScareKind kind)
        {
            switch (kind)
            {
                case ScareKind.CardboardMax: return "IT'S CARDBOARD.\nHE COULDN'T AFFORD A REAL ME.";
                case ScareKind.GhostSheet: return "BOO. ...SORRY.\nHAVE YOU SEEN MY TEAM?";
                case ScareKind.Dummy: return "TEAM-BUILDING DUMMY\nWANTS A HUG.";
                case ScareKind.Mannequin: return "SMILE!\n(MANDATORY)";
                case ScareKind.FilingCabinet: return "FILED UNDER:\nB FOR BOO";
                case ScareKind.MirrorFace: return "OBJECTS IN MIRROR ARE\nFRIENDLIER THAN THEY APPEAR";
            }
            return "BOO?";
        }

        static Mesh Sph(float r) => ProcMesh.Cached(EnvMeshes.Key("scare_sph", r), () => ProcMesh.Sphere(r, 12, 8));
        static Mesh Cap(float r, float h) => ProcMesh.Cached(EnvMeshes.Key("scare_cap", r, h), () => ProcMesh.Capsule(r, h, 12, 4));

        /// <summary>Builds the popper visuals in face space (front +Z, feet at y=0, ~1.5-1.8 m). mild = smiley stickers instead
        /// of painted smiles.</summary>
        public static void BuildPopper(Transform t, ScareKind kind, bool mild)
        {
            GameObject vis = null;
            try { vis = StalkerSkins.BuildScareProp(kind, t, Vector3.zero, Quaternion.identity, mild); } catch (System.Exception e) { Debug.LogWarning("[ScareKit] scare prop " + kind + ": " + e.Message); }
            if (vis != null) return;   // visuals' models (same as the stalkers'); FilingCabinet / MirrorFace are ours
            var painted = StoryKit.Painted;
            switch (kind)
            {
                case ScareKind.CardboardMax:
                {
                    float k = 1.25f;
                    ProcMesh.Part("Cutout", t, StoryKitMeshes.MaxFigure(false), painted, Vector3.zero, null, new Vector3(k, k, k * 0.06f));
                    Kit.BoxVisual(t, "Brace", new Vector3(0f, 0.5f, -0.3f), new Vector3(0.06f, 1f, 0.5f), MatKey.Cardboard, 0.005f, Quaternion.Euler(28f, 0f, 0f));
                    break;
                }
                case ScareKind.GhostSheet:
                    ProcMesh.Part("Sheet", t, StoryKitMeshes.Ghost(new Color(0.96f, 0.96f, 0.95f)), painted, Vector3.zero, null, Vector3.one * 1.15f);
                    break;
                case ScareKind.Dummy:
                case ScareKind.Mannequin:
                {
                    bool dummy = kind == ScareKind.Dummy;
                    var body = MaterialLibrary.Colored(dummy ? StoryKitMeshes.Hex(0xD9C7A0) : StoryKitMeshes.Hex(0xE8E6E1), 0.35f);
                    float r = dummy ? 0.27f : 0.2f;
                    ProcMesh.Part("Body", t, Cap(r, 1.2f), body, new Vector3(0f, 0.7f, 0f));
                    ProcMesh.Part("Head", t, Sph(0.21f), body, new Vector3(0f, 1.52f, 0f));
                    for (int s = -1; s <= 1; s += 2)
                        ProcMesh.Part("Arm", t, Cap(0.07f, 0.7f), body, new Vector3(s * (r + 0.1f), 0.95f, 0.05f), Quaternion.Euler(0f, 0f, s * 10f));
                    if (dummy) EnvKit.AddText(t, "TEAM\nBUILDING", new Vector3(0f, 0.95f, r + 0.01f), new Vector2(0.4f, 0.22f), StoryKitMeshes.Hex(0x8B5A2B), ShiftersFonts.Display, 0.08f);
                    else Kit.BoxVisual(t, "Tie", new Vector3(0f, 1.0f, r - 0.01f), new Vector3(0.07f, 0.34f, 0.03f), MatKey.WarningRed, 0.005f);
                    FaceOn(t, new Vector3(0f, 1.53f, 0.2f), mild);
                    break;
                }
                case ScareKind.FilingCabinet:
                {
                    Kit.BoxVisual(t, "Cabinet", new Vector3(0f, 0.7f, 0f), new Vector3(0.6f, 1.4f, 0.7f), MatKey.MetalLight, 0.02f);
                    for (int i = 0; i < 4; i++)
                        Kit.BoxVisual(t, "Drawer" + i, new Vector3(0f, 0.2f + i * 0.34f, 0.35f + (i == 3 ? 0.18f : 0f)), new Vector3(0.54f, 0.3f, 0.04f), MatKey.MetalDark, 0.01f);
                    var eye = MaterialLibrary.Unlit(Color.white, 2.5f);
                    for (int s = -1; s <= 1; s += 2) ProcMesh.Part("Eye", t, Sph(0.045f), eye, new Vector3(s * 0.1f, 1.38f, 0.46f), null, null, false);
                    break;
                }
                case ScareKind.MirrorFace:
                {
                    Kit.BoxVisual(t, "Frame", new Vector3(0f, 1.0f, 0f), new Vector3(0.95f, 1.7f, 0.08f), MatKey.Wood, 0.03f);
                    ProcMesh.Part("Glass", t, ProcMesh.CachedBox(new Vector3(0.8f, 1.5f, 0.01f), 0f), MaterialLibrary.Colored(StoryKitMeshes.Hex(0x9FB4C8), 0.95f), new Vector3(0f, 1.0f, 0.045f), null, null, false);
                    EnvKit.AddText(t, mild ? "(^_^)" : "(O_O)", new Vector3(0f, 1.15f, 0.055f), new Vector2(0.7f, 0.4f), StoryKitMeshes.Hex(0x1A1C22), ShiftersFonts.Display, 0.3f);
                    break;
                }
            }
        }

        static void FaceOn(Transform t, Vector3 at, bool mild)
        {
            if (mild)
            {
                ProcMesh.Part("Sticker", t, ProcMesh.Cached("scare_sticker", () => ProcMesh.Cylinder(0.13f, 0.01f, 20, true)), MaterialLibrary.Colored(StoryKitMeshes.Hex(0xFFD23C), 0.4f), at, Quaternion.Euler(90f, 0f, 0f), null, false);
                EnvKit.AddText(t, "^_^", at + new Vector3(0f, 0f, 0.012f), new Vector2(0.22f, 0.14f), StoryKitMeshes.Hex(0x1A1C22), ShiftersFonts.Display, 0.1f);
            }
            else
            {
                var ink = Kit.Mat(MatKey.Black);
                for (int s = -1; s <= 1; s += 2) ProcMesh.Part("Eye", t, Sph(0.025f), ink, at + new Vector3(s * 0.07f, 0.05f, 0f), null, null, false);
                Kit.BoxVisual(t, "PaintedSmile", at + new Vector3(0f, -0.06f, 0f), new Vector3(0.18f, 0.025f, 0.02f), MatKey.WarningRed, 0.005f, null, false);
                for (int s = -1; s <= 1; s += 2) Kit.BoxVisual(t, "SmileEnd", at + new Vector3(s * 0.1f, -0.04f, 0f), new Vector3(0.04f, 0.025f, 0.02f), MatKey.WarningRed, 0.005f, Quaternion.Euler(0f, 0f, s * 35f), false);
            }
        }

        // ------------------------------------------------------------------ shared runtime helper
        internal static bool AnyAliveInBox(Transform space, Vector3 center, Vector3 size)
        {
            var cm = CharacterManager.Instance;
            if (cm == null || cm.Team == null) return false;
            var h = size * 0.5f;
            for (int i = 0; i < cm.Team.Count; i++)
            {
                var c = cm.Team[i];
                if (c == null || !c.IsAlive) continue;
                var l = (space != null ? space.InverseTransformPoint(c.transform.position) : c.transform.position) - center;
                if (Mathf.Abs(l.x) <= h.x && Mathf.Abs(l.y) <= h.y + 0.5f && Mathf.Abs(l.z) <= h.z) return true;
            }
            return false;
        }
    }
}
