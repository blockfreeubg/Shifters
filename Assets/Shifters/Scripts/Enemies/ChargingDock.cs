// =====================================================================================
//  SHIFTERS - ChargingDock.cs   [owned by the ENEMIES agent]
//  Promoted copy of L5ChargingDock (the original stays for L5's saved scene). When powered, its bot
//  remembers it is on 3% battery, glides onto the dock and falls asleep (brain disabled, kinematic, "Zzz").
//  It never docks while carrying a prisoner or stunned - it finishes that first. Once asleep it stays
//  asleep unless WakeUp() is called. Build with EnemyKit.ChargingDock(...).
// =====================================================================================
using TMPro;
using UnityEngine;

namespace Shifters
{
    public class ChargingDock : SignalReceiver
    {
        public SecurityBot Bot;
        [Tooltip("Where the bot parks (its feet).")] public Transform DockPoint;
        public TextMeshPro Status;
        public PuzzleLamp Lamp;
        public float GlideSeconds = 1.6f;

        public bool IsDocking { get; private set; }
        public bool IsAsleep { get; private set; }

        bool _wanted;
        float _t, _zzz;
        Vector3 _from;
        Quaternion _fromRot;
        Rigidbody _rb;

        protected override void OnPowerChanged(bool powered, bool initial)
        {
            if (powered) _wanted = true;
            if (Lamp != null) Lamp.Set(powered || IsAsleep ? PuzzleLampColor.Green : PuzzleLampColor.Red);
            if (Status != null && !IsAsleep) Status.text = powered ? "DOCK: POWERED" : "DOCK: NO POWER";
            if (powered && !initial) Sfx.At(SfxId.PuzzleSolved, transform.position + Vector3.up, 0.7f);
        }

        /// <summary>Wake the bot again (brain on, physics on). The dock must be powered again to re-dock it.</summary>
        public void WakeUp()
        {
            if (Bot == null || (!IsAsleep && !IsDocking)) return;
            IsAsleep = false; IsDocking = false; _wanted = false;
            if (_rb != null) _rb.isKinematic = false;
            Bot.enabled = true;
            if (Status != null) Status.text = "DOCK: IDLE";
            Fx.FloatingText(Bot.transform.position + Vector3.up * 2.2f, "100% BATTERY!", UIColors.Danger, 1.2f);
        }

        void FixedUpdate()
        {
            if (Bot == null || IsAsleep) return;
            if (_wanted && !IsDocking)
            {
                if (Bot.Carried != null || Bot.IsStunned) return; // finish the delivery / reboot first
                BeginDock();
            }
            if (!IsDocking) return;
            _t += Time.fixedDeltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_t / GlideSeconds));
            var target = DockPoint != null ? DockPoint.position : transform.position;
            var targetRot = DockPoint != null ? DockPoint.rotation : transform.rotation;
            var p = Vector3.Lerp(_from, target, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.6f;
            var r = Quaternion.Slerp(_fromRot, targetRot, k);
            if (_rb != null) { _rb.MovePosition(p); _rb.MoveRotation(r); }
            else { Bot.transform.SetPositionAndRotation(p, r); }
            if (k >= 1f) FallAsleep();
        }

        protected override void Update()
        {
            base.Update();
            if (!IsAsleep || Bot == null) return;
            _zzz -= Time.deltaTime;
            if (_zzz <= 0f)
            {
                _zzz = 1.6f;
                Fx.FloatingText(Bot.transform.position + Vector3.up * 2.1f, "Zzz", UIColors.Info, 0.9f);
            }
        }

        void BeginDock()
        {
            IsDocking = true;
            _t = 0f;
            _rb = Bot.GetComponent<Rigidbody>();
            if (_rb != null)
            {
                if (!_rb.isKinematic) { _rb.linearVelocity = Vector3.zero; _rb.angularVelocity = Vector3.zero; }
                _rb.isKinematic = true;
            }
            Bot.enabled = false;
            _from = Bot.transform.position;
            _fromRot = Bot.transform.rotation;
            Fx.FloatingText(Bot.transform.position + Vector3.up * 2.2f, "LOW BATTERY!", UIColors.AccentYellow, 1.2f);
            Sfx.At(SfxId.EnemyBeep, Bot.transform.position, 0.9f, 0.6f);
            GameEvents.RaiseToast("BONK-BOT: \"3% BATTERY. GOING TO MY HAPPY PLACE.\"", UIColors.Info);
        }

        void FallAsleep()
        {
            IsDocking = false;
            IsAsleep = true;
            if (Status != null) Status.text = "CHARGING... DO NOT DISTURB";
            if (Lamp != null) Lamp.Set(PuzzleLampColor.Green);
            Sfx.At(SfxId.ElevatorHum, transform.position, 0.6f, 0.7f);
            Fx.Puff(transform.position, 1.2f);
        }
    }
}
