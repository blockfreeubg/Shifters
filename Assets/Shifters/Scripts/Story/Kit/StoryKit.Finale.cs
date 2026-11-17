// =====================================================================================
//  SHIFTERS - StoryKit.Finale.cs   [owned by STORY_KIT]
//  Hearing / finale props (CH05, CH08, CH11, CH14, C15): ExhibitProp, TallyBoard, LetterBlock (the ONLY StoryKit piece
//  with a collider), FoundersStatue, CitizenCrowd. Facing / pos conventions: see StoryKit.cs header.
// =====================================================================================
using TMPro;
using UnityEngine;

namespace Shifters
{
    public static partial class StoryKit
    {
        /// <summary>Evidence on a small display stand (visual only, no rot: front faces -Z; rotate the returned root).
        /// exhibitId "A" THE GREY MEMO, "B" THE BOOKS, "C" THE BEDSHEET (anything else: a sealed evidence box).
        /// pos = floor point; stand 0.9 m.</summary>
        public static GameObject ExhibitProp(Transform p, Vector3 pos, string exhibitId)
        {
            string id = (exhibitId ?? "").Trim().ToUpperInvariant();
            if (id.StartsWith("EXHIBIT")) id = id.Substring(7).Trim();
            var root = Root("Exhibit_" + id, p, pos, null);
            var t = root.transform;
            Kit.BoxVisual(t, "Stand", new Vector3(0f, 0.45f, 0f), new Vector3(0.6f, 0.9f, 0.6f), MatKey.Wood, 0.03f);
            Kit.BoxVisual(t, "Top", new Vector3(0f, 0.915f, 0f), new Vector3(0.7f, 0.03f, 0.7f), MatKey.MetalDark, 0.01f);
            string title;
            switch (id)
            {
                case "A":
                    title = "EXHIBIT A\nTHE GREY MEMO";
                    Kit.BoxVisual(t, "Memo", new Vector3(0f, 1.1f, 0f), new Vector3(0.36f, 0.46f, 0.01f), MatKey.Poster, 0f, Quaternion.Euler(-12f, 0f, 0f), false);
                    Text(t, "MEMO\n\nDRAIN THE COLOR.\nNOBODY WILL NOTICE.\n\n- ME", new Vector3(0f, 1.1f, 0.012f), new Vector2(0.3f, 0.4f), StoryKitMeshes.Hex(0x30343A), ShiftersFonts.Label, 0.045f)
                        .transform.parent.Find("Memo").SetAsLastSibling();
                    Kit.BoxVisual(t, "MemoStand", new Vector3(0f, 0.95f, -0.08f), new Vector3(0.04f, 0.1f, 0.04f), MatKey.MetalLight, 0.005f);
                    break;
                case "B":
                    title = "EXHIBIT B\nTHE BOOKS";
                    for (int i = 0; i < 3; i++)
                        Kit.BoxVisual(t, "Book" + i, new Vector3((i - 1) * 0.02f, 0.97f + i * 0.09f, 0f), new Vector3(0.42f, 0.085f, 0.3f), i == 1 ? MatKey.Velvet : MatKey.Gold, 0.01f, Quaternion.Euler(0f, (i - 1) * 9f, 0f));
                    Text(t, "THE BOOKS\n(COOKED)", new Vector3(0f, 1.06f, 0.155f), new Vector2(0.36f, 0.07f), Color.white, ShiftersFonts.Display, 0.035f);
                    break;
                case "C":
                    title = "EXHIBIT C\nTHE BEDSHEET";
                    Part(t, "Sheet", StoryKitMeshes.Ghost(new Color(0.95f, 0.95f, 0.93f)), Painted, new Vector3(0f, 0.93f, 0f), null, new Vector3(0.45f, 0.22f, 0.45f));
                    break;
                default:
                    title = "EXHIBIT " + id;
                    Kit.BoxVisual(t, "EvidenceBox", new Vector3(0f, 1.08f, 0f), new Vector3(0.4f, 0.3f, 0.3f), MatKey.Cardboard, 0.01f);
                    break;
            }
            Part(t, "Cloche", Cyl(0.3f, 0.55f), Kit.Mat(MatKey.Glass), new Vector3(0f, 1.21f, 0f), null, null, false);
            Kit.BoxVisual(t, "Tag", new Vector3(0f, 0.6f, 0.305f), new Vector3(0.48f, 0.22f, 0.01f), MatKey.HazardYellow, 0.005f, null, false);
            Text(t, title, new Vector3(0f, 0.6f, 0.312f), new Vector2(0.44f, 0.19f), StoryKitMeshes.Hex(0x15161A), ShiftersFonts.Display, 0.07f);
            return root;
        }

