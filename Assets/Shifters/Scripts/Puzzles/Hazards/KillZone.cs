using UnityEngine;

namespace Shifters
{
    public enum KillZoneType { Void, Acid, Incinerator, FoamPit }

    /// <summary>
    /// Trigger volume (box collider on this GameObject). Characters are Lost with a themed cause ("fell into the void",
    /// "dissolved in acid", "was incinerated"); props with a Respawnable poof home. FoamPit (tutorial): characters
    /// are bounced back to RespawnPoint with a poof and the toast "Foam pit! No interns were harmed."
    /// Carried characters are ignored (their carrier decides their fate).
    /// </summary>
    public class KillZone : MonoBehaviour
    {
        public KillZoneType Type = KillZoneType.Void;
        [Tooltip("Optional override for the Lost cause text.")] public string Cause = "";
        [Tooltip("FoamPit: world position characters are returned to.")] public Vector3 RespawnPoint;
        public float RespawnYaw;

        public int Victims { get; private set; }
        public int Bounces { get; private set; }

        readonly PuzzleCooldown _cool = new PuzzleCooldown(0.75f);

        public static string CauseFor(KillZoneType t)
        {
            switch (t)
            {
                case KillZoneType.Acid: return "dissolved in acid";
                case KillZoneType.Incinerator: return "was incinerated";
                case KillZoneType.FoamPit: return "fell in the foam pit";
                default: return "fell into the void";
            }
        }

        void OnTriggerEnter(Collider other) => Handle(other);
        void OnTriggerStay(Collider other) => Handle(other);

        void Handle(Collider other)
        {
            var rb = other.attachedRigidbody;
            if (rb == null) return;
            if (rb.TryGetComponent(out ShifterCharacter c)) { HandleCharacter(c); return; }
            if (rb.TryGetComponent(out Respawnable r))
            {
                if (r.IsRespawning) return;
                if (rb.TryGetComponent(out Grabbable g) && g.IsHeld) return;
                Splash(rb.position, false);
                r.Respawn();
            }
        }

        void HandleCharacter(ShifterCharacter c)
        {
            if (c.Condition == CharacterCondition.Carried) return;
            if (Type == KillZoneType.FoamPit)
            {
                if (c.State == CharacterState.Lost || !_cool.Ready(c)) return;
                Bounces++;
                Fx.Poof(c.Center, c.Color, 1.2f);
                Sfx.At(SfxId.Bounce, c.Center);
                Sfx.At(SfxId.Squeak, c.Center, 0.8f);
                var rot = Quaternion.Euler(0f, RespawnYaw, 0f);
                if (c.Motor != null) c.Motor.Teleport(RespawnPoint, rot);
                else c.transform.SetPositionAndRotation(RespawnPoint, rot);
                Fx.Poof(RespawnPoint + Vector3.up, c.Color, 1.2f);
                GameEvents.RaiseToast("Foam pit! No interns were harmed.", UIColors.Info);
                return;
            }
            if (c.State == CharacterState.Lost) return;
            Victims++;
            Splash(c.Center, true);
            c.Lose(string.IsNullOrEmpty(Cause) ? CauseFor(Type) : Cause);
        }

        void Splash(Vector3 p, bool character)
        {
            switch (Type)
            {
                case KillZoneType.Acid:
                    Fx.Splash(p, MaterialLibrary.DefaultColor(MatKey.Acid));
                    Sfx.At(SfxId.Splash, p);
                    Sfx.At(SfxId.Sizzle, p, 0.9f);
                    break;
                case KillZoneType.Incinerator:
                    Fx.Sparks(p, Vector3.up, 24, UIColors.Accent);
                    Fx.Puff(p, 1.5f, new Color(0.2f, 0.2f, 0.2f));
                    Sfx.At(SfxId.Sizzle, p);
                    if (character) Sfx.At(SfxId.Explosion, p, 0.5f, 1.3f);
                    break;
                case KillZoneType.FoamPit:
                    Fx.Poof(p, Color.white, 1f);
                    Sfx.At(SfxId.Squeak, p);
                    break;
            }
        }
    }
}
