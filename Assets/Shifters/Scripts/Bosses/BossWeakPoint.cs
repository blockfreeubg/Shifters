// =====================================================================================
//  SHIFTERS - BossWeakPoint.cs   [BOSSES module - B1/B11]
//  Where specialist actions turn into boss hits. Kind is a flag set:
//    Shoot    - any IShootable.OnShot (every gun AND tool, B11)
//    Punch    - E-interact "Punch" by STR >= RequiredStrength
//    Hack/Plate/TeamZone/Swap/Custom - the rising edge of Source (Terminal, plate, zone trigger...)
//    Impact   - a rigidbody of >= MinMassKg (prop or thrown intern) hitting the trigger at >= MinImpactSpeed
//  PhaseMask: bit p = active in phase p (0 = every phase). Glow renderers light up while a hit would count.
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public sealed class BossWeakPoint : MonoBehaviour, IShootable, IInteractable
    {
        public BossHitKind Kind = BossHitKind.Shoot;
        [Tooltip("Bit p = accepts hits in phase p. 0 = every phase.")] public int PhaseMask;
        public BossBase Boss;
        [Header("Punch")] public int RequiredStrength = 7;
        public string Prompt = "Punch";
        [Header("Impact")] public float MinMassKg = 90f;
        public float MinImpactSpeed = 2f;
        [Header("Signal kinds")] public SignalSource Source;
        [Header("Feedback")] public Renderer[] Glow = new Renderer[0];
        public string HitLabel = "";

        public int HitsLanded { get; private set; }
        public int Attempts { get; private set; }
        float _lastImpact = -99f, _lastDenied = -99f;
        bool _lit;
        MaterialPropertyBlock _mpb;

        public bool ActiveInPhase => Boss != null && (PhaseMask == 0 || (PhaseMask & (1 << Mathf.Clamp(Boss.Phase, 0, 30))) != 0);
        /// <summary>A hit of one of this point's kinds would count right now.</summary>
        public bool Hittable => ActiveInPhase && Boss.CanAccept(Kind);

        void OnEnable() { if (Source != null) Source.Changed += OnSourceChanged; }
        void OnDisable() { if (Source != null) Source.Changed -= OnSourceChanged; }

        /// <summary>Rewire Source at runtime (factories call it after creating the terminal/plate).</summary>
        public void SetSource(SignalSource s)
        {
            if (Source != null) Source.Changed -= OnSourceChanged;
            Source = s;
            if (Source != null && isActiveAndEnabled) Source.Changed += OnSourceChanged;
        }

        bool Deliver(BossHitKind kind, Vector3 point, ShifterCharacter by)
        {
            Attempts++;
            if (!ActiveInPhase || Boss == null) return false;
            bool ok = Boss.TryHit(kind, point, by);
            if (ok) HitsLanded++;
            return ok;
        }

        // ------------------------------------------------------------------ Shoot
        public void OnShot(in ShotInfo shot)
        {
            if ((Kind & BossHitKind.Shoot) == 0) { Fx.Sparks(shot.Point, shot.Normal, 6, Color.white); return; }
            if (!Deliver(BossHitKind.Shoot, shot.Point, shot.Shooter)) Fx.Sparks(shot.Point, shot.Normal, 8, Color.white);
        }

        // ------------------------------------------------------------------ Punch (IInteractable)
        public int InteractPriority => 3;
        public Vector3 InteractPoint => transform.position;

        public bool CanInteract(ShifterCharacter who, out string blockedReason)
        {
            blockedReason = null;
            if ((Kind & BossHitKind.Punch) == 0 || who == null || Boss == null || !ActiveInPhase) return false;
            if (Boss.State == BossState.Defeated) return false;
            if (who.Stats == null || who.Stats.Strength < RequiredStrength) { blockedReason = PuzzleUtil.StrBlockedReason(RequiredStrength, who); return false; }
            if (!Boss.CanAccept(BossHitKind.Punch)) { blockedReason = "WAIT FOR AN OPENING"; return false; }
            return true;
        }

        public string GetPrompt(ShifterCharacter who) => Prompt;

        public void Interact(ShifterCharacter who)
        {
            if (!CanInteract(who, out var reason))
            {
                if (reason != null && PuzzleUtil.RateLimit(ref _lastDenied, 1.2f)) { Sfx.At(SfxId.Bonk, InteractPoint, 0.7f, 0.7f); if (who != null) who.Say("Ow. Not yet."); }
                return;
            }
            PuzzleUtil.Gesture(who, VisualAction.Punch);
            Sfx.At(SfxId.Bonk, InteractPoint, 1f, 0.55f);
            Deliver(BossHitKind.Punch, InteractPoint, who);
        }

        // ------------------------------------------------------------------ Signal kinds
        void OnSourceChanged(SignalSource src, bool on)
        {
            if (!on) return;
            var kind = FirstSignalKind();
            if (kind == BossHitKind.None) return;
            var t = src as Terminal;
            Deliver(kind, transform.position, t != null ? t.Hacker : null);
        }

        BossHitKind FirstSignalKind()
        {
            if ((Kind & BossHitKind.Hack) != 0) return BossHitKind.Hack;
            if ((Kind & BossHitKind.Plate) != 0) return BossHitKind.Plate;
            if ((Kind & BossHitKind.TeamZone) != 0) return BossHitKind.TeamZone;
            if ((Kind & BossHitKind.Swap) != 0) return BossHitKind.Swap;
            if ((Kind & BossHitKind.Custom) != 0) return BossHitKind.Custom;
            return BossHitKind.None;
        }

        /// <summary>Custom triggers (level scripts): deliver one hit of the given kind here.</summary>
        public bool Trigger(BossHitKind kind, ShifterCharacter by = null) => (Kind & kind) != 0 && Deliver(kind, transform.position, by);

        // ------------------------------------------------------------------ Impact
        void OnTriggerEnter(Collider other) => TryImpact(other.attachedRigidbody, other.attachedRigidbody != null ? other.attachedRigidbody.linearVelocity : Vector3.zero);
        void OnCollisionEnter(Collision c) => TryImpact(c.rigidbody, c.relativeVelocity);

        void TryImpact(Rigidbody rb, Vector3 vel)
        {
            if ((Kind & BossHitKind.Impact) == 0 || rb == null) return;
            if (Boss != null && rb.transform.IsChildOf(Boss.transform)) return;
            var ch = rb.GetComponent<ShifterCharacter>();
            float mass = ch != null ? ch.TotalMass : rb.mass;
            bool thrown = ch != null && ch.Condition == CharacterCondition.Thrown;
            if (mass < MinMassKg) return;
            if (!thrown && vel.magnitude < MinImpactSpeed) return;
            if (Time.time - _lastImpact < 1f) return;
            _lastImpact = Time.time;
            Deliver(BossHitKind.Impact, rb.position, ch);
        }

        // ------------------------------------------------------------------ feedback
        void Update()
        {
            bool lit = Hittable;
            if (lit == _lit && !lit) return;
            _lit = lit;
            if (Glow == null || Glow.Length == 0) return;
            _mpb ??= new MaterialPropertyBlock();
            float k = lit ? 1.5f + 0.8f * Mathf.Sin(Time.time * 8f) : 0f;
            var col = (lit ? UIColors.AccentYellow : Color.black) * k;
            for (int i = 0; i < Glow.Length; i++)
            {
                var r = Glow[i];
                if (r == null) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor(ShaderIds.EmissionColor, col);
                r.SetPropertyBlock(_mpb);
            }
        }
    }
}
