// =====================================================================================
//  SHIFTERS - GhostTrio.cs   (VISUALS module, campaign wave)
//  GHOSTS OF TEAMS PAST always float in trios (default tags MARKETING, IT, BOWLING LEAGUE).
//  StoryKit.GhostTrio can call GhostTrio.Create. Boo/Bow/Wave act on all three (staggered).
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public class GhostTrio : MonoBehaviour
    {
        public static readonly string[] DefaultTags = { "MARKETING", "IT", "BOWLING LEAGUE" };
        public GhostSheet[] Ghosts { get; private set; } = new GhostSheet[0];

        public static GhostTrio Create(Transform parent, Vector3 localPosition, Quaternion? localRotation = null, string[] tags = null)
        {
            var go = new GameObject("GhostTrio");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = localRotation ?? Quaternion.identity;
            var trio = go.AddComponent<GhostTrio>();
            if (tags == null || tags.Length == 0) tags = DefaultTags;
            trio.Ghosts = new GhostSheet[3];
            var offs = new[] { new Vector3(-0.85f, 0f, -0.25f), new Vector3(0f, 0.15f, 0.2f), new Vector3(0.85f, 0f, -0.25f) };
            for (int i = 0; i < 3; i++)
            {
                var g = GhostSheet.Create(go.transform, offs[i], Quaternion.Euler(0f, (1 - i) * 12f, 0f), tags[i % tags.Length]);
                g.Phase = i * 1.3f;
                trio.Ghosts[i] = g;
            }
            return trio;
        }

        public void Boo() { for (int i = 0; i < Ghosts.Length; i++) if (Ghosts[i] != null) Ghosts[i].Invoke(nameof(GhostSheet.Boo), i * 0.12f); }
        public void Bow() { for (int i = 0; i < Ghosts.Length; i++) if (Ghosts[i] != null) Ghosts[i].Invoke(nameof(GhostSheet.Bow), i * 0.15f); }
        public void Wave() { for (int i = 0; i < Ghosts.Length; i++) if (Ghosts[i] != null) Ghosts[i].Invoke(nameof(GhostSheet.Wave), i * 0.2f); }
        public bool UseUnscaledTime { set { for (int i = 0; i < Ghosts.Length; i++) if (Ghosts[i] != null) Ghosts[i].UseUnscaledTime = value; } }
    }
}
