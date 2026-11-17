// =====================================================================================
//  SHIFTERS - LogoAnimator.cs   (UI agent)
//  The animated SHIFTERS logo: one TMP object per letter (Bungee, yellow->orange gradient,
//  outlined logo material), gentle idle bob, and every few seconds two letters "shift":
//  they arc past each other, swap places (whoosh), hold, then return.
// =====================================================================================
using TMPro;
using UnityEngine;

namespace Shifters
{
    public class LogoAnimator : MonoBehaviour
    {
        const string Word = "SHIFTERS";
        RectTransform[] _letters;
        Vector2[] _home;
        float _timer = 2.2f, _t;
        int _a = -1, _b = -1, _phase; // 0 idle, 1 swapping, 2 hold, 3 returning
        System.Random _rng = new System.Random(7);

        public bool IsShifting => _phase != 0;
        public float Width { get; private set; }

        public static LogoAnimator Create(Transform parent, float fontSize)
        {
            var rt = UIFactory.Rect("Logo", parent);
            var la = rt.gameObject.AddComponent<LogoAnimator>();
            la.Build(rt, fontSize);
            return la;
        }

        void Build(RectTransform rt, float size)
        {
            _letters = new RectTransform[Word.Length];
            _home = new Vector2[Word.Length];
            float x = 0f;
            var mat = FontLibrary.LogoMaterial;
            for (int i = 0; i < Word.Length; i++)
            {
                string ch = Word[i].ToString();
                var t = UIFactory.Text(rt, "L" + i, ch, FontLibrary.Display, size, Color.white, TextAlignmentOptions.Center);
                if (mat != null) t.fontSharedMaterial = mat;
                t.enableVertexGradient = true;
                t.colorGradient = new VertexGradient(UIColors.AccentYellow, UIColors.AccentYellow, UIColors.Accent, UIColors.Hex(0xF26A10));
                float w = UIFactory.Preferred(t, ch).x;
                var lrt = t.rectTransform;
                lrt.anchorMin = lrt.anchorMax = new Vector2(0f, 0.5f);
                lrt.pivot = new Vector2(0.5f, 0.5f);
                lrt.sizeDelta = new Vector2(w + 10f, size * 1.25f);
                _home[i] = new Vector2(x + w * 0.5f, 0f);
                lrt.anchoredPosition = _home[i];
                _letters[i] = lrt;
                x += w - size * 0.02f;
            }
            Width = x;
            rt.sizeDelta = new Vector2(x, size * 1.25f);
        }

        /// <summary>Start a letter shift right now (tests / on menu open).</summary>
        public void ForceShift()
        {
            if (_letters == null) return;
            _a = _rng.Next(Word.Length);
            do { _b = _rng.Next(Word.Length); } while (_b == _a || Mathf.Abs(_b - _a) > 4);
            _phase = 1;
            _t = 0f;
            Sfx.Play(SfxId.SwitchWhoosh, 0.45f, 1.1f);
        }

        void Update()
        {
            if (_letters == null) return;
            float dt = UITime.Delta;
            float now = UITime.Now;
            if (_phase == 0)
            {
                _timer -= dt;
                if (_timer <= 0f) { ForceShift(); _timer = 3.2f + (float)_rng.NextDouble() * 2.5f; }
            }
            else
            {
                _t += dt;
                if (_phase == 1 && _t >= 0.42f) { _phase = 2; _t = 0f; }
                else if (_phase == 2 && _t >= 0.75f) { _phase = 3; _t = 0f; Sfx.Play(SfxId.SwitchWhoosh, 0.3f, 1.3f); }
                else if (_phase == 3 && _t >= 0.36f) { _phase = 0; _t = 0f; }
            }
            for (int i = 0; i < _letters.Length; i++)
            {
                var p = _home[i] + new Vector2(0f, Mathf.Sin(now * 2.2f + i * 0.7f) * 4f);
                float rot = Mathf.Sin(now * 1.7f + i) * 1.5f;
                if (_phase != 0 && (i == _a || i == _b))
                {
                    int other = i == _a ? _b : _a;
                    float k = _phase == 1 ? Easing.Evaluate(Ease.OutBack, _t / 0.42f) : _phase == 2 ? 1f : 1f - Easing.Evaluate(Ease.InOutCubic, _t / 0.36f);
                    float arc = Mathf.Sin(Mathf.Clamp01(_phase == 1 ? _t / 0.42f : _phase == 3 ? _t / 0.36f : 1f) * Mathf.PI) * (i == _a ? 70f : -55f);
                    if (_phase == 2) arc = 0f;
                    p = Vector2.LerpUnclamped(_home[i], _home[other], k) + new Vector2(0f, arc);
                    rot += (i == _a ? -12f : 12f) * Mathf.Sin(Mathf.Clamp01(k) * Mathf.PI);
                }
                _letters[i].anchoredPosition = p;
                _letters[i].localEulerAngles = new Vector3(0f, 0f, rot);
            }
        }
    }
}
