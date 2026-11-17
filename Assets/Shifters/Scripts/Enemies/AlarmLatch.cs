// =====================================================================================
//  SHIFTERS - AlarmLatch.cs   [owned by the ENEMIES agent]
//  Latches ON when any of its SecurityCameras detects; stays ON for ResetSeconds (or until ResetAlarm),
//  then clears. Counts trips (RunStats.TeamworkViolations, stealth grade), optional story beat, klaxon,
//  a red beacon. Wire an EscortBack to it (CH04) or doors/lasers (CH07).
// =====================================================================================
using System;
using UnityEngine;

namespace Shifters
{
    [DisallowMultipleComponent]
    public class AlarmLatch : SignalSource, IResettable
    {
        public SecurityCamera[] Cameras = new SecurityCamera[0];
        public float ResetSeconds = 8f;
        [Tooltip("GameEvents.StoryBeat raised on every trip (e.g. ch04.detected). Empty = none.")] public string TripBeat = "";
        [Tooltip("Count trips in RunStats.TeamworkViolations.")] public bool CountViolations = true;
        [Tooltip("Toast on trip. Empty = none.")] public string TripToast = "TEAMWORK DETECTED! SECURITY IS ON ITS WAY.";
        public Renderer Beacon;

        public int TripCount { get; private set; }
        public float Remaining { get; private set; }
        public SecurityCamera TrippedBy { get; private set; }
        public bool Tripped => IsOn;

        public event Action<AlarmLatch> OnTripped;
        public event Action<AlarmLatch> OnCleared;

        float _klaxon;

        public void SetCameras(params SecurityCamera[] cams) { Cameras = cams ?? new SecurityCamera[0]; }

        void Update()
        {
            float dt = Time.deltaTime;
            if (!IsOn)
            {
                if (Cameras == null) return;
                for (int i = 0; i < Cameras.Length; i++)
                {
                    var c = Cameras[i];
                    if (c != null && c.Detecting) { Trip(c); break; }
                }
                return;
            }
            Remaining -= dt;
            _klaxon -= dt;
            if (_klaxon <= 0f && Remaining > 0.5f) { _klaxon = 1.6f; Sfx.At(SfxId.Klaxon, transform.position + Vector3.up, 0.6f); }
            if (Beacon != null) Beacon.enabled = Mathf.Repeat(Time.time * 2.5f, 1f) < 0.5f;
            if (Remaining <= 0f) ResetAlarm();
        }

        /// <summary>Trip now (also used by levels for scripted alarms).</summary>
        public void Trip(SecurityCamera by = null)
        {
            if (IsOn) return;
            TrippedBy = by;
            TripCount++;
            Remaining = ResetSeconds;
            _klaxon = 0f;
            SetOn(true);
            if (CountViolations) { var s = GameSession.Instance; if (s != null && s.Stats != null) s.Stats.TeamworkViolations++; }
            if (!string.IsNullOrEmpty(TripToast)) GameEvents.RaiseToast(TripToast, UIColors.Danger);
            if (!string.IsNullOrEmpty(TripBeat)) GameEvents.RaiseStoryBeat(TripBeat);
            OnTripped?.Invoke(this);
        }

        /// <summary>Clear the latch now (EscortBack calls this after the teleport).</summary>
        public void ResetAlarm()
        {
            if (!IsOn) return;
            Remaining = 0f;
            SetOn(false);
            if (Beacon != null) Beacon.enabled = false;
            OnCleared?.Invoke(this);
        }

        public void ResetState() { ResetAlarm(); }

        /// <summary>Pause every camera of this alarm (no view) for seconds.</summary>
        public void PauseCameras(float seconds)
        {
            if (Cameras == null) return;
            for (int i = 0; i < Cameras.Length; i++) if (Cameras[i] != null) Cameras[i].Pause(seconds);
        }
    }
}
