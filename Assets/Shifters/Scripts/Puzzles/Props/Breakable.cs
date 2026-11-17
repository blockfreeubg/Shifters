using UnityEngine;

namespace Shifters
{
    public enum BreakableKind { Glass, CrackedWall }

    /// <summary>
    /// Glass pane or cracked wall. Glass breaks after ShotsToBreak hits (IShootable) or from any fast thrown body.
    /// A cracked wall breaks from an E-punch by STR &gt;= RequiredStrength ("Punch wall"), a thrown strong character,
    /// or ShotsToBreak hits (0 = immune to bullets). Shards are pooled physics debris that fade after 3 s.
    /// It is also a SignalSource: ON once broken (wire "shoot the glass" to anything).
    /// </summary>
    public class Breakable : SignalSource, IShootable, IInteractable, IResettable
    {
        public BreakableKind Kind = BreakableKind.Glass;
        public int ShotsToBreak = 1;
        public int RequiredStrength = Tuning.HeavyPushStrength;
        [Tooltip("Relative impact speed of a thrown body that breaks it.")] public float ImpactBreakSpeed = 6f;
        public Vector3 Size = new Vector3(3f, 3f, 0.1f);

        [Header("Parts")]
        public GameObject Intact;
        public Collider Solid;
        public Revealable Reveal;
        public Material ShardMaterial;

        public bool IsBroken => IsOn;

        void Start()
        {
            if (Reveal == null) return;
            if (Kind == BreakableKind.Glass) Reveal.DynamicLabel = v => IsBroken ? "" : "SHOOT TO BREAK";
            else Reveal.DynamicLabel = v => IsBroken ? "" : v != null && v.Stats != null && v.Stats.Strength >= RequiredStrength
                ? "CRACKED WALL - PUNCH IT (E)" : "CRACKED WALL - NEEDS STR " + RequiredStrength;
        }
        public int Hits { get; private set; }
        float _lastDenied;

        // ------------------------------------------------------------------ shooting
        public void OnShot(in ShotInfo shot)
        {
            if (IsBroken) return;
            if (ShotsToBreak <= 0)
            {
                Fx.Sparks(shot.Point, shot.Normal, 6, Color.white);
                Sfx.At(SfxId.Ricochet, shot.Point, 0.6f);
                return;
            }
            Hits++;
            Sfx.At(Kind == BreakableKind.Glass ? SfxId.Impact : SfxId.MetalHit, shot.Point, 0.7f, 1.2f);
            if (Hits >= ShotsToBreak) Break(shot.Direction);
            else Fx.FloatingText(shot.Point, "CRACK", Color.white, 0.8f);
        }

        // ------------------------------------------------------------------ punching (cracked wall)
        public int InteractPriority => 0;
        public Vector3 InteractPoint => transform.position + Vector3.up * Mathf.Min(1.2f, Size.y * 0.4f);

        public bool CanInteract(ShifterCharacter who, out string blockedReason)
        {
            blockedReason = null;
            if (Kind != BreakableKind.CrackedWall || IsBroken || who == null) return false;
            if (who.Stats == null || who.Stats.Strength < RequiredStrength)
            {
                blockedReason = PuzzleUtil.StrBlockedReason(RequiredStrength, who);
                return false;
            }
            return true;
        }

        public string GetPrompt(ShifterCharacter who) => "Punch wall";

        public void Interact(ShifterCharacter who)
        {
            if (!CanInteract(who, out var reason))
            {
                if (reason != null && PuzzleUtil.RateLimit(ref _lastDenied, 1.5f)) { Sfx.At(SfxId.Bonk, InteractPoint, 0.7f, 0.7f); if (who != null) who.Say("Ow. My knuckles."); }
                return;
            }
            PuzzleUtil.Gesture(who, VisualAction.Punch);
            Sfx.At(SfxId.Bonk, InteractPoint, 1f, 0.6f);
            var dir = transform.position - who.transform.position; dir.y = 0f;
            Break(dir.sqrMagnitude > 0.01f ? dir.normalized : transform.forward);
            who.Say("HULK... er, Brute SMASH.");
        }

        // ------------------------------------------------------------------ impacts
        void OnCollisionEnter(Collision c)
        {
            if (IsBroken) return;
            float s = c.relativeVelocity.magnitude;
            if (s < ImpactBreakSpeed) return;
            var rb = c.rigidbody;
            if (rb == null) return;
            bool isChar = rb.TryGetComponent(out ShifterCharacter ch);
            if (Kind == BreakableKind.Glass)
            {
                if (isChar && ch.Condition != CharacterCondition.Thrown) return; // walking into glass doesn't break it
                Break(-c.relativeVelocity.normalized);
            }
            else if (isChar && ch.Condition == CharacterCondition.Thrown && ch.Stats != null && ch.Stats.Strength >= RequiredStrength)
            {
                Break(-c.relativeVelocity.normalized);
                ch.Say("I AM THE WRECKING BALL!");
            }
        }

        /// <summary>Break now (scripts/tests too).</summary>
        public void Break(Vector3 direction)
        {
            if (IsBroken) return;
            if (Intact != null) Intact.SetActive(false);
            if (Solid != null) Solid.enabled = false;
            var dir = direction.sqrMagnitude > 1e-4f ? direction.normalized : transform.forward;
            bool glass = Kind == BreakableKind.Glass;
            var mat = ShardMaterial != null ? ShardMaterial : Kit.Mat(glass ? MatKey.Glass : MatKey.Concrete);
            PuzzleShardPool.Burst(transform.position + Vector3.up * Size.y * 0.5f, Size, transform.rotation, mat, glass ? 14 : 12, glass ? 0.22f : 0.45f, dir * (glass ? 5f : 4f));
            Sfx.At(SfxId.Break, transform.position + Vector3.up * Size.y * 0.5f, 1f, glass ? 1.2f : 0.7f);
            if (!glass) Sfx.At(SfxId.Crumble, transform.position, 1f, 0.8f);
            Fx.Puff(transform.position + Vector3.up * Size.y * 0.4f, glass ? 0.8f : 2f, glass ? new Color(0.8f, 0.95f, 1f) : MaterialLibrary.DefaultColor(MatKey.Concrete));
            GameEvents.RaiseNoise(transform.position, 10f, null, 1f);
            SetOn(true);
        }

        public void ResetState()
        {
            Hits = 0;
            if (Intact != null) Intact.SetActive(true);
            if (Solid != null) Solid.enabled = true;
            SetOn(false);
        }
    }
}
