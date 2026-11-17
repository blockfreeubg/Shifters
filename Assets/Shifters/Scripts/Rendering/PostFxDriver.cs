// =====================================================================================
//  SHIFTERS - PostFxDriver.cs   (RENDERING)
//  Lives on the persistent [PostFx] object: animates screen flashes (overlay canvas, unscaled time),
//  decays chromatic-aberration bursts and keeps Camera.main rendering post-processing.
// =====================================================================================
using UnityEngine;
using UnityEngine.UI;

namespace Shifters
{
    public class PostFxDriver : MonoBehaviour
    {
        Canvas _canvas;
        RawImage _flash;
        Color _flashColor;
        float _flashT = 1f, _flashDur = 0.25f;
        float _burstPeak, _burstT = 1f, _burstDur = 0.3f;
        float _camCheck;

        void Awake()
        {
            var cgo = new GameObject("FlashCanvas");
            cgo.transform.SetParent(transform, false);
            _canvas = cgo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = -10;
            var igo = new GameObject("Flash");
            igo.transform.SetParent(cgo.transform, false);
            _flash = igo.AddComponent<RawImage>();
            _flash.raycastTarget = false;
            var rt = _flash.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            _flash.color = new Color(1, 1, 1, 0);
            _canvas.enabled = false;
        }

        public void Flash(Color c, float duration)
        {
            _flashColor = c;
            _flashDur = duration;
            _flashT = 0f;
            _canvas.enabled = true;
        }

        public void Burst(float strength, float duration)
        {
            _burstPeak = Mathf.Max(strength, CurrentBurst());
            _burstDur = duration;
            _burstT = 0f;
        }

        float CurrentBurst() => _burstT >= 1f ? 0f : _burstPeak * (1f - _burstT) * (1f - _burstT);

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (_flashT < 1f)
            {
                _flashT = Mathf.Min(1f, _flashT + dt / _flashDur);
                float a = (1f - _flashT); a *= a;
                var c = _flashColor; c.a = Mathf.Clamp01(_flashColor.a) * 0.55f * a;
                _flash.color = c;
                if (_flashT >= 1f) _canvas.enabled = false;
            }
            if (_burstT < 1f)
            {
                _burstT = Mathf.Min(1f, _burstT + dt / _burstDur);
                PostFx.SetBurstValue(CurrentBurst());
            }
            _camCheck -= dt;
            if (_camCheck <= 0f)
            {
                _camCheck = 0.5f;
                PostFx.EnsureCameraPost(Camera.main);
            }
        }
    }
}