        /// <summary>Hearing vote board (4 x 2.5 m on two legs, board center at 2.6 m): "AYE n" / "NAY n" + caption. Listens
        /// to StoryBeat "ch14.*" (TallyBoardView: ch14.vote -> NAY 1, ch14.impeached -> AYE 3 NAY 1 MOTION CARRIES,
        /// ch14.tally.aye / .nay / .reset, ch14.tally.set.A.N). pos = floor point. Collider-free.</summary>
        public static GameObject TallyBoard(Transform p, Vector3 pos, Quaternion? rot)
        {
            var root = Root("TallyBoard", p, pos, rot);
            var t = root.transform;
            const float w = 4f, h = 2.5f, cy = 2.6f;
            for (int s = -1; s <= 1; s += 2) Kit.BoxVisual(t, "Leg", new Vector3(s * 1.6f, (cy - h * 0.5f) * 0.5f, -0.05f), new Vector3(0.14f, cy - h * 0.5f, 0.14f), MatKey.MetalDark, 0.02f);
            Kit.BoxVisual(t, "Board", new Vector3(0f, cy, -0.06f), new Vector3(w + 0.2f, h + 0.2f, 0.12f), MatKey.Wood, 0.03f);
            Part(t, "Face", ProcMesh.CachedBox(new Vector3(w, h, 0.02f), 0f), Glow(StoryKitMeshes.Hex(0x10151C), 1f), new Vector3(0f, cy, 0.01f), null, null, false);
            Part(t, "Divider", ProcMesh.CachedBox(new Vector3(0.04f, h * 0.62f, 0.01f), 0f), Glow(Color.white, 1.2f), new Vector3(0f, cy + 0.2f, 0.024f), null, null, false);
            Text(t, "THE PEOPLE (AND THREE INTERNS) v. MAXIMILIAN ME", new Vector3(0f, cy + h * 0.42f, 0.03f), new Vector2(w * 0.94f, 0.22f), StoryKitLines.MaxColor, ShiftersFonts.Label, 0.16f);
            // reader's left = root +X (front is +Z, reader looks -Z)
            var green = StoryKitMeshes.Hex(0x3DDC97); var red = StoryKitMeshes.Hex(0xE8394B);
            Text(t, "AYE", new Vector3(w * 0.25f, cy + 0.62f, 0.03f), new Vector2(w * 0.4f, 0.32f), green, ShiftersFonts.Display, 0.3f);
            Text(t, "NAY", new Vector3(-w * 0.25f, cy + 0.62f, 0.03f), new Vector2(w * 0.4f, 0.32f), red, ShiftersFonts.Display, 0.3f);
            var aye = Text(t, "0", new Vector3(w * 0.25f, cy + 0.05f, 0.03f), new Vector2(w * 0.4f, 0.8f), green, ShiftersFonts.Numbers, 0.8f);
            var nay = Text(t, "0", new Vector3(-w * 0.25f, cy + 0.05f, 0.03f), new Vector2(w * 0.4f, 0.8f), red, ShiftersFonts.Numbers, 0.8f);
            var cap = Text(t, "THE HEARING IS IN SESSION", new Vector3(0f, cy - h * 0.38f, 0.03f), new Vector2(w * 0.94f, 0.3f), Color.white, ShiftersFonts.Display, 0.2f);
            var v = root.AddComponent<TallyBoardView>();
            v.AyeText = aye; v.NayText = nay; v.Caption = cap;
            return root;
        }

