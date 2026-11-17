// SHIFTERS - L7SweepChip.cs  SPD view chip on the floor at a camera's chokepoint: "SWEEP AWAY IN 1.4 s" while the
// camera's cone covers the point, "CLEAR FOR 2.3 s" while it does not. Label strings are cached (no per-frame allocs).
using UnityEngine;

namespace Shifters
{
    public class L7SweepChip : MonoBehaviour
    {
        public SecurityCamera Camera;
        public string Where = "";
        public Revealable Reveal;

        static string[] _away, _clear;
        string _text = "";
        float _next;

        static void EnsureStrings()
        {
            if (_away != null) return;
            _away = new string[100]; _clear = new string[100];
            for (int i = 0; i < 100; i++)
            {
                string s = (i / 10f).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
                _away[i] = "CAMERA LOOKING HERE\nSWEEP AWAY IN " + s + " s";
                _clear[i] = "CAMERA LOOKING AWAY\nCLEAR FOR " + s + " s";
            }
        }

        void Awake()
        {
            EnsureStrings();
            if (Reveal != null) Reveal.DynamicLabel = Label;
        }

        string Label(ShifterCharacter c) => _text;

        void Update()
        {
            if (Time.time < _next) return;
            _next = Time.time + 0.1f;
            if (Camera == null) { _text = ""; return; }
            if (Camera.IsInactive) { _text = "CAMERA OFFLINE"; return; }
            Vector3 p = transform.position;
            float away = Camera.SecondsUntilClear(p, 9.9f);
            if (away > 0.01f) { _text = _away[Mathf.Clamp(Mathf.RoundToInt(away * 10f), 0, 99)]; return; }
            float clear = Camera.SecondsUntilSees(p, 9.9f);
            _text = _clear[Mathf.Clamp(Mathf.RoundToInt(clear * 10f), 0, 99)];
        }
    }
}
