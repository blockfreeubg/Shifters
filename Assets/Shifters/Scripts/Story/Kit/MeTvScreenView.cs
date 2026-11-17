// =====================================================================================
//  SHIFTERS - MeTvScreenView.cs   [owned by STORY_KIT]   (StoryKit.MeTvScreen runtime)
//  Idle: cycles the ticker every 4 s, blinks the LIVE dot, bobs gently. On GameEvents.BroadcastChanged(true) it shows
//  VillainStudio's texture (if visuals provides one) instead of the idle portrait; restores on (false).
// =====================================================================================
using TMPro;
using UnityEngine;

namespace Shifters
{
    public sealed class MeTvScreenView : MonoBehaviour
    {
        public MeshRenderer Screen;
        public GameObject Portrait;
        public TextMeshPro Ticker, Logo, Headline;
        public MeshRenderer LiveDot;
        public string[] TickerLines;
        public float TickerSeconds = 4f;
        public bool Bob = true;

        int _tick;
        float _t, _phase;
        Vector3 _basePos;
        Material _idleMat, _liveMat;
        bool _live;

        void OnEnable()
        {
            _basePos = transform.localPosition;
            _phase = (transform.position.x * 0.37f + transform.position.z * 0.11f) % 6.28f;
            GameEvents.BroadcastChanged += OnBroadcast;
        }

        void OnDisable()
        {
            GameEvents.BroadcastChanged -= OnBroadcast;
            if (_live) OnBroadcast(false, null);
            transform.localPosition = _basePos;
        }

        void OnDestroy() { if (_liveMat != null) Destroy(_liveMat); }

        /// <summary>Changes the big idle headline (e.g. per chapter).</summary>
        public void SetHeadline(string text) { if (Headline != null) Headline.text = text ?? ""; }

        void OnBroadcast(bool on, string speakerId)
        {
            if (Screen == null) return;
            if (on)
            {
                var tex = StoryKitLate.VillainTexture();
                if (tex == null) { if (Headline != null) Headline.text = "ON AIR:\nMAXIMILIAN ME"; return; }
                if (_idleMat == null) _idleMat = Screen.sharedMaterial;
                if (_liveMat == null) _liveMat = new Material(MaterialLibrary.UnlitShader) { name = "MeTvLive" };
                _liveMat.SetColor(ShaderIds.BaseColor, Color.white);
                _liveMat.SetTexture(ShaderIds.BaseMap, tex);
                Screen.sharedMaterial = _liveMat;
                if (Portrait != null) Portrait.SetActive(false);
                if (Headline != null) Headline.gameObject.SetActive(false);
                _live = true;
            }
            else
            {
                if (_idleMat != null) Screen.sharedMaterial = _idleMat;
                if (Portrait != null) Portrait.SetActive(true);
                if (Headline != null) { Headline.gameObject.SetActive(true); if (Headline.text.StartsWith("ON AIR")) Headline.text = "SHIFT CHANGE IN: NEVER."; }
                _live = false;
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _t += dt;
            if (LiveDot != null) LiveDot.enabled = (Time.time % 1f) < 0.6f;
            if (_t >= TickerSeconds && Ticker != null)
            {
                _t = 0f;
                var lines = TickerLines != null && TickerLines.Length > 0 ? TickerLines : StoryKitLines.Ticker;
                _tick = (_tick + 1) % lines.Length;
                Ticker.text = lines[_tick];
            }
            if (Bob) transform.localPosition = _basePos + new Vector3(0f, Mathf.Sin(Time.time * 0.8f + _phase) * 0.12f, 0f);
        }
    }
}
