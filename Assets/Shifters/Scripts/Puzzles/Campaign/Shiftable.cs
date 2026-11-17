// =====================================================================================
//  SHIFTERS - Shiftable.cs   [PUZZLES - campaign wave, CAMPAIGN §13.2 BIT-SHIFTER]
//  Size classes for a dynamic prop: NORMAL (0) -> HUGE (1: x Tuning.HugeScale size, x Tuning.HugeMassMul mass)
//  -> TINY (-1: x Tuning.TinyScale, x Tuning.TinyMassMul) -> NORMAL. Growing needs clearance ("OVERFLOW ERROR"),
//  NO-SIZE zones refuse ("HR SAYS NO."), a respawn / checkpoint reset puts it back to NORMAL.
//  Pushable blocks: HUGE = "PERMANENT FIXTURE" (unpushable), TINY = carryable by anyone (Grabbable enabled).
//  Added by PuzzleKit.MakeShiftable(rb).
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    [DisallowMultipleComponent]
    public class Shiftable : MonoBehaviour, IResizable, IResettable
    {
        public const string OverflowMessage = "OVERFLOW ERROR";
        public const string RespawningMessage = "SEGMENTATION FAULT (TRY AGAIN)";
        public const int UnpushableStrength = 99;

        [Tooltip("Mass at NORMAL size (kg).")] public float BaseMass = 25f;
        [Tooltip("localScale at NORMAL size.")] public Vector3 BaseScale = Vector3.one;
        [Tooltip("Collider bounds in local units (unscaled).")] public Bounds LocalBounds = new Bounds(Vector3.zero, Vector3.one);
        public int BasePushStrength = Tuning.HeavyPushStrength;
        [Tooltip("Tween seconds of a size change.")] public float TweenSeconds = 0.28f;

        public int SizeClass { get; private set; }
        public int Changes { get; private set; }
        public Rigidbody Body { get; private set; }
        public float CurrentScale => ScaleOf(SizeClass);

        Pushable _push;
        Grabbable _grab;
        Respawnable _respawn;
        float _tween = -1f, _fromScale = 1f;
        static readonly Collider[] _overlap = new Collider[32];

        public static float ScaleOf(int cls) => cls > 0 ? Tuning.HugeScale : cls < 0 ? Tuning.TinyScale : 1f;
        public static float MassMulOf(int cls) => cls > 0 ? Tuning.HugeMassMul : cls < 0 ? Tuning.TinyMassMul : 1f;
        public static int NextClass(int cls) => cls == 0 ? 1 : cls > 0 ? -1 : 0;
        public static string ClassName(int cls) => cls > 0 ? "HUGE" : cls < 0 ? "TINY" : "NORMAL";

        void Awake() => Cache();

        void Cache()
        {
            if (Body == null) Body = GetComponent<Rigidbody>();
            if (_push == null) _push = GetComponent<Pushable>();
            if (_grab == null) _grab = GetComponent<Grabbable>();
            if (_respawn == null) _respawn = GetComponent<Respawnable>();
        }

        /// <summary>Capture NORMAL scale/mass/bounds from the current state (factory; also edit mode).</summary>
        public void Capture()
        {
            Cache();
            BaseScale = transform.localScale;
            BaseMass = Body != null ? Body.mass : BaseMass;
            if (_push != null) BasePushStrength = _push.RequiredStrength;
            var cols = GetComponentsInChildren<Collider>(true);
            bool any = false;
            var b = new Bounds();
            foreach (var c in cols)
            {
                if (c == null || c.isTrigger) continue;
                Bounds lb;
                if (c is BoxCollider bc) lb = new Bounds(bc.center, bc.size);
                else if (c is SphereCollider sc) lb = new Bounds(sc.center, Vector3.one * sc.radius * 2f);
                else if (c is CapsuleCollider cc) lb = new Bounds(cc.center, new Vector3(cc.radius * 2f, cc.height, cc.radius * 2f));
                else if (c is MeshCollider mc && mc.sharedMesh != null) lb = mc.sharedMesh.bounds;
                else continue;
                // to root-local (child colliders: through their local pose relative to the root)
                if (c.transform != transform)
                {
                    var m = transform.worldToLocalMatrix * c.transform.localToWorldMatrix;
                    var mn = m.MultiplyPoint3x4(lb.min); var mx = m.MultiplyPoint3x4(lb.max);
                    lb = new Bounds((mn + mx) * 0.5f, Vector3.zero); lb.Encapsulate(mn); lb.Encapsulate(mx);
                }
                if (!any) { b = lb; any = true; } else b.Encapsulate(lb);
            }
            if (any) LocalBounds = b;
        }

        // ------------------------------------------------------------------ IResizable
        public bool TryCycleSize(ShifterCharacter by, out string reason)
        {
            Cache();
            reason = null;
            if (NoToolZone.Blocks(transform.position, ToolMask.BitShifter)) { reason = NoToolZone.Message; return false; }
            if (_respawn != null && _respawn.IsRespawning) { reason = RespawningMessage; return false; }
            int next = NextClass(SizeClass);
            if (ScaleOf(next) > CurrentScale && !HasClearance(next))
            {
                reason = OverflowMessage;
                Fx.FloatingText(transform.position + Vector3.up * 1.2f, OverflowMessage, UIColors.Danger, 1.1f);
                Sfx.At(SfxId.Denied, transform.position);
                return false;
            }
            SetSize(next, true);
            return true;
        }

        /// <summary>Would this prop fit at size class 'cls' where it stands (bottom kept on its support)?</summary>
        public bool HasClearance(int cls)
        {
            float s = ScaleOf(cls);
            var rot = transform.rotation;
            var scaled = Vector3.Scale(BaseScale, LocalBounds.extents) * s;
            var bottom = BottomY();
            var center = PivotAt(s, bottom) + rot * Vector3.Scale(BaseScale * s, LocalBounds.center);
            var half = new Vector3(Mathf.Max(0.02f, scaled.x - 0.04f), Mathf.Max(0.02f, scaled.y - 0.05f), Mathf.Max(0.02f, scaled.z - 0.04f));
            center += Vector3.up * 0.03f;
            int n = Physics.OverlapBoxNonAlloc(center, half, _overlap, rot, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var c = _overlap[i];
                if (c == null) continue;
                if (c.transform.IsChildOf(transform)) continue;
                if (Body != null && c.attachedRigidbody == Body) continue;
                return false;
            }
            return true;
        }

        /// <summary>Set the size class directly (tests, scripted beats). animate = tween + fx.</summary>
        public void SetSize(int cls, bool animate)
        {
            Cache();
            cls = Mathf.Clamp(cls, -1, 1);
            float from = transform.localScale.x / Mathf.Max(0.0001f, BaseScale.x);
            int old = SizeClass;
            SizeClass = cls;
            if (old != cls) Changes++;
            if (_grab != null && _grab.IsHeld && cls > old && _grab.Holder != null) _grab.Holder.ForceRelease();
            if (Body != null)
            {
                Body.mass = BaseMass * MassMulOf(cls);
                if (!Body.isKinematic) { Body.linearVelocity = Vector3.zero; Body.angularVelocity = Vector3.zero; }
            }
            if (_push != null)
            {
                _push.RequiredStrength = cls > 0 && BaseMass >= 100f ? UnpushableStrength : BasePushStrength;
                bool tiny = cls < 0;
                _push.enabled = !tiny;
                if (Body != null) Body.constraints = tiny ? RigidbodyConstraints.None : RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;
                if (_grab != null) _grab.CanBeGrabbed = tiny;
            }
            if (animate)
            {
                _fromScale = from;
                _tween = 0f;
                var p = transform.position + Vector3.up * 0.6f;
                string label = cls > 0 ? "<< HUGE" : cls < 0 ? ">> TINY" : "NORMAL";
                Fx.FloatingText(p + Vector3.up * 0.6f, label, UIColors.Info, 1.2f);
                Fx.Sparks(p, Vector3.up, 14, new Color(0.3f, 1f, 0.6f));
                Sfx.At(SfxId.BitShift, transform.position);
                Sfx.At(cls > old ? SfxId.LandHeavy : SfxId.UIPop, transform.position, 0.6f, cls > old ? 0.8f : 1.5f);
                if (cls > 0 && _push != null && BaseMass >= 100f)
                    GameEvents.RaiseToast("PERMANENT FIXTURE. IT LIVES HERE NOW.", UIColors.AccentYellow);
            }
            else
            {
                _tween = -1f;
                ApplyScale(ScaleOf(cls), BottomY());
            }
        }

        float BottomY()
        {
            float s = transform.localScale.x / Mathf.Max(0.0001f, BaseScale.x);
            return transform.position.y - PivotToBottom(s);
        }

        float PivotToBottom(float s) => -(LocalBounds.min.y * BaseScale.y * s);

        Vector3 PivotAt(float s, float bottomY)
        {
            var p = transform.position;
            p.y = bottomY + PivotToBottom(s);
            return p;
        }

        void ApplyScale(float s, float bottomY)
        {
            transform.localScale = BaseScale * s;
            var p = PivotAt(s, bottomY);
            transform.position = p;
            if (Body != null) Body.position = p;
        }

        void Update()
        {
            if (_respawn != null && _respawn.IsRespawning && SizeClass != 0) SetSize(0, false);
            if (_tween < 0f) return;
            _tween += Time.deltaTime / Mathf.Max(0.05f, TweenSeconds);
            float t = Mathf.Clamp01(_tween);
            // ease-out-back: overshoot a little, then settle
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float e = 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
            float bottom = BottomY();
            ApplyScale(Mathf.LerpUnclamped(_fromScale, ScaleOf(SizeClass), e), bottom);
            if (t >= 1f) _tween = -1f;
        }

        public void ResetState() { if (SizeClass != 0) SetSize(0, false); }
    }
}
