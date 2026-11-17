// =====================================================================================
//  SHIFTERS - CharacterPuppet.cs   [CONTRACT API - implementation owned by the VISUALS agent]
//  Visual-only character (no physics, no ShifterCharacter) built from a profile: used by the
//  PortraitStudio, the main-menu stage and the Team Reveal screen. Idles, blinks, emotes.
//  Shares CharacterModelBuilder + CharacterAnimator + FaceController with CharacterVisual.
//  Animates on UNSCALED time by default (portraits stay lively while the switcher slows time).
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public class CharacterPuppet : MonoBehaviour
    {
        public CharacterProfile Profile { get; protected set; }
        public Transform Head { get; protected set; }

        public CharacterRig Rig { get; protected set; }
        public CharacterAnimator Animator { get; protected set; }
        public FaceController Face => Rig != null ? Rig.Face : null;
        public Transform WeaponSocket => Rig != null ? Rig.WeaponSocket : null;
        /// <summary>true (default) = animate with unscaled time.</summary>
        public bool UseUnscaledTime = true;
        /// <summary>World point the eyes follow (null = straight ahead).</summary>
        public Vector3? LookTarget;
        public Expression CurrentExpression { get; protected set; }
        public PoseOverride Pose { get; protected set; }
        /// <summary>World-space head center and approximate head radius (portrait framing).</summary>
        public Vector3 HeadCenter => Rig != null ? Rig.HeadCenterWorld : transform.position;
        public float HeadRadius => Rig != null ? Mathf.Max(Rig.D.HeadHalf.x, Rig.D.HeadHalf.y) : 0.2f;

        protected Expression _base = Expression.Neutral, _temp; protected float _tempUntil = -1f;
        protected float _speed, _vy; protected bool _grounded = true, _sprint;
        protected float _time;
        /// <summary>(campaign) Stop animating (statues, frozen cutscene frames). Tick(dt) still works when called manually.</summary>
        public bool Frozen;
        /// <summary>(campaign) Solid gold statue look (founders' statue).</summary>
        public bool IsGold => Rig != null && Rig.IsGold;
        /// <summary>(campaign) Trembles + Scared face (SCARED interns in portraits / cutscenes).</summary>
        public bool Scared;

        /// <summary>Build a puppet under parent. Feet at localPosition, facing +Z of localRotation.</summary>
        public static CharacterPuppet Create(CharacterProfile profile, Transform parent, Vector3 localPosition, Quaternion localRotation)
        {
            if (profile == null) profile = new CharacterProfile();
            var go = new GameObject("Puppet_" + profile.Name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = localRotation;
            var p = go.AddComponent<CharacterPuppet>();
            p.Profile = profile;
            int seed = (profile.Name ?? "").Length * 977 + (int)(profile.PrimaryColor.r * 1000f);
            p.Rig = CharacterModelBuilder.Build(profile, go.transform, seed);
            p.Animator = new CharacterAnimator(p.Rig, seed + 1);
            p.Head = p.Rig.Head;
            p.Tick(0f);
            return p;
        }

        public virtual void SetExpression(Expression e, float duration = 0f)
        {
            if (duration > 0f) { _temp = e; _tempUntil = _time + duration; }
            else { _base = e; _tempUntil = -1f; }
        }

        public virtual void Play(VisualAction action) { Animator?.Play(action); }
        /// <summary>0 = calm idle, 1 = bouncy/excited, 2 = nervous.</summary>
        public virtual void SetIdleStyle(int style) { if (Animator != null) Animator.IdleStyle = Mathf.Clamp(style, 0, 2); }
        public virtual void SetDesaturated(bool on) { Rig?.Mats.SetDesaturated(on); }

        public void SetHighlight(Color color, float strength) { Rig?.Mats.SetHighlight(color, strength); }
        /// <summary>Full-body pose (KO, Captured, Carrying, Aiming, Bridge...). None = idle.</summary>
        public void SetPose(PoseOverride pose) { Pose = pose; }
        /// <summary>Fake locomotion (walk/run in place on the menu stage, pose sheets).</summary>
        public void SetMotion(float planarSpeed, bool grounded = true, float verticalVelocity = 0f, bool sprinting = false)
        { _speed = planarSpeed; _grounded = grounded; _vy = verticalVelocity; _sprint = sprinting; }
        public void SetBridgeStretch(float length, float sag01) { Animator?.SetBridge(length, sag01); }
        public void SetVisible(bool on) { Rig?.SetVisible(on); }
        /// <summary>(campaign) Solid gold statue look on/off (CH14 founders' statue). Combine with Frozen for a still pose.</summary>
        public virtual void SetGold(bool on) { CharacterGold.Apply(Rig, on); }
        /// <summary>(campaign) Heist disguise: a big fake mustache (CharacterDisguise).</summary>
        public void SetDisguise(bool on) { CharacterDisguise.SetMustache(Rig, on); }
        public bool HasDisguise => CharacterDisguise.HasMustache(Rig);

        /// <summary>Open eyes, finish crossfades and apply the pose right now (before a static portrait render).</summary>
        public void PrepareSnapshot()
        {
            if (Rig == null) return;
            Animator.SnapPose();
            Rig.Face.SuppressBlink = true;
            Tick(0.0001f);
            Rig.Face.Snap();
            Rig.Face.Tick(0.0001f, LookTarget);
            Rig.Face.SuppressBlink = false;
        }

        protected virtual Expression Resolve()
        {
            if (_tempUntil >= 0f && _time < _tempUntil) return _temp;
            if (Scared && _base == Expression.Neutral) return Expression.Scared;
            if (Animator != null && Animator.IsGesturing && _base == Expression.Neutral) return CharacterGestures.Face(Animator.CurrentGesture);
            if (_base == Expression.Neutral && Animator != null)
            {
                if (Pose == PoseOverride.KO) return Expression.KO;
                if (Pose == PoseOverride.Captured) return Expression.Scared;
                if (Pose == PoseOverride.Bridge) return Expression.Strain;
                if (Animator.IdleStyle == 1) return Expression.Happy;
                if (Animator.IdleStyle == 2) return Expression.Scared;
            }
            return _base;
        }

        /// <summary>Advance animation by dt (called automatically in LateUpdate).</summary>
        public virtual void Tick(float dt)
        {
            if (Rig == null || Animator == null) return;
            _time += dt;
            Animator.ExtraTremble = Scared ? 2.6f : 0f;
            var inp = AnimInput.Idle;
            inp.Speed = _speed; inp.Grounded = _grounded; inp.VerticalSpeed = _vy; inp.Sprinting = _sprint;
            inp.Turbo = _sprint && Profile != null && Profile.HasSpeedPerk;
            inp.AirTime = _grounded ? 0f : 0.6f;
            inp.Pose = Pose;
            inp.Strain01 = Pose == PoseOverride.Bridge ? 0.5f : 0f;
            Animator.Tick(dt, inp);
            var e = Resolve();
            CurrentExpression = e;
            Rig.Face.Set(e);
            Rig.Face.Tick(dt, LookTarget);
        }

        protected virtual void LateUpdate() { if (!Frozen) Tick(UseUnscaledTime ? TimeController.RealDeltaTime : Time.deltaTime); }

        protected virtual void OnDestroy() { Rig?.Mats?.Destroy(); }
    }
}
