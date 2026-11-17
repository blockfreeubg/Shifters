// =====================================================================================
//  SHIFTERS - LevelExit.cs   [owned by the LEVEL agent]
//  Elevator cab with a huge orange EXIT sign, a zone inside the cab and an "[E] Leave without
//  them" console. Shows "EXIT: 2/3 PRESENT" toasts while the active character is inside.
//  Completes when ALL Alive characters are inside (Downed teammates carried in count as saved
//  and get revived): doors close, the team celebrates, 1.5 s later the LevelResult is sent to
//  GameSession.CompleteLevel + GameEvents.RaiseLevelCompleted. Build with LevelKit.Exit.
// =====================================================================================
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Shifters
{
    public class LevelExit : MonoBehaviour
    {
        public static readonly List<LevelExit> All = new List<LevelExit>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { All.Clear(); GlobalPrecondition = null; }

        /// <summary>Campaign seam: returns null when this exit may complete, else the refusal toast shown while the
        /// team waits inside (e.g. LevelKit.ExitWeightLimit, "BOSS FIRST"). Null delegate = always OK.</summary>
        public Func<string> Precondition;
        /// <summary>Same as <see cref="Precondition"/> for every exit of the scene (checked after the instance one). Reset per scene by whoever sets it; also at SubsystemRegistration.</summary>
        public static Func<string> GlobalPrecondition;
        /// <summary>Times the exit refused to complete because of a precondition (tests).</summary>
        public int RefusalCount { get; private set; }
        public string LastRefusal { get; private set; }
        /// <summary>Time.unscaledTime of the last refusal (-1 = never).</summary>
        public float LastRefusalTime { get; private set; } = -1f;
        float _nextRefusalToast;

        /// <summary>Null when the exit may complete, else the refusal text (instance precondition, then the global one).</summary>
        public string CheckPrecondition()
        {
            string r = null;
            try { r = Precondition?.Invoke(); if (string.IsNullOrEmpty(r)) r = GlobalPrecondition?.Invoke(); }
            catch (Exception e) { Debug.LogException(e); r = null; }
            return string.IsNullOrEmpty(r) ? null : r;
        }

        bool Refuse()
        {
            string r = CheckPrecondition();
            if (r == null) return false;
            RefusalCount++;
            LastRefusal = r;
            LastRefusalTime = Time.unscaledTime;
            if (Time.unscaledTime >= _nextRefusalToast)
            {
                _nextRefusalToast = Time.unscaledTime + 3f;
                GameEvents.RaiseToast(r, UIColors.Danger);
                Sfx.At(SfxId.Denied, transform.position + Vector3.up * 2f, 0.8f);
            }
            return true;
        }

        public Vector3 ZoneCenter = new Vector3(0f, 2f, 2.1f);
        public Vector3 ZoneSize = new Vector3(4f, 4f, 3.6f);
        public Transform DoorLeft, DoorRight;
        [Tooltip("How far each door panel slides sideways when open.")] public float DoorOpenOffset = 1.55f;
        public ExitConsole Console;
        public float CompleteDelay = 1.5f;
        [Tooltip("Seconds all alive characters must stay inside before the doors close.")] public float SettleTime = 0.35f;

        public bool IsCompleting { get; private set; }
        public bool IsCompleted { get; private set; }
        /// <summary>Alive characters inside / alive characters total.</summary>
        public int AlivePresent { get; private set; }
        public int AliveTotal { get; private set; }
        public int DownedPresent { get; private set; }
        public LevelResult LastResult { get; private set; }

        readonly List<ShifterCharacter> _inside = new List<ShifterCharacter>(3);
        readonly List<string> _missing = new List<string>(3);
        float _poll, _allInside, _doorT;
        int _shownPresent = -1, _shownTotal = -1;
        bool _activeInside;

        static readonly string[] VictoryLines = { "WE DID IT!", "Going up!", "Is there a gift shop?", "Best. Shift. Ever.", "Can we get pizza now?", "End of shift, baby!", "Clocking out!" };

        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() { All.Remove(this); }

        public bool Contains(ShifterCharacter c) => ZoneMath.ContainsCharacter(transform, ZoneCenter, ZoneSize, c);

        /// <summary>Display names of Alive characters that are NOT inside (who "Leave without them" abandons).</summary>
        public List<string> MissingAliveNames()
        {
            _missing.Clear();
            var cm = CharacterManager.Instance;
            if (cm == null) return _missing;
            for (int i = 0; i < cm.Team.Count; i++)
            {
                var c = cm.Team[i];
                if (c != null && c.IsAlive && !Contains(c)) _missing.Add(c.DisplayName);
            }
            return _missing;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            AnimateDoors(dt);
            if (IsCompleting) return;
            _poll -= dt;
            if (_poll > 0f) return;
            _poll = 0.1f;
            Recount();

            var cm = CharacterManager.Instance;
            var active = cm != null ? cm.Active : null;
            bool activeInside = active != null && Contains(active);
            if (activeInside && (!_activeInside || AlivePresent != _shownPresent || AliveTotal != _shownTotal) && AlivePresent < AliveTotal)
            {
                _shownPresent = AlivePresent;
                _shownTotal = AliveTotal;
                GameEvents.RaiseToast("EXIT: " + AlivePresent + "/" + AliveTotal + " PRESENT", UIColors.AccentYellow);
            }
            if (!activeInside) { _shownPresent = -1; _shownTotal = -1; }
            _activeInside = activeInside;

            if (AliveTotal > 0 && AlivePresent >= AliveTotal)
            {
                _allInside += 0.1f;
                if (_allInside >= SettleTime && !Refuse()) Complete(false);
            }
            else _allInside = 0f;
        }

        void Recount()
        {
            int present = 0, total = 0, downed = 0;
            var cm = CharacterManager.Instance;
            if (cm != null)
            {
                for (int i = 0; i < cm.Team.Count; i++)
                {
                    var c = cm.Team[i];
                    if (c == null) continue;
                    bool inside = c.isActiveAndEnabled && Contains(c);
                    if (c.IsAlive) { total++; if (inside) present++; }
                    else if (c.State == CharacterState.Downed && inside) downed++;
                }
            }
            AlivePresent = present;
            AliveTotal = total;
            DownedPresent = downed;
        }

        /// <summary>Console confirm: finish with whoever is inside right now.</summary>
        public void LeaveWithoutThem() { if (!IsCompleting && !Refuse()) Complete(true); }

        void Complete(bool abandoning)
        {
            if (IsCompleting) return;
            if (LevelManager.Instance != null && LevelManager.Instance.IsCompleted) return;
            IsCompleting = true;
            StartCoroutine(CompleteRoutine(abandoning));
        }

        IEnumerator CompleteRoutine(bool abandoning)
        {
            var cm = CharacterManager.Instance;
            _inside.Clear();
            var left = new List<string>(3);
            if (cm != null)
            {
                for (int i = 0; i < cm.Team.Count; i++)
                {
                    var c = cm.Team[i];
                    if (c == null) continue;
                    bool inside = c.isActiveAndEnabled && Contains(c) && (c.IsAlive || c.State == CharacterState.Downed);
                    if (inside) _inside.Add(c); else left.Add(c.DisplayName);
                }
            }
            if (LevelManager.Instance != null) LevelManager.Instance.MarkCompleted();
            Sfx.At(SfxId.DoorClose, transform.position + Vector3.up * 2f);
            Sfx.At(SfxId.ElevatorHum, transform.position + Vector3.up * 2f, 0.8f);
            for (int i = 0; i < _inside.Count; i++)
            {
                var c = _inside[i];
                if (c.State == CharacterState.Downed)
                {
                    if (c.Grabbable != null && c.Grabbable.IsHeld && c.Grabbable.Holder != null) c.Grabbable.Holder.ForceRelease();
                    c.Revive();
                    CheckpointManager.CountRevive();
                }
                c.ExternalControl = true; // stay in the cab and party
                if (c.Motor != null) c.Motor.ClearCommand();
                if (c.Visual != null) c.Visual.Play(VisualAction.Celebrate);
                if (i == 0) c.Say(abandoning && left.Count > 0 ? "They'll understand. Eventually." : VictoryLines[UnityEngine.Random.Range(0, VictoryLines.Length)]);
                Sfx.Voice(c, VoiceLine.Victory);
            }
            Fx.Confetti(transform.TransformPoint(ZoneCenter + Vector3.up * 1.2f));

            float t = 0f;
            while (t < CompleteDelay) { t += PerspectiveStyle.UiDelta; yield return null; }

            var result = LevelManager.Instance != null
                ? LevelManager.Instance.BuildResult(_inside.Count, left.ToArray())
                : new LevelResult { LevelIndex = -1, Survivors = _inside.Count, LeftBehind = left.ToArray() };
            LastResult = result;
            IsCompleted = true;
            // weapons W5: put the team's original gun back (never save a locker tool into the profiles) before the save
            try { WeaponCustody.RestoreOriginal(); } catch (System.Exception e) { Debug.LogException(e); }
            if (GameSession.Instance != null) GameSession.Instance.CompleteLevel(result);
            GameEvents.RaiseLevelCompleted(result);
        }

        void AnimateDoors(float dt)
        {
            float target = IsCompleting ? 1f : 0f; // 1 = closed
            if (Mathf.Approximately(_doorT, target)) return;
            _doorT = Mathf.MoveTowards(_doorT, target, dt * 1.6f);
            float k = Mathf.SmoothStep(0f, 1f, _doorT);
            float off = Mathf.Lerp(DoorOpenOffset, 0f, k);
            if (DoorLeft != null) { var p = DoorLeft.localPosition; p.x = -0.8f - off; DoorLeft.localPosition = p; }
            if (DoorRight != null) { var p = DoorRight.localPosition; p.x = 0.8f + off; DoorRight.localPosition = p; }
        }

        void OnDrawGizmos() => ZoneMath.DrawGizmo(transform, ZoneCenter, ZoneSize, UIColors.Accent);
    }
}
