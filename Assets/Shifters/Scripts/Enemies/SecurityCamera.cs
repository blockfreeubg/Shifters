// =====================================================================================
//  SHIFTERS - SecurityCamera.cs   [owned by the ENEMIES agent]
//  "ME-CAM": sweeping security camera (CH04/CH07/O1). Yaw = center + SweepDeg * sin(2*pi*(clock/Period + Phase))
//  (SweepDeg is the AMPLITUDE: +-SweepDeg; Phase is a fraction of the period; clock = scaled time, halts while
//  frozen). The view is the hologram floor wedge: planar distance <= Range from the camera's floor point and
//  within Fov/2 of the sweep yaw, plus a clear line of sight from the lens to the intern's centre or head
//  (characters and triggers never block; anything solid does).
//  Teamwork mode: >= 2 Alive interns within Tuning.TeamworkRadius of each other, both inside the cone,
//  for Tuning.TeamworkSpotSeconds (meter decays at 2x when broken). AnyIntern mode: any Alive intern.
//  SignalSource ON while detecting. Shot = disabled 5 s; SAY CHEESE / T-SHIRT = blinded; CAPS-LOCK = frozen sweep.
//  Wedge colors: white (idle) -> orange (spotting, meter filling) -> red (detected); grey/hidden while off.
// =====================================================================================
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Shifters
{
    [DisallowMultipleComponent]
    public class SecurityCamera : SignalSource, IShootable, IEnemyReactions, IResettable
    {
        public static readonly List<SecurityCamera> All = new List<SecurityCamera>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { All.Clear(); }

        public const float SenseInterval = 0.05f;
        public const float PredictStep = 0.05f;

        [Header("Behaviour (EnemyKit.SecurityCamera)")]
        public CameraDetect Mode = CameraDetect.Teamwork;
        [Tooltip("Sweep AMPLITUDE in degrees (yaw = center +- SweepDeg).")] public float SweepDeg = 35f;
        public float Period = 6f;
        public float Range = Tuning.CameraRange;
        [Tooltip("Full cone angle in degrees.")] public float Fov = Tuning.CameraFov;
        [Tooltip("Phase as a fraction of the period (0..1).")] public float Phase;
        public float TeamRadius = Tuning.TeamworkRadius;
        public float SpotSeconds = Tuning.TeamworkSpotSeconds;
        [Tooltip("Center yaw (world degrees). Captured from the transform at Start when NaN.")] public float CenterYaw = float.NaN;

        [Header("Parts (EnemyKit)")]
        public Transform Head;
        public Transform Lens;
        public EnemyConeVisual Cone;
        public Renderer Led;

        // ------------------------------------------------------------------ readable state
        public bool Detecting => IsOn;
        /// <summary>0..1 spot meter (1 = detected).</summary>
        public float Meter01 => _meter;
        public float SweepYaw => YawAt(_clock);
        public float Clock => _clock;
        public bool IsDisabled => _disabled > 0f;
        public bool IsBlind => _blind > 0f;
        public bool IsPaused => _pause > 0f;
        public bool IsFrozen => _frozen > 0f;
        /// <summary>Disabled, blinded or paused: sees nothing.</summary>
        public bool IsInactive => _disabled > 0f || _blind > 0f || _pause > 0f || !isActiveAndEnabled;
        public float InactiveRemaining => Mathf.Max(_disabled, Mathf.Max(_blind, _pause));
        public int DetectionCount { get; private set; }
        public ShifterCharacter SpottedA { get; private set; }
        public ShifterCharacter SpottedB { get; private set; }
        public Vector3 EyePosition => Lens != null ? Lens.position : transform.position;
        /// <summary>World point on the floor under the camera (apex of the floor wedge).</summary>
        public Vector3 FloorPoint => _floor;

        public event Action<SecurityCamera> Detected;

        float _clock, _meter, _senseTimer, _disabled, _blind, _pause, _frozen, _beep;
        bool _started;
        Vector3 _floor;
        static readonly RaycastHit[] _hits = new RaycastHit[12];

        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() { All.Remove(this); if (IsOn) SetOn(false); }

        void Start()
        {
            if (float.IsNaN(CenterYaw)) CenterYaw = transform.eulerAngles.y;
            _clock = 0f;
            _floor = FindFloor();
            if (Cone != null) Cone.transform.position = _floor + Vector3.up * 0.03f;
            _started = true;
            ApplyPose();
        }

        Vector3 FindFloor()
        {
            Vector3 o = transform.position + Vector3.down * 0.45f;
            int n = Physics.RaycastNonAlloc(o, Vector3.down, _hits, 40f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue; Vector3 p = new Vector3(o.x, (transform.parent != null ? transform.parent.position.y : 0f), o.z);
            for (int i = 0; i < n; i++)
            {
                var h = _hits[i];
                if (h.collider == null || h.collider.transform.IsChildOf(transform)) continue;
                if (h.collider.GetComponentInParent<ShifterCharacter>() != null) continue;
                if (h.distance < best) { best = h.distance; p = h.point; }
            }
            return p;
        }

        // ------------------------------------------------------------------ geometry API (bots, levels, HUD chips)
        public float YawAt(float clock)
        {
            float c = float.IsNaN(CenterYaw) ? transform.eulerAngles.y : CenterYaw;
            if (Period <= 0.01f || Mathf.Abs(SweepDeg) < 0.01f) return c;
            return c + SweepDeg * Mathf.Sin(2f * Mathf.PI * (clock / Period + Phase));
        }

        /// <summary>Is the floor position p inside the wedge at the given yaw (range + angle only, no line of sight)?</summary>
        public bool InWedge(Vector3 p, float yaw)
        {
            Vector3 apex = _started ? _floor : transform.position;
            Vector3 d = p - apex; d.y = 0f;
            float m = d.magnitude;
            if (m > Range) return false;
            if (m < 0.6f) return true;
            Vector3 f = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            return Vector3.Angle(f, d) <= Fov * 0.5f;
        }

        /// <summary>Clear line of sight from the lens to a world point (characters, stalkers, triggers ignored).</summary>
        public bool LineOfSight(Vector3 p)
        {
            Vector3 a = EyePosition, dir = p - a;
            float dist = dir.magnitude;
            if (dist < 0.05f) return true;
            dir /= dist;
            int n = Physics.RaycastNonAlloc(a, dir, _hits, dist - 0.05f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var col = _hits[i].collider;
                if (col == null || col.transform.IsChildOf(transform)) continue;
                if (col.GetComponentInParent<ShifterCharacter>() != null) continue;
                return false;
            }
            return true;
        }

        /// <summary>Visible right now: active, in the wedge at the current yaw, LOS to p+1.0 or p+1.6 (p = feet).</summary>
        public bool Sees(Vector3 feet) => !IsInactive && InWedge(feet, SweepYaw) && FeetVisible(feet);

        bool FeetVisible(Vector3 feet) => LineOfSight(feet + Vector3.up * 1.0f) || LineOfSight(feet + Vector3.up * 1.6f);

        /// <summary>Can the camera see this character right now (ignores Alive)?</summary>
        public bool SeesCharacter(ShifterCharacter c)
        {
            if (c == null || IsInactive) return false;
            if (!InWedge(c.Feet, SweepYaw)) return false;
            return LineOfSight(c.Center) || LineOfSight(c.HeadTop - Vector3.up * 0.12f);
        }

        /// <summary>Could the camera see the feet position p at any moment in [now, now + horizon] (assumes no new
        /// disables; current inactive time is skipped)? Bots: wait while true.</summary>
        public bool CouldSee(Vector3 p, float horizon)
        {
            if (!FeetVisible(p)) return false;
            float skip = InactiveRemaining;
            for (float t = 0f; t <= horizon + 1e-4f; t += PredictStep)
            {
                if (t < skip) continue;
                float clk = _clock + (IsFrozen ? 0f : t);
                if (InWedge(p, YawAt(clk))) return true;
            }
            return false;
        }

        /// <summary>Could a and b be seen TOGETHER (same moment) within horizon? In Teamwork mode a pair farther apart
        /// than TeamRadius is never teamwork (false).</summary>
        public bool CouldSeePair(Vector3 a, Vector3 b, float horizon)
        {
            if (Mode == CameraDetect.Teamwork && Vector3.Distance(a, b) > TeamRadius) return false;
            if (!FeetVisible(a) || !FeetVisible(b)) return false;
            float skip = InactiveRemaining;
            for (float t = 0f; t <= horizon + 1e-4f; t += PredictStep)
            {
                if (t < skip) continue;
                float yaw = YawAt(_clock + (IsFrozen ? 0f : t));
                if (InWedge(a, yaw) && InWedge(b, yaw)) return true;
            }
            return false;
        }

        /// <summary>Seconds until p is (or could be) inside the wedge (0 = now; > max = never within max).</summary>
        public float SecondsUntilSees(Vector3 p, float max = 10f)
        {
            if (!FeetVisible(p)) return float.PositiveInfinity;
            float skip = InactiveRemaining;
            for (float t = 0f; t <= max; t += PredictStep)
                if (t >= skip && InWedge(p, YawAt(_clock + (IsFrozen ? 0f : t)))) return t;
            return float.PositiveInfinity;
        }

        /// <summary>Seconds until p leaves the wedge (0 = not seen now). SPD "SWEEP AWAY IN 1.4 s" chips.</summary>
        public float SecondsUntilClear(Vector3 p, float max = 10f)
        {
            if (IsInactive || !FeetVisible(p)) return 0f;
            for (float t = 0f; t <= max; t += PredictStep)
                if (!InWedge(p, YawAt(_clock + (IsFrozen ? 0f : t)))) return t;
            return max;
        }

        // ------------------------------------------------------------------ reactions
        /// <summary>Knocked out for seconds (shot): no view, sparks, wedge hidden.</summary>
        public void Disable(float seconds)
        {
            if (seconds <= 0f) return;
            bool was = _disabled > 0f;
            _disabled = Mathf.Max(_disabled, seconds);
            ClearDetection();
            if (!was)
            {
                Fx.Sparks(EyePosition, Vector3.down, 16, UIColors.AccentYellow);
                Fx.FloatingText(EyePosition + Vector3.up * 0.6f, "BZZT! OFFLINE " + seconds.ToString("0") + " S", UIColors.AccentYellow, 1.1f);
                Sfx.At(SfxId.EnemyStun, EyePosition, 0.8f, 1.3f);
            }
        }

        public void Blind(float seconds, string label = null)
        {
            if (seconds <= 0f) return;
            bool was = _blind > 0f;
            _blind = Mathf.Max(_blind, seconds);
            ClearDetection();
            if (!was) Fx.FloatingText(EyePosition + Vector3.up * 0.6f, label ?? "BLINDED!", UIColors.AccentYellow, 1.1f);
        }

        /// <summary>Silent pause (after an escort): sees nothing for seconds.</summary>
        public void Pause(float seconds) { if (seconds > 0f) { _pause = Mathf.Max(_pause, seconds); ClearDetection(); } }

        public void Stun(float seconds, string label = null) => Disable(seconds);
        public void Shove(Vector3 velocity, float stunSeconds = 0f) { if (stunSeconds > 0f) Disable(stunSeconds); }
        public void Lure(Vector3 point, float priority = 1f) { }

        /// <summary>CAPS-LOCK: the sweep stops where it is (the camera still watches).</summary>
        public void Freeze(float seconds)
        {
            if (seconds <= 0f) return;
            if (_frozen <= 0f) Fx.FloatingText(EyePosition + Vector3.up * 0.6f, "LOCKED", UIColors.Info, 1f);
            _frozen = Mathf.Max(_frozen, seconds);
        }

        public void OnShot(in ShotInfo shot)
        {
            switch (EnemyReact.Classify(shot))
            {
                case EnemyHit.SayCheese: Blind(EnemyReact.CheeseCameraBlindSeconds, "FLASH! BLIND 5 S"); break;
                case EnemyHit.TShirt: Blind(EnemyReact.WrapBlindSeconds, "WRAPPED!"); break;
                case EnemyHit.LeafBlower: Fx.FloatingText(EyePosition + Vector3.up * 0.6f, "*whoosh*", UIColors.TextMuted, 0.8f); break;
                case EnemyHit.ToolOnly: Fx.Sparks(shot.Point, shot.Normal, 6, UIColors.Info); break;
                default: Fx.Sparks(shot.Point, shot.Normal, 10, UIColors.AccentYellow); Disable(EnemyReact.CameraShotDisableSeconds); break;
            }
        }

        public void ResetState()
        {
            _meter = 0f; _disabled = _blind = _pause = _frozen = 0f;
            SpottedA = SpottedB = null;
            if (IsOn) SetOn(false);
        }

        void ClearDetection()
        {
            _meter = 0f; SpottedA = SpottedB = null;
            if (IsOn) SetOn(false);
        }

        // ------------------------------------------------------------------ tick
        void Update()
        {
            if (!_started) return;
            float dt = Time.deltaTime;
            if (_frozen > 0f) _frozen -= dt; else _clock += dt;
            if (_disabled > 0f) _disabled -= dt;
            if (_blind > 0f) _blind -= dt;
            if (_pause > 0f) _pause -= dt;
            ApplyPose();

            _senseTimer -= dt;
            if (_senseTimer <= 0f)
            {
                float step = SenseInterval - _senseTimer;
                _senseTimer = SenseInterval;
                Sense(step);
            }
            UpdateVisuals(dt);
        }

        void Sense(float step)
        {
            if (IsInactive) { if (_meter > 0f || IsOn) ClearDetection(); return; }
            bool seeing = Mode == CameraDetect.Teamwork ? FindPair() : FindAny();
            float rate = step / Mathf.Max(0.05f, SpotSeconds);
            _meter = Mathf.Clamp01(_meter + (seeing ? rate : -2f * rate));
            if (_meter >= 1f && !IsOn)
            {
                SetOn(true);
                DetectionCount++;
                Sfx.At(SfxId.CameraSpotted, EyePosition, 1f);
                Fx.FloatingText(EyePosition + Vector3.up * 0.7f, Mode == CameraDetect.Teamwork ? "TEAMWORK DETECTED!" : "INTRUDER!", UIColors.Danger, 1.5f);
                Detected?.Invoke(this);
            }
            else if (IsOn && _meter <= 0f) SetOn(false);
        }

        bool FindAny()
        {
            var all = ShifterCharacter.All;
            for (int i = 0; i < all.Count; i++)
            {
                var c = all[i];
                if (c == null || !c.isActiveAndEnabled || !c.IsAlive) continue;
                if (SeesCharacter(c)) { SpottedA = c; SpottedB = null; return true; }
            }
            return false;
        }

        // visible flags cached per sense tick (<= 8 interns)
        static readonly bool[] _vis = new bool[8];

        bool FindPair()
        {
            var all = ShifterCharacter.All;
            int n = Mathf.Min(all.Count, _vis.Length);
            int visible = 0;
            for (int i = 0; i < n; i++)
            {
                var c = all[i];
                _vis[i] = c != null && c.isActiveAndEnabled && c.IsAlive && SeesCharacter(c);
                if (_vis[i]) visible++;
            }
            if (visible < 2) return false;
            float r2 = TeamRadius * TeamRadius;
            for (int i = 0; i < n; i++)
            {
                if (!_vis[i]) continue;
                for (int j = i + 1; j < n; j++)
                {
                    if (!_vis[j]) continue;
                    if ((all[i].Feet - all[j].Feet).sqrMagnitude <= r2) { SpottedA = all[i]; SpottedB = all[j]; return true; }
                }
            }
            return false;
        }

        void ApplyPose()
        {
            float yaw = SweepYaw;
            if (Head != null) Head.rotation = Quaternion.Euler(0f, yaw, 0f);
            if (Cone != null) Cone.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        void UpdateVisuals(float dt)
        {
            if (Cone != null)
            {
                bool off = _disabled > 0f || _blind > 0f;
                Cone.SetVisible(!off || Mathf.Repeat(Time.time * 3f, 1f) < 0.15f);
                Color col;
                if (off || _pause > 0f) col = EnemyConeVisual.Off;
                else if (IsOn) col = EnemyConeVisual.Alert;
                else if (_meter > 0f) col = Color.Lerp(EnemyConeVisual.Idle, EnemyConeVisual.Spotting, 0.45f + 0.55f * _meter);
                else col = EnemyConeVisual.Idle;
                Cone.SetColor(col);
            }
            if (Led != null) Led.enabled = !(_disabled > 0f) && (IsOn || _meter > 0f || Mathf.Repeat(Time.time, 1.5f) < 0.75f);
            if (_meter > 0f && !IsOn && !IsInactive)
            {
                _beep -= dt;
                if (_beep <= 0f) { _beep = 0.14f; Sfx.At(SfxId.CameraBeep, EyePosition, 0.5f, 1f + _meter * 0.8f); }
            }
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = IsOn ? Color.red : Color.white;
            Vector3 apex = _started ? _floor : transform.position;
            float yaw = Application.isPlaying ? SweepYaw : transform.eulerAngles.y;
            Vector3 l = Quaternion.Euler(0f, yaw - Fov * 0.5f, 0f) * Vector3.forward * Range;
            Vector3 r = Quaternion.Euler(0f, yaw + Fov * 0.5f, 0f) * Vector3.forward * Range;
            Gizmos.DrawLine(apex, apex + l); Gizmos.DrawLine(apex, apex + r); Gizmos.DrawLine(apex + l, apex + r);
        }
    }
}