        /// <summary>Toy letter block (size = box extents) with 'word' on its front and back faces and the first letter on
        /// the sides. HAS a static (non-trigger) BoxCollider on the root (the CH14 filibuster pile; add a Rigidbody yourself
        /// for falling blocks). pos = block center.</summary>
        public static GameObject LetterBlock(Transform p, Vector3 pos, Quaternion? rot, string word, Vector3 size)
        {
            word = (word ?? "").ToUpperInvariant();
            size = new Vector3(Mathf.Max(0.2f, size.x), Mathf.Max(0.2f, size.y), Mathf.Max(0.2f, size.z));
            var root = Root("LetterBlock_" + word, p, pos, rot);
            var t = root.transform;
            Color[] cols = { StoryKitMeshes.Hex(0xE8394B), StoryKitMeshes.Hex(0x3C7DD9), StoryKitMeshes.Hex(0xFFC93C), StoryKitMeshes.Hex(0x3DDC97), StoryKitMeshes.Hex(0xB07CFF), StoryKitMeshes.Hex(0xFF8A1F) };
            int hsh = 0; foreach (char ch in word) hsh = hsh * 31 + ch;
            var col = cols[Mathf.Abs(hsh) % cols.Length];
            Part(t, "Block", StoryKitMeshes.Block(size), MaterialLibrary.Colored(col, 0.35f), Vector3.zero);
            var white = Color.white;
            float fs = Mathf.Min(size.x / Mathf.Max(1, word.Length) * 1.3f, size.y * 0.7f);
            Text(t, word, new Vector3(0f, 0f, size.z * 0.5f + 0.006f), new Vector2(size.x * 0.9f, size.y * 0.8f), white, ShiftersFonts.Display, fs);
            Text(t, word, new Vector3(0f, 0f, -size.z * 0.5f - 0.006f), new Vector2(size.x * 0.9f, size.y * 0.8f), white, ShiftersFonts.Display, fs).transform.localRotation = Quaternion.identity;
            var col3 = root.AddComponent<BoxCollider>();
            col3.size = size;
            return root;
        }

        /// <summary>Founders' statue: a marble plinth (3.6 x 1 m) with plaque "THE FOUNDERS OF THE SHIFTOCRACY" + gold
        /// placeholder figures; at runtime FoundersStatueView replaces them with gold CharacterPuppets of the CURRENT team
        /// (GameSession.Team) striking a pose, then freezes them. pos = floor point. Collider-free.</summary>
        public static GameObject FoundersStatue(Transform p, Vector3 pos, Quaternion? rot)
        {
            var root = Root("FoundersStatue", p, pos, rot);
            var t = root.transform;
            Kit.BoxVisual(t, "Plinth", new Vector3(0f, 0.5f, 0f), new Vector3(3.6f, 1f, 1.4f), MatKey.Marble, 0.04f);
            Kit.BoxVisual(t, "Cap", new Vector3(0f, 1.03f, 0f), new Vector3(3.8f, 0.06f, 1.55f), MatKey.Gold, 0.01f);
            Kit.BoxVisual(t, "Plaque", new Vector3(0f, 0.52f, 0.705f), new Vector3(2.6f, 0.5f, 0.02f), MatKey.Gold, 0.005f, null, false);
            Text(t, "THE FOUNDERS OF THE SHIFTOCRACY", new Vector3(0f, 0.6f, 0.718f), new Vector2(2.4f, 0.2f), StoryKitMeshes.Hex(0x3A2A08), ShiftersFonts.Display, 0.16f);
            var names = Text(t, "THREE INTERNS. ONE TEAM.", new Vector3(0f, 0.41f, 0.718f), new Vector2(2.4f, 0.16f), StoryKitMeshes.Hex(0x3A2A08), ShiftersFonts.Label, 0.12f);
            var slots = new Transform[3];
            for (int i = 0; i < 3; i++)
            {
                var s = Kit.Make("Founder_" + i, t, new Vector3((1 - i) * 1.15f, 1.06f, 0f), Quaternion.identity);
                slots[i] = s.transform;
                var ph = Kit.Make("Placeholder", s.transform, Vector3.zero, Quaternion.identity).transform;
                Part(ph, "Body", ProcMesh.Cached("story_founder_ph", () => ProcMesh.Capsule(0.28f, 1.3f, 12, 4)), Kit.Mat(MatKey.Gold), new Vector3(0f, 0.65f, 0f));
                Part(ph, "Head", Sphere(0.24f), Kit.Mat(MatKey.Gold), new Vector3(0f, 1.5f, 0f));
            }
            var v = root.AddComponent<FoundersStatueView>();
            v.Slots = slots; v.Names = names;
            return root;
        }

        /// <summary>Crowd of 'count' citizens (max 400) as ONE merged mesh (one renderer, no colliders) scattered in 'area'
        /// (local to parent; area.min.y = floor). They face the parent's -Z (a stage on the -Z side) with jitter.</summary>
        public static GameObject CitizenCrowd(Transform p, Bounds area, int count, int seed)
        {
            var go = Kit.Make("CitizenCrowd", p, Vector3.zero, Quaternion.identity);
            ProcMesh.AddRenderer(go, StoryKitMeshes.Crowd(area, count, seed), Painted, false);
            return go;
        }
    }
}
