// =====================================================================================
//  SHIFTERS - ShiftersCamera.cs   [CONTRACT API - implementation owned by the CORE agent]
//  Third-person orbit camera: smooth follow, collision (sphere cast ignoring characters and
//  triggers), mouse orbit, scroll zoom, over-the-shoulder aiming, screen shake, and the
//  signature GTA-style character-switch transition (rise -> travel -> drop).
//  Tight spots (polish): over-the-shoulder + lift framing when the boom is blocked, and any team
//  body the camera is inside / right against renders shadows-only (see TightFrom, SelfHideNear).
//  Lives on the scene's main Camera. LevelManager creates one if the scene has none.
//  Runs on UNSCALED time (works during switcher slow-mo and while time ramps).
// =====================================================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Shifters
{
    [RequireComponent(typeof(Camera))]
    public class ShiftersCamera : MonoBehaviour
    {
        public static ShiftersCamera Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; MainCameraSuppressed = false; }

        /// <summary>True while SuppressMainCamera(true) is in effect (the story CutsceneCamera renders instead).</summary>
        public static bool MainCameraSuppressed { get; private set; }
        Camera _replacement;
        bool _retagged;

        /// <summary>Story seam: suppress = the gameplay camera stops rendering (Camera component disabled; its AudioListener,
        /// follow logic and Instance stay alive) so exactly one camera renders. Pass the CutsceneCamera as replacement:
        /// it is enabled, tagged MainCamera while suppressed (so Camera.main, billboards and world labels follow it; Camera.main
        /// would be NULL without one) and gets URP post-processing. suppress = false re-enables the gameplay camera and untags
        /// the replacement it tagged. Do not put an AudioListener on the replacement.</summary>
        public void SuppressMainCamera(bool suppress, Camera replacement = null)
        {
            if (suppress)
            {
                if (replacement != null && replacement != Cam)
                {
                    if (_replacement != null && _replacement != replacement && _retagged) _replacement.tag = "Untagged";
                    _replacement = replacement;
                    _retagged = !replacement.CompareTag("MainCamera");
                    if (_retagged) replacement.tag = "MainCamera";
                    replacement.enabled = true;
                    var data = replacement.GetUniversalAdditionalCameraData();
                    if (data != null) data.renderPostProcessing = true;
                }
                if (Cam != null) Cam.enabled = false;
                MainCameraSuppressed = true;
            }
            else
            {
                if (Cam != null) Cam.enabled = true;
                if (_replacement != null && _retagged) _replacement.tag = "Untagged";
                _replacement = null;
                _retagged = false;
                MainCameraSuppressed = false;
            }
        }

        public Camera Cam { get; private set; }
        public ShifterCharacter Target { get; protected set; }
        public float Yaw { get; set; }
        public float Pitch { get; set; } = 15f;
        public bool IsAiming { get; set; }
        public bool IsTransitioning { get; protected set; }
        /// <summary>When false the camera ignores look input (menus, cutscenes).</summary>
        public bool InputEnabled { get; set; } = true;

        public Vector3 FlatForward { get { var f = transform.forward; f.y = 0f; return f.sqrMagnitude > 1e-4f ? f.normalized : Vector3.forward; } }
        public Vector3 FlatRight { get { var r = transform.right; r.y = 0f; return r.sqrMagnitude > 1e-4f ? r.normalized : Vector3.right; } }
        /// <summary>Ray through the screen center (crosshair).</summary>
        public Ray AimRay => new Ray(transform.position, transform.forward);

        // ---------------------------------------------------------------- added API
        /// <summary>Requested zoom distance (scroll wheel), Tuning.CamMinDistance..CamMaxDistance.</summary>
        public float ZoomDistance { get => _zoom; set => _zoom = Mathf.Clamp(value, Tuning.CamMinDistance, Tuning.CamMaxDistance); }
        /// <summary>Actual distance from the pivot after collision.</summary>
        public float CurrentDistance => _camDist;
        /// <summary>The (smoothed) orbit pivot in world space.</summary>
        public Vector3 Pivot => _pivot;
        /// <summary>Kind of the running/last transition (true = GTA sky cam, false = glide).</summary>
        public bool LastTransitionWasSky { get; private set; }
        /// <summary>Unscaled duration of the running/last transition.</summary>
        public float LastTransitionDuration { get; private set; }

        /// <summary>Unscaled frame delta that stays deterministic in capture mode (Time.captureDeltaTime, DevPlay/video):
        /// camera, switcher ramps and switch transitions use it so they behave identically in batch tests and real play.</summary>
        public static float UnscaledDeltaTime => Time.captureDeltaTime > 0f ? Time.captureDeltaTime : Time.unscaledDeltaTime;

        public const float FollowSmoothTime = 0.08f;
        public const float CollisionRadius = 0.25f;
        public const float ShoulderOffset = 0.6f;
        public const float AimFovDelta = -10f;
        public const float GlideTime = 0.35f;
        public const float SkyHeight = 20f;
        const float SkyBack = 5f;
        const float RiseTime = 0.35f, DropTime = 0.35f, TravelMin = 0.3f, TravelMax = 0.5f;

        Vector3 _pivot, _pivotVel;
        float _clock;
        float _zoom = Tuning.CamDistance;
        float _camDist = Tuning.CamDistance;
        float _aimBlend;
        float _baseFov = Tuning.CamDefaultFov;
        bool _shakeEnabled = true;
        readonly RaycastHit[] _hits = new RaycastHit[16];

        // shake
        float _shakeAmp, _shakeDur, _shakeT;
        // focus
        Vector3 _focusPoint;
        float _focusUntil = -1f;
        // transition
        bool _sky;
        float _trT, _trTotal, _travelTime;
        Vector3 _trStartPos, _trStartPivot;
        Quaternion _trStartRot;
        float _trFinalYaw, _trFinalPitch;
        Action _onArrive;

        protected virtual void Awake()
        {
            Instance = this;
            Cam = GetComponent<Camera>();
            ConfigureCamera();
            var e = transform.eulerAngles;
            Yaw = e.y;
            float p = e.x > 180f ? e.x - 360f : e.x;
            Pitch = Mathf.Clamp(p, Tuning.CamPitchMin, Tuning.CamPitchMax);
        }

        protected virtual void OnDestroy() { RestoreAllBodies(); if (Instance == this) { Instance = null; MainCameraSuppressed = false; } }

        void OnEnable() { GameEvents.SettingsChanged += OnSettingsChanged; OnSettingsChanged(); }
        void OnDisable() { GameEvents.SettingsChanged -= OnSettingsChanged; RestoreAllBodies(); }

        void ConfigureCamera()
        {
            Cam.nearClipPlane = 0.1f;
            Cam.farClipPlane = 500f;
            var data = Cam.GetUniversalAdditionalCameraData();
            if (data != null) data.renderPostProcessing = true;
            var mine = GetComponent<AudioListener>();
            if (mine == null) mine = gameObject.AddComponent<AudioListener>();
            mine.enabled = true;
            foreach (var l in FindObjectsByType<AudioListener>()) if (l != mine && l.enabled) l.enabled = false;
            Cam.fieldOfView = _baseFov;
        }

        void OnSettingsChanged()
        {
            var s = GameSession.Instance != null ? GameSession.Instance.Settings : null;
            _baseFov = s != null ? Mathf.Clamp(s.Fov, 40f, 110f) : Tuning.CamDefaultFov;
            _shakeEnabled = s == null || s.ScreenShake;
            if (!_shakeEnabled) _shakeAmp = 0f;
        }

        // ---------------------------------------------------------------- API
        /// <summary>Snap or smoothly retarget (no GTA transition).</summary>
        public virtual void SetTarget(ShifterCharacter c, bool instant)
        {
            if (IsTransitioning) FinishTransition(invokeCallback: true);
            Target = c;
            if (c == null || !instant) return;
            _pivot = PivotOf(c);
            _pivotVel = Vector3.zero;
            Yaw = c.transform.eulerAngles.y;
            Pitch = 15f;
            _camDist = CollisionDistance(_pivot, -(Quaternion.Euler(Pitch, Yaw, 0f) * Vector3.forward), _zoom);
            ApplyFollowPose();
        }

        /// <summary>Character-switch transition. Calls onArrive when control should be handed over.</summary>
        public virtual void TransitionTo(ShifterCharacter c, Action onArrive = null)
        {
            if (c == null) { onArrive?.Invoke(); return; }
            if (IsTransitioning) FinishTransition(invokeCallback: true);
            if (Target == null) { SetTarget(c, true); onArrive?.Invoke(); return; }

            var settings = GameSession.Instance != null ? GameSession.Instance.Settings : null;
            bool cinematic = settings == null || settings.CinematicSwitch;
            Vector3 newPivot = PivotOf(c);
            float dist = Vector3.Distance(_pivot, newPivot);
            Target = c;
            _sky = cinematic && dist >= Tuning.SwitchLongDistance;
            _trT = 0f;
            _trStartPos = transform.position;
            _trStartRot = transform.rotation;
            _trStartPivot = _pivot;
            _travelTime = Mathf.Lerp(TravelMin, TravelMax, Mathf.Clamp01((dist - Tuning.SwitchLongDistance) / 50f));
            _trTotal = _sky ? RiseTime + _travelTime + DropTime : GlideTime;
            if (_sky)
            {
                // drop in behind the new character
                _trFinalYaw = c.transform.eulerAngles.y;
                _trFinalPitch = 15f;
            }
            else
            {
                _trFinalYaw = Yaw;
                _trFinalPitch = Pitch;
            }
            LastTransitionWasSky = _sky;
            LastTransitionDuration = _trTotal;
            _onArrive = onArrive;
            IsTransitioning = true;
            Sfx.Play(SfxId.SwitchWhoosh);
        }

        public virtual void Shake(float amplitude, float duration)
        {
            if (!_shakeEnabled || amplitude <= 0f || duration <= 0f) return;
            float remaining = _shakeDur > 0f ? _shakeAmp * (1f - Mathf.Clamp01(_shakeT / _shakeDur)) : 0f;
            _shakeAmp = Mathf.Max(amplitude, remaining);
            _shakeDur = duration;
            _shakeT = 0f;
        }

        /// <summary>Look at a point for a moment (e.g. a door that just opened). Returns control after duration.</summary>
        public virtual void FocusOn(Vector3 point, float duration)
        {
            _focusPoint = point;
            _focusUntil = _clock + Mathf.Max(0.1f, duration);
        }

        // ---------------------------------------------------------------- update
        void LateUpdate()
        {
            float dt = Mathf.Min(UnscaledDeltaTime, 0.1f);
            _clock += dt;
            if (IsTransitioning) UpdateTransition(dt);
            else if (Target != null) UpdateFollow(dt);
            UpdateFov(dt);
            ApplyShake(dt);
            UpdateBodyFades();
        }

        void UpdateFollow(float dt)
        {
            _pivot = Vector3.SmoothDamp(_pivot, PivotOf(Target), ref _pivotVel, FollowSmoothTime, Mathf.Infinity, dt);

            bool input = InputEnabled && ShiftersInput.Context == InputContext.Gameplay;
            if (_clock < _focusUntil)
            {
                Vector3 to = _focusPoint - _pivot;
                if (to.sqrMagnitude > 0.01f)
                {
                    float wantYaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
                    float wantPitch = Mathf.Clamp(-Mathf.Atan2(to.y, new Vector2(to.x, to.z).magnitude) * Mathf.Rad2Deg + 8f, Tuning.CamPitchMin, Tuning.CamPitchMax);
                    float k = 1f - Mathf.Exp(-6f * dt);
                    Yaw = Mathf.LerpAngle(Yaw, wantYaw, k);
                    Pitch = Mathf.Lerp(Pitch, wantPitch, k);
                }
            }
            else if (input)
            {
                Vector2 look = ShiftersInput.Look;
                Yaw += look.x;
                Pitch = Mathf.Clamp(Pitch - look.y, Tuning.CamPitchMin, Tuning.CamPitchMax);
                float z = ShiftersInput.Zoom;
                if (Mathf.Abs(z) > 0.001f) _zoom = Mathf.Clamp(_zoom - z * 0.75f, Tuning.CamMinDistance, Tuning.CamMaxDistance);
            }
            Yaw = Mathf.Repeat(Yaw + 180f, 360f) - 180f;

            bool aiming = IsAiming || (Target.Weapon != null && Target.Weapon.IsAiming);
            _aimBlend = Mathf.MoveTowards(_aimBlend, aiming ? 1f : 0f, dt * 6f);
            ApplyFollowPose(dt);
        }

        void ApplyFollowPose(float dt = -1f)
        {
            float wantDist = Mathf.Lerp(_zoom, Tuning.CamAimDistance, _aimBlend);
            Quaternion rot = Quaternion.Euler(Pitch, Yaw, 0f);
            Vector3 back = -(rot * Vector3.forward);
            float tight = TightTarget(_pivot, rot, back, wantDist);
            _tight = dt < 0f ? tight : Mathf.MoveTowards(_tight, tight, dt * TightRate);
            Vector3 origin = FollowOrigin(_pivot, rot, _tight, _tightSide);
            float allowed = CollisionDistance(origin, back, wantDist);
            if (dt < 0f || allowed < _camDist) _camDist = allowed;                         // pull in fast (never clip)
            else _camDist = Mathf.Lerp(_camDist, allowed, 1f - Mathf.Exp(-4f * dt));     // ease back out
            transform.SetPositionAndRotation(origin + back * _camDist, rot);
        }

        // ---------------------------------------------------------------- tight-spot framing
        /// <summary>Tight spots: when a wall or prop behind the pivot leaves less than TightFrom metres of boom, the
        /// orbit origin slides over a shoulder (right preferred; the left one when the right is more blocked; ShoulderOffset
        /// x body width) and rises up to TightLift, fully at TightFull, so the active body stops covering the middle of the view. Open space (boom
        /// not blocked) and aiming are unchanged; rotation is never touched, so look/aim/throw directions stay identical.</summary>
        public const float TightFrom = 3.2f, TightFull = 1.4f, TightLift = 0.35f;
        const float TightRate = 2.5f;
        float _tight, _tightSide = 1f;
        /// <summary>0..1 tight-spot framing blend currently applied (diagnostics/tests).</summary>
        public float TightBlend => _tight;

        float TightTarget(Vector3 pivot, Quaternion rot, Vector3 back, float wantDist)
        {
            float centered = CollisionDistance(pivot, back, wantDist);
            if (centered >= wantDist - 0.05f) return 0f;
            float k = Mathf.Clamp01((TightFrom - centered) / (TightFrom - TightFull)) * (1f - _aimBlend);
            // never trade boom length for the offset (a prop behind one shoulder): try the current side, then the other,
            // halving the blend, until the offset origin keeps at least the centered boom. The side only flips once the
            // applied blend has eased back to ~0, so the camera never jumps across the body.
            float side0 = _tightSide;
            for (int i = 0; i < 3 && k > 0.001f; i++, k *= 0.5f)
                for (int s = 0; s < 2; s++)
                {
                    float side = s == 0 ? side0 : -side0;
                    if (CollisionDistance(FollowOrigin(pivot, rot, k, side), back, wantDist) < centered - 0.1f) continue;
                    if (side == _tightSide) return k;
                    if (_tight < 0.02f) { _tightSide = side; return k; }
                    return 0f;   // ease out on this side first
                }
            return 0f;
        }

        /// <summary>Orbit origin: pivot + over-the-shoulder offset (aiming or tight spot) + tight-spot lift, each limited by geometry.</summary>
        Vector3 FollowOrigin(Vector3 pivot, Quaternion rot, float tight, float tightSide)
        {
            Vector3 o = pivot;
            // aiming is always over the right shoulder; the tight-spot offset uses its own side (continuous blend between them)
            float aimSide = ShoulderOffset * _aimBlend, tSide = ShoulderOffset * tight * BodyWidthScale(Target);
            float side = tightSide > 0f ? Mathf.Max(aimSide, tSide) : aimSide - tSide;
            if (Mathf.Abs(side) > 0.001f)
            {
                Vector3 dir = rot * Vector3.right * Mathf.Sign(side);
                o += dir * FreeDistance(o, dir, Mathf.Abs(side));
            }
            float lift = TightLift * tight;
            if (lift > 0.001f) o += Vector3.up * FreeDistance(o, Vector3.up, lift);
            return o;
        }

        static float BodyWidthScale(ShifterCharacter c) => c != null && c.Capsule != null ? Mathf.Clamp(c.Capsule.radius / 0.42f, 1f, 1.3f) : 1f;

        void UpdateFov(float dt)
        {
            float target = _baseFov + AimFovDelta * _aimBlend;
            Cam.fieldOfView = Mathf.Lerp(Cam.fieldOfView, target, 1f - Mathf.Exp(-10f * dt));
        }

        void ApplyShake(float dt)
        {
            if (_shakeAmp <= 0f || _shakeDur <= 0f) return;
            _shakeT += dt;
            float k = 1f - Mathf.Clamp01(_shakeT / _shakeDur);
            if (k <= 0f) { _shakeAmp = 0f; _shakeDur = 0f; return; }
            float t = _clock * 22f;
            float a = _shakeAmp * k * k;
            float x = (Mathf.PerlinNoise(t, 0.37f) - 0.5f) * 2f * a;
            float y = (Mathf.PerlinNoise(0.71f, t) - 0.5f) * 2f * a;
            float roll = (Mathf.PerlinNoise(t, t * 0.5f) - 0.5f) * 4f * a;
            transform.position += transform.right * x + transform.up * y;
            transform.rotation *= Quaternion.Euler(0f, 0f, roll);
        }

        // ---------------------------------------------------------------- body fade (tight spots)
        /// <summary>Camera-to-body-surface distances (m, capsule surface): a team member's model switches to shadows-only
        /// (renderers stay enabled, shadow kept) when the rendered camera is closer than Near, and comes back beyond Far
        /// (hysteresis), so a tight spot never fills the view with (or puts the camera inside) a body. Self = the camera's
        /// Target, Mate = any other team member. Presentation only: colliders, physics, AI and bots are untouched.</summary>
        public const float SelfHideNear = 0.55f, SelfHideFar = 0.65f;
        public const float MateHideNear = 0.9f, MateHideFar = 1.1f;

        sealed class BodyFade { public ShifterCharacter C; public Renderer[] R; public ShadowCastingMode[] M; }
        readonly List<BodyFade> _fades = new List<BodyFade>(4);

        /// <summary>True while the camera has c's model switched to shadows-only (camera inside / right against it).</summary>
        public static bool IsBodyFaded(ShifterCharacter c) => Instance != null && Instance.FadeIndex(c) >= 0;

        int FadeIndex(ShifterCharacter c)
        {
            if (c == null) return -1;
            for (int i = 0; i < _fades.Count; i++) if (_fades[i].C == c) return i;
            return -1;
        }

        void UpdateBodyFades()
        {
            var cm = CharacterManager.Instance;
            bool live = cm != null && Cam != null && Cam.enabled && !MainCameraSuppressed;
            Vector3 eye = transform.position;
            for (int i = _fades.Count - 1; i >= 0; i--)
            {
                var f = _fades[i];
                if (live && f.C != null && !f.C.IsHidden && TooClose(f.C, eye, true)) continue;
                RestoreBody(f);
                _fades.RemoveAt(i);
            }
            if (!live) return;
            var team = cm.Team;
            for (int i = 0; i < team.Count; i++)
            {
                var c = team[i];
                if (c == null || c.IsHidden || c.Visual == null || FadeIndex(c) >= 0 || !TooClose(c, eye, false)) continue;
                var r = c.Visual.Renderers;
                if (r == null || r.Length == 0) continue;
                var f = new BodyFade { C = c, R = r, M = new ShadowCastingMode[r.Length] };
                for (int k = 0; k < r.Length; k++)
                {
                    if (r[k] == null) continue;
                    f.M[k] = r[k].shadowCastingMode;
                    r[k].shadowCastingMode = ShadowCastingMode.ShadowsOnly;
                }
                _fades.Add(f);
            }
        }

        bool TooClose(ShifterCharacter c, Vector3 eye, bool faded)
        {
            bool self = c == Target;
            float lim = self ? (faded ? SelfHideFar : SelfHideNear) : (faded ? MateHideFar : MateHideNear);
            return BodySurfaceDistance(c, eye) < lim * BodyWidthScale(c);   // wide bodies (Brute) reach past their capsule
        }

        /// <summary>Distance from p to the surface of c's capsule (negative = inside).</summary>
        public static float BodySurfaceDistance(ShifterCharacter c, Vector3 p)
        {
            var cap = c.Capsule;
            float r = cap != null ? cap.radius : 0.4f;
            float h = cap != null ? cap.height : c.Height;
            Vector3 center = cap != null ? cap.center : new Vector3(0f, h * 0.5f, 0f);
            float half = Mathf.Max(0f, h * 0.5f - r);
            Vector3 a = c.transform.TransformPoint(center - Vector3.up * half), b = c.transform.TransformPoint(center + Vector3.up * half);
            Vector3 ab = b - a;
            float t = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            return Vector3.Distance(p, a + ab * t) - r;
        }

        static void RestoreBody(BodyFade f)
        {
            for (int k = 0; k < f.R.Length; k++) if (f.R[k] != null) f.R[k].shadowCastingMode = f.M[k];
        }

        void RestoreAllBodies()
        {
            for (int i = 0; i < _fades.Count; i++) RestoreBody(_fades[i]);
            _fades.Clear();
        }

        // ---------------------------------------------------------------- transition
        void UpdateTransition(float dt)
        {
            if (Target == null) { FinishTransition(true); return; }
            _trT += dt;
            Vector3 pivotNow = PivotOf(Target);
            Quaternion finalRot = Quaternion.Euler(_trFinalPitch, _trFinalYaw, 0f);
            Vector3 back = -(finalRot * Vector3.forward);
            _tight = TightTarget(pivotNow, finalRot, back, _zoom);
            Vector3 finalOrigin = FollowOrigin(pivotNow, finalRot, _tight, _tightSide);
            Vector3 finalPos = finalOrigin + back * CollisionDistance(finalOrigin, back, _zoom);

            Vector3 pos; Quaternion rot;
            if (!_sky)
            {
                float e = EaseInOut(_trT / _trTotal);
                Vector3 pv = Vector3.Lerp(_trStartPivot, pivotNow, e);
                Quaternion r = Quaternion.Slerp(_trStartRot, finalRot, e);
                Vector3 b = -(r * Vector3.forward);
                // boom length along each end's back axis + the off-axis part (shoulder/lift), both blended
                Vector3 b0 = -(_trStartRot * Vector3.forward), s0 = _trStartPos - _trStartPivot, s1 = finalPos - pivotNow;
                float d0 = Vector3.Dot(s0, b0), d1 = Vector3.Dot(s1, back);
                pos = pv + b * Mathf.Lerp(d0, d1, e) + Vector3.Lerp(s0 - b0 * d0, s1 - back * d1, e);
                rot = r;
            }
            else
            {
                Vector3 startFlat = Flat(_trStartRot * Vector3.forward);
                Vector3 endFlat = Flat(finalRot * Vector3.forward);
                Vector3 skyA = _trStartPivot + Vector3.up * SkyHeight - startFlat * SkyBack;
                Vector3 skyB = pivotNow + Vector3.up * SkyHeight - endFlat * SkyBack;
                Quaternion rotA = Quaternion.LookRotation((_trStartPivot - skyA).normalized, startFlat);
                Quaternion rotB = Quaternion.LookRotation((pivotNow - skyB).normalized, endFlat);
                if (_trT < RiseTime)
                {
                    float e = EaseInOut(_trT / RiseTime);
                    pos = Vector3.Lerp(_trStartPos, skyA, e);
                    rot = Quaternion.Slerp(_trStartRot, rotA, e);
                }
                else if (_trT < RiseTime + _travelTime)
                {
                    float e = EaseInOut((_trT - RiseTime) / _travelTime);
                    pos = Vector3.Lerp(skyA, skyB, e) + Vector3.up * (Mathf.Sin(e * Mathf.PI) * 3f);
                    rot = Quaternion.Slerp(rotA, rotB, e);
                }
                else
                {
                    float e = EaseInOut((_trT - RiseTime - _travelTime) / DropTime);
                    pos = Vector3.Lerp(skyB, finalPos, e);
                    rot = Quaternion.Slerp(rotB, finalRot, e);
                }
            }
            transform.SetPositionAndRotation(pos, rot);
            if (_trT >= _trTotal) FinishTransition(true);
        }

        void FinishTransition(bool invokeCallback)
        {
            if (!IsTransitioning) return;
            IsTransitioning = false;
            Yaw = _trFinalYaw;
            Pitch = _trFinalPitch;
            if (Target != null)
            {
                _pivot = PivotOf(Target);
                _pivotVel = Vector3.zero;
                ApplyFollowPose();
            }
            Sfx.Play(SfxId.SwitchLand);
            var cb = _onArrive;
            _onArrive = null;
            if (invokeCallback) cb?.Invoke();
        }

        // ---------------------------------------------------------------- helpers
        /// <summary>Orbit pivot for a character: CameraTarget when upright, else above the capsule center (tumble/bridge safe).</summary>
        public static Vector3 PivotOf(ShifterCharacter c)
        {
            if (c == null) return Vector3.zero;
            bool upright = c.Motor == null || (!c.Motor.IsTumbling && !c.Motor.IsGettingUp);
            var cond = c.Condition;
            if (upright && c.CameraTarget != null && (cond == CharacterCondition.Normal || cond == CharacterCondition.Interacting || cond == CharacterCondition.Seated))
                return c.CameraTarget.position;
            Vector3 center = c.Capsule != null ? c.transform.TransformPoint(c.Capsule.center) : c.Center;
            return center + Vector3.up * (c.Height * 0.32f);
        }

        /// <summary>Free distance along dir from origin for the camera sphere (ignores triggers, characters, held objects).</summary>
        public float CollisionDistance(Vector3 origin, Vector3 dir, float maxDist)
        {
            if (maxDist <= 0.01f) return maxDist;
            int n = Physics.SphereCastNonAlloc(origin, CollisionRadius, dir, _hits, maxDist, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float best = maxDist;
            for (int i = 0; i < n; i++)
            {
                var h = _hits[i];
                if (h.collider == null || IgnoredByCamera(h.collider)) continue;
                if (h.distance <= 0f && h.point == Vector3.zero) { best = Mathf.Min(best, 0.3f); continue; } // pivot already inside geometry
                best = Mathf.Min(best, Mathf.Max(0.3f, h.distance - 0.05f));
            }
            return best;
        }

        /// <summary>Unclamped free distance along dir for the camera sphere (0 when already touching geometry).</summary>
        float FreeDistance(Vector3 origin, Vector3 dir, float maxDist)
        {
            if (maxDist <= 0.001f) return 0f;
            int n = Physics.SphereCastNonAlloc(origin, CollisionRadius, dir, _hits, maxDist, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float best = maxDist;
            for (int i = 0; i < n; i++)
            {
                var h = _hits[i];
                if (h.collider == null || IgnoredByCamera(h.collider)) continue;
                if (h.distance <= 0f && h.point == Vector3.zero) return 0f;
                best = Mathf.Min(best, Mathf.Max(0f, h.distance - 0.05f));
            }
            return best;
        }

        static bool IgnoredByCamera(Collider col)
        {
            if (col.GetComponentInParent<ShifterCharacter>() != null) return true;
            var g = col.GetComponentInParent<Grabbable>();
            return g != null && g.IsHeld;
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v.sqrMagnitude > 1e-4f ? v.normalized : Vector3.forward; }
        static float EaseInOut(float t) { t = Mathf.Clamp01(t); return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f; }
    }
}
