// =====================================================================================
//  SHIFTERS - BossTestMonitor.cs   [BOSSES module - tests]
//  Samples a boss fight every fixed step: B3 (never two attacks active), B12 (<= 10 projectiles),
//  B4 (inactive interns never lose health; pushes <= 2 m/s are the runner's job), team states
//  (Downed / Captured / Lost counts), min health. Attach to any GameObject; set Boss.
// =====================================================================================
using System.Collections.Generic;
using UnityEngine;

namespace Shifters
{
    public sealed class BossTestMonitor : MonoBehaviour
    {
        public BossBase Boss;
        public int MaxActive, MaxProjectiles, InactiveDamaged, Downed, Captured, Lost, Telegraphs, ShortTelegraphs;
        public float MinHealth = 9999f, MinWindup = 9999f;
        public string LastInactiveDamage = "";
        public readonly HashSet<BossAttackKind> KindsSeen = new HashSet<BossAttackKind>();
        readonly Dictionary<ShifterCharacter, float> _hp = new Dictionary<ShifterCharacter, float>();
        readonly Dictionary<ShifterCharacter, CharacterState> _st = new Dictionary<ShifterCharacter, CharacterState>();
        BossBase _hooked;

        public void ResetCounts()
        {
            MaxActive = MaxProjectiles = InactiveDamaged = Downed = Captured = Lost = Telegraphs = ShortTelegraphs = 0;
            MinHealth = MinWindup = 9999f; LastInactiveDamage = ""; KindsSeen.Clear(); _hp.Clear(); _st.Clear();
        }

        void Hook()
        {
            if (_hooked == Boss) return;
            if (_hooked != null) _hooked.Attacks.Started -= OnStarted;
            _hooked = Boss;
            if (_hooked != null) _hooked.Attacks.Started += OnStarted;
        }

        void OnDestroy() { if (_hooked != null) _hooked.Attacks.Started -= OnStarted; }

        void OnStarted(BossTelegraph t)
        {
            Telegraphs++;
            KindsSeen.Add(t.Kind);
            MinWindup = Mathf.Min(MinWindup, t.WindupSeconds);
            float min = BossAttackRunner.MinTelegraph(t.Phase) * (Boss != null ? Boss.TellScale : 1f);
            if (t.WindupSeconds < min - 1e-3f) ShortTelegraphs++;
        }

        void FixedUpdate()
        {
            Hook();
            if (Boss != null)
            {
                MaxActive = Mathf.Max(MaxActive, Boss.Attacks.Active.Count);
                MaxProjectiles = Mathf.Max(MaxProjectiles, Boss.Attacks.LiveProjectiles);
            }
            var all = ShifterCharacter.All;
            for (int i = 0; i < all.Count; i++)
            {
                var c = all[i];
                if (c == null) continue;
                if (_hp.TryGetValue(c, out float prev) && c.Health < prev - 0.01f && !c.IsActive)
                {
                    InactiveDamaged++;
                    LastInactiveDamage = c.DisplayName + " " + prev.ToString("0") + "->" + c.Health.ToString("0");
                }
                _hp[c] = c.Health;
                MinHealth = Mathf.Min(MinHealth, c.Health);
                _st.TryGetValue(c, out var ps);
                if (c.State != ps)
                {
                    if (c.State == CharacterState.Downed) Downed++;
                    else if (c.State == CharacterState.Captured) Captured++;
                    else if (c.State == CharacterState.Lost) Lost++;
                    _st[c] = c.State;
                }
            }
        }
    }
}
