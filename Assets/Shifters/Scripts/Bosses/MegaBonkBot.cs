// =====================================================================================
//  SHIFTERS - MegaBonkBot.cs   [BOSSES module - the framework test boss / brain template]
//  "MEGA BONK-BOT 9000" (BossKit.TestBoss). ME-VISION (ActiveOnly). Three phases:
//    P0 BONK      SLAM at you, SHOCKWAVE ring  -> overheats 6 s after the shockwave -> GUN shoots the heart x2
//    P1 CHARGE    CHARGE at you, SLAM          -> dizzy 7 s after the charge          -> STR punches the belly x1
//    P2 OVERCLOCK VOLLEY at you, kazoo SWEEP   -> reboots 8 s after the sweep          -> IQ hacks the port x1
//  A real brain is this short: fill Phases, open the window in OnAttackEnd, flavour in OnPhaseBegin.
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public sealed class MegaBonkBot : BossBase
    {
        public BossWeakPoint Heart, PunchPlate, HackPort;

        static readonly string[] Quips = { "BONK PROTOCOL ENGAGED.", "TEAMWORK IS INEFFICIENT.", "I WORK ALONE. LIKE MY CREATOR.", "SHUTTING DOWN... JUST KIDDING." };

        /// <summary>Fills identity + phases (called by BossKit.TestBoss; safe to call again).</summary>
        public void Configure()
        {
            BossId = "megabonk"; BossName = "MEGA BONK-BOT 9000"; BossTitle = "Prototype Middle Manager"; BarLabel = "BONKS 0/4";
            Rule = BossTargetRule.ActiveOnly;
            Phases = new[]
            {
                new BossPhaseSpec { Name = "PHASE 1: BONK", HitsRequired = 2, Accepts = BossHitKind.Shoot, HitsOnlyWhenVulnerable = true, VulnerableSeconds = 6f, CheckpointIndex = 1,
                    Pattern = new[] {
                        BossAttackSpec.Make(BossAttackKind.SlamZone, 1.6f, true, null, 3f),
                        BossAttackSpec.Make(BossAttackKind.Shockwave, 1.5f, false, null, 0.6f, 9f, 8f) } },
                new BossPhaseSpec { Name = "PHASE 2: CHARGE", HitsRequired = 1, Accepts = BossHitKind.Punch, HitsOnlyWhenVulnerable = true, VulnerableSeconds = 7f, CheckpointIndex = 2,
                    Pattern = new[] {
                        BossAttackSpec.Make(BossAttackKind.Charge, 1.4f, true, null, 1.4f, 12f, 13f, 0f, 1.6f),
                        BossAttackSpec.Make(BossAttackKind.SlamZone, 1.3f, true, null, 3f) } },
                new BossPhaseSpec { Name = "PHASE 3: OVERCLOCK", HitsRequired = 1, Accepts = BossHitKind.Hack, HitsOnlyWhenVulnerable = true, VulnerableSeconds = 8f, CheckpointIndex = 3,
                    Pattern = new[] {
                        BossAttackSpec.Make(BossAttackKind.Volley, 1.3f, true, null, 0.35f, 20f, 12f, 1.4f),
                        BossAttackSpec.Make(BossAttackKind.Sweep, 1.4f, false, null, 0.6f, 7f, 120f) } },
            };
        }

        protected override void Awake()
        {
            if (Phases == null || Phases.Length == 0) Configure();
            base.Awake();
        }

        int TotalHits() { int n = 0; for (int p = 0; p < Phase && p < PhaseCount; p++) n += Mathf.Max(1, Phases[p].HitsRequired); return n + Hits; }

        protected override void OnPhaseBegin(int phase)
        {
            Say(Quips[Mathf.Clamp(phase, 0, Quips.Length - 1)]);
            UpdateBar();
        }

        protected override void OnAttackEnd(BossTelegraph t)
        {
            // the window comes after the phase's "big" move: shockwave / charge / sweep
            if ((Phase == 0 && t.Kind == BossAttackKind.Shockwave) || (Phase == 1 && t.Kind == BossAttackKind.Charge) || (Phase == 2 && t.Kind == BossAttackKind.Sweep))
                SetVulnerable(0f);
        }

        protected override void OnVulnerableChanged(bool v)
        {
            if (v) Say(Phase == 0 ? "OVERHEATING. HEART EXPOSED." : Phase == 1 ? "DIZZY. SO DIZZY." : "REBOOTING. DO NOT HACK.");
        }

        protected override void OnHit(int hitsLeft) { UpdateBar(); Say(hitsLeft > 0 ? "OW. FILING A COMPLAINT." : "ERROR. ERROR."); }

        protected override void OnDefeated() { BarLabel = "BONKS 4/4"; Say("I... SHOULD HAVE... DELEGATED."); }

        void UpdateBar() { BarLabel = "BONKS " + TotalHits() + "/4"; RaiseStatus(); }

        void Say(string line) => Fx.FloatingText(transform.position + Vector3.up * 5f, line, UIColors.Danger, 1.2f);
    }
}
