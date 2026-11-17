// =====================================================================================
//  SHIFTERS - EscortBack.cs   [owned by the ENEMIES agent]
//  CH04 "detected" consequence: when its AlarmLatch trips -> fade out (caption) -> every Alive intern is
//  teleported to the section's return points (slot i -> point i % n; carries are released first) -> the
//  alarm's cameras pause CameraPause s -> the alarm clears -> fade in. No capture, no damage; puzzle state
//  persists. Escorted event + EscortCount for levels/bots.
// =====================================================================================
using System;
using System.Collections;
using UnityEngine;

namespace Shifters
{
    [DisallowMultipleComponent]
    public class EscortBack : MonoBehaviour
    {
        public AlarmLatch Alarm;
        public Transform[] ReturnPoints = new Transform[0];
        public float FadeOutSeconds = 0.5f;
        public float HoldSeconds = 0.2f;
        public float FadeInSeconds = 0.5f;
        public float CameraPause = 3f;
        public bool UseFade = true;
        public string Caption = "PLEASE WAIT ALONE.";

        public int EscortCount { get; private set; }
        public bool IsEscorting { get; private set; }
        public event Action<EscortBack> Escorted;

        AlarmLatch _bound;

        void OnEnable() { Bind(); }
        void OnDisable() { Unbind(); }

        void Bind()
        {
            if (_bound == Alarm) return;
            Unbind();
            _bound = Alarm;
            if (_bound != null) _bound.OnTripped += OnTrip;
        }

        void Unbind() { if (_bound != null) _bound.OnTripped -= OnTrip; _bound = null; }

        void Update() { if (_bound != Alarm) Bind(); }

        void OnTrip(AlarmLatch a) { if (!IsEscorting && isActiveAndEnabled) StartCoroutine(Run()); }

        public void SetReturnPoints(params Transform[] points) { ReturnPoints = points ?? new Transform[0]; }

        /// <summary>Run the escort now (without an alarm, e.g. scripted).</summary>
        public void Trigger() { if (!IsEscorting && isActiveAndEnabled) StartCoroutine(Run()); }

        IEnumerator Run()
        {
            IsEscorting = true;
            if (UseFade && Application.isPlaying) yield return ScreenFader.FadeOut(FadeOutSeconds, Caption);
            TeleportTeam();
            if (Alarm != null) { Alarm.PauseCameras(CameraPause); Alarm.ResetAlarm(); }
            if (HoldSeconds > 0f) yield return new WaitForSeconds(HoldSeconds);
            if (UseFade && Application.isPlaying) yield return ScreenFader.FadeIn(FadeInSeconds);
            EscortCount++;
            IsEscorting = false;
            Escorted?.Invoke(this);
        }

        /// <summary>Teleport every Alive intern to its return point now (no fade).</summary>
        public void TeleportTeam()
        {
            var all = ShifterCharacter.All;
            int n = ReturnPoints != null ? ReturnPoints.Length : 0;
            if (n == 0) return;
            for (int i = 0; i < all.Count; i++)
            {
                var c = all[i];
                if (c == null || !c.IsAlive) continue;
                if (c.Grabber != null) c.Grabber.ForceRelease();
            }
            int k = 0;
            for (int i = 0; i < all.Count; i++)
            {
                var c = all[i];
                if (c == null || !c.IsAlive) continue;
                int slot = c.Slot >= 0 ? c.Slot : k;
                k++;
                var p = ReturnPoints[slot % n];
                if (p == null) continue;
                if (c.Motor != null) c.Motor.Teleport(p.position, p.rotation);
                else c.transform.SetPositionAndRotation(p.position, p.rotation);
            }
            Sfx.Play(SfxId.Whoosh, 0.6f);
        }
    }
}
