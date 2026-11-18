// =====================================================================================
//  SHIFTERS - DeviceKeyCap.cs   (gp_core)
//  A key cap bound to an ACTION that restyles itself live when the player switches device:
//  keyboard = the classic light key cap with the keyboard text (unchanged), gamepad = round Xbox-coloured
//  face buttons (A green, B red, X blue, Y yellow) or a dark labelled cap (LB/RB/LT/RT/LS/RS/L3/R3/D-pad/
//  Start/Back). Create with UIFactory.ActionCap. Width can change: hosts that lay caps out side by side
//  listen to Resized (or anchor the cap by the side that must stay put).
// =====================================================================================
using System;
using UnityEngine;

namespace Shifters
{
    public class DeviceKeyCap : MonoBehaviour
    {
        /// <summary>Canonical action (KeyLabels.Actions) or null when the cap text is not an action.</summary>
        public string Action { get; private set; }
        /// <summary>Text shown on keyboard/mouse.</summary>
        public string KeyboardText { get; private set; }
        public float Height { get; private set; }
        /// <summary>False when the current device has no binding for the action (the cap graphics are hidden).</summary>
        public bool HasLabel { get; private set; } = true;
        /// <summary>Current cap text as drawn (upper case; "" when hidden).</summary>
        public string Text { get; private set; }
        public bool ShowingGamepad { get; private set; }
        /// <summary>Raised after a restyle changed the cap width or visibility.</summary>
        public event Action<DeviceKeyCap> Resized;

        bool _inited;

        public void Init(string action, string keyboardText, float height)
        {
            Action = action;
            KeyboardText = keyboardText;
            Height = height;
            _inited = true;
            Refresh(true);
        }

        void OnEnable()
        {
            ShiftersInput.DeviceChanged += OnDeviceChanged;
            if (_inited) Refresh(false);
        }

        void OnDisable() => ShiftersInput.DeviceChanged -= OnDeviceChanged;

        void OnDeviceChanged(bool gamepad) => Refresh(false);

        /// <summary>Restyle for the current device.</summary>
        public void Refresh(bool force)
        {
            if (!_inited) return;
            bool pad = ShiftersInput.UsingGamepad;
            if (!force && pad == ShowingGamepad) return;
            ShowingGamepad = pad;
            string label = pad ? (Action != null ? KeyLabels.Gamepad(Action) : "") : KeyboardText;
            var rt = (RectTransform)transform;
            float oldW = rt.sizeDelta.x; bool oldHas = HasLabel;
            HasLabel = !string.IsNullOrEmpty(label);
            Text = (label ?? "").ToUpperInvariant();
            UIFactory.StyleCap(rt, HasLabel ? label : "", pad, Height);
            if (Mathf.Abs(rt.sizeDelta.x - oldW) > 0.01f || oldHas != HasLabel) Resized?.Invoke(this);
        }
    }
}
