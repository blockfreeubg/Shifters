// =====================================================================================
//  SHIFTERS - FoundersStatueView.cs   [owned by STORY_KIT]   (StoryKit.FoundersStatue runtime)
//  Start (play mode only): builds gold CharacterPuppets of GameSession.Team (CharacterPuppet.SetGold when visuals has it,
//  else every renderer gets the Gold material), poses them (Celebrate / FistPump / Point), then freezes the pose after
//  FreezeAfter seconds by disabling the puppet behaviours. Placeholders stay when there is no team.
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public sealed class FoundersStatueView : MonoBehaviour
    {
        public Transform[] Slots;
        public TMPro.TextMeshPro Names;
        public float FreezeAfter = 0.8f;
        public bool Built { get; private set; }
        CharacterPuppet[] _puppets;
        float _freezeAt = -1f;

        void Start() { if (Application.isPlaying) Rebuild(); }

        /// <summary>(Re)build the gold puppets from the current team. Returns the number built.</summary>
        public int Rebuild()
        {
            var team = GameSession.Instance != null ? GameSession.Instance.Team : null;
            if (team == null || team.Members == null || Slots == null) return 0;
            if (_puppets != null) foreach (var old in _puppets) if (old != null) Destroy(old.gameObject);
            _puppets = new CharacterPuppet[Slots.Length];
            VisualAction[] poses = { VisualAction.Celebrate, VisualAction.FistPump, VisualAction.Point };
            var names = new System.Text.StringBuilder();
            int n = 0;
            for (int i = 0; i < Slots.Length && i < team.Members.Length; i++)
            {
                var prof = team.Members[i];
                if (prof == null || Slots[i] == null) continue;
                var ph = Slots[i].Find("Placeholder");
                CharacterPuppet pup = null;
                try { pup = CharacterPuppet.Create(prof, Slots[i], Vector3.zero, Quaternion.identity); }
                catch (System.Exception e) { Debug.LogWarning("[FoundersStatue] puppet: " + e.Message); }
                if (pup == null) continue;
                if (ph != null) ph.gameObject.SetActive(false);
                if (!StoryKitLate.TrySetGold(pup, true))
                {
                    var gold = Kit.Mat(MatKey.Gold);
                    foreach (var r in pup.GetComponentsInChildren<Renderer>(true))
                    {
                        if (r is ParticleSystemRenderer) continue;
                        var mats = r.sharedMaterials;
                        for (int m = 0; m < mats.Length; m++) mats[m] = gold;
                        r.sharedMaterials = mats;
                    }
                }
                pup.SetExpression(Expression.Happy);
                pup.Play(poses[i % poses.Length]);
                _puppets[i] = pup;
                if (names.Length > 0) names.Append("  -  ");
                names.Append((prof.Name ?? "INTERN").ToUpperInvariant());
                n++;
            }
            if (Names != null && names.Length > 0) Names.text = names.ToString();
            Built = n > 0;
            _freezeAt = Time.time + FreezeAfter;
            return n;
        }

        void Update()
        {
            if (_freezeAt < 0f || Time.time < _freezeAt) return;
            _freezeAt = -1f;
            if (_puppets == null) return;
            foreach (var p in _puppets)
            {
                if (p == null) continue;
                foreach (var b in p.GetComponentsInChildren<MonoBehaviour>(true)) if (b != null) b.enabled = false;
            }
        }
    }
}
