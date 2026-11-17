// =====================================================================================
//  SHIFTERS - HudTutorial.cs   (UI agent)
//  Top-center tutorial card: Bungee title, body, key-cap row, auto-hide timer bar and a
//  "[BACKSPACE] SKIP" hint. Listens: TutorialShow / TutorialHide; ShiftersInput.SkipTutorialPressed
//  hides it locally (TutorialManager owns the queue / seen ids).
//  Layout rules (polish H2, driven by CampaignHud every frame): while a boss bar / chase meter owns the top
//  centre the card DOCKS to the left column (under the objective memo + PiP, above the team cards) at 80 %
//  scale, so it no longer covers the bar, its stamps or the world call-outs over the boss; while a big
//  centre banner shows it DUCKS (fades out, auto-hide timer paused) and comes back when the banner is gone.
// =====================================================================================
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Shifters
{
    public class HudTutorial : MonoBehaviour
    {
        const float Width = 780f;
        static readonly Vector2 HomePos = new Vector2(0f, -150f);
        /// <summary>Docked placement (top-left anchored): left column under the PiP (ends at y -368), above the team cards (y -725).</summary>
        public static readonly Vector2 DockPos = new Vector2(26f, -390f);
        public const float DockScale = 0.8f;
        /// <summary>Bottom-left canvas units taken by the team cards (+ margin); a docked card shrinks (>= 60 %) to stay above them on short canvases (ultrawide).</summary>
        const float TeamColumnH = 362f;
        RectTransform _rt, _keysRow, _timerFill, _timerTrack;
        TextMeshProUGUI _title, _body;
        CanvasGroup _group;
        UIPanel _panel;
        readonly List<RectTransform> _keys = new List<RectTransform>();
        float _duration, _elapsed;
        bool _visible;
        TutorialMessage _current;

        public bool Visible => _visible;
        /// <summary>Docked to the left column (a boss bar / chase meter owns the top centre).</summary>
        public bool Docked { get; private set; }
        /// <summary>Faded out while a centre banner shows (timer paused).</summary>
        public bool Ducked { get; private set; }
        Vector2 Pos => Docked ? DockPos : HomePos;
        /// <summary>Where the card slides in from / out to: from above at home, from the left edge when docked.</summary>
        Vector2 OffPos(float amount) => Docked ? DockPos + new Vector2(-(Width * DockScale + 40f), 0f) : HomePos + new Vector2(0f, amount);

        /// <summary>Dock the card to the left column (true) or put it back at the top centre (false).</summary>
        public void SetDocked(bool docked)
        {
            if (docked == Docked) return;
            Docked = docked;
            UITween.Kill(_rt);
            var a = docked ? new Vector2(0f, 1f) : new Vector2(0.5f, 1f);
            _rt.anchorMin = _rt.anchorMax = a; _rt.pivot = a;
            _rt.localScale = Vector3.one * (docked ? DockScaleNow() : 1f);
            if (_visible) UITween.Move(_rt, Pos, 0.35f, Ease.OutCubic, 0f, OffPos(120f));
            else _rt.anchoredPosition = Pos;
        }

        /// <summary>Fade the card out while a centre banner shows (the card stays up; its timer pauses).</summary>
        public void SetDucked(bool ducked)
        {
            if (ducked == Ducked) return;
            Ducked = ducked;
            if (ducked && _visible) UITween.Kill(_group);   // stop a running fade-in; Hide's fade-out (not _visible) is left alone
        }
        public TutorialMessage Current => _current;

        float DockScaleNow()
        {
            var parent = _rt.parent as RectTransform;
            float avail = (parent != null ? parent.rect.height : 1080f) - TeamColumnH + DockPos.y;
            return Mathf.Clamp(avail / Mathf.Max(1f, _rt.sizeDelta.y), 0.6f, DockScale);
        }

        public static HudTutorial Create(Transform parent)
        {
            var rt = UIFactory.Rect("Tutorial", parent, new Vector2(0.5f, 1f), HomePos, new Vector2(Width, 220f), new Vector2(0.5f, 1f));
            var t = rt.gameObject.AddComponent<HudTutorial>();
            t._rt = rt;
            t._group = UIFactory.Group(rt.gameObject);
            t._panel = UIFactory.Panel(rt, "Panel", rt.sizeDelta, UIFactory.WithAlpha(UIColors.Panel, 0.95f), 20, true, UIFactory.WithAlpha(UIColors.Accent, 0.85f));
            UIFactory.Stretch(t._panel.Rect);
            var stripes = UIFactory.Image(rt, "Stripes", UISprites.Stripes, UIFactory.WithAlpha(UIColors.AccentYellow, 0.9f));
            stripes.type = Image.Type.Tiled;
            var srt = stripes.rectTransform;
            srt.anchorMin = new Vector2(0f, 1f); srt.anchorMax = new Vector2(0f, 1f); srt.pivot = new Vector2(0f, 1f);
            srt.anchoredPosition = new Vector2(22f, 14f); srt.sizeDelta = new Vector2(150f, 26f);
            var tag = UIFactory.Text(rt, "Tag", "TUTORIAL", FontLibrary.Label, 18f, UIColors.Hex(0x12161C), new Vector2(0f, 1f), new Vector2(22f, 14f), new Vector2(150f, 26f), TextAlignmentOptions.Center, false, new Vector2(0f, 1f));
            tag.characterSpacing = 6f;
            var tagBg = UIFactory.Image(rt, "TagBg", UISprites.Rounded(4), UIColors.AccentYellow);
            tagBg.rectTransform.anchorMin = tagBg.rectTransform.anchorMax = new Vector2(0f, 1f);
            tagBg.rectTransform.pivot = new Vector2(0f, 1f);
            tagBg.rectTransform.anchoredPosition = new Vector2(46f, 11f); tagBg.rectTransform.sizeDelta = new Vector2(102f, 20f);
            tagBg.transform.SetSiblingIndex(stripes.transform.GetSiblingIndex() + 1);

            t._title = UIFactory.Text(rt, "Title", "", FontLibrary.Display, 38f, UIColors.AccentYellow, new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(Width - 60f, 48f), TextAlignmentOptions.Center, false, new Vector2(0.5f, 1f));
            UIFactory.AutoSize(t._title, 24f, 38f);
            t._body = UIFactory.Text(rt, "Body", "", FontLibrary.Body, 26f, UIColors.Text, new Vector2(0.5f, 1f), new Vector2(0f, -76f), new Vector2(Width - 70f, 70f), TextAlignmentOptions.Top, true, new Vector2(0.5f, 1f));
            t._keysRow = UIFactory.Rect("Keys", rt, new Vector2(0.5f, 0f), new Vector2(0f, 44f), new Vector2(Width - 60f, 44f), new Vector2(0.5f, 0f));
            var skip = UIFactory.Rect("Skip", rt, new Vector2(1f, 0f), new Vector2(-18f, 12f), new Vector2(240f, 26f), new Vector2(1f, 0f));
            var skipText = UIFactory.Text(skip, "Text", "SKIP", FontLibrary.Label, 18f, UIColors.TextMuted, new Vector2(1f, 0.5f), Vector2.zero, new Vector2(60f, 24f), TextAlignmentOptions.Right, false, new Vector2(1f, 0.5f));
            skipText.characterSpacing = 4f;
            var key = UIFactory.KeyCap(skip, "Backspace", 22f);
            key.anchorMin = key.anchorMax = new Vector2(1f, 0.5f); key.pivot = new Vector2(1f, 0.5f); key.anchoredPosition = new Vector2(-66f, 1f);
            t._timerTrack = UIFactory.Rect("Timer", rt, new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(Width - 40f, 5f), new Vector2(0.5f, 0f));
            UIFactory.Fill(t._timerTrack, "Bg", UISprites.Rounded(2), new Color(1f, 1f, 1f, 0.08f));
            var fill = UIFactory.Fill(t._timerTrack, "Fill", UISprites.Rounded(2), UIColors.Accent);
            t._timerFill = fill.rectTransform;
            t._group.alpha = 0f;
            rt.gameObject.SetActive(false);
            return t;
        }

        // Awake/OnDestroy (not OnEnable): the popup deactivates its own GameObject while hidden.
        void Awake()
        {
            GameEvents.TutorialShow += Show;
            GameEvents.TutorialHide += Hide;
        }

        void OnDestroy()
        {
            GameEvents.TutorialShow -= Show;
            GameEvents.TutorialHide -= Hide;
        }

        public void Clear() { _visible = false; _current = null; _rt.gameObject.SetActive(false); }

        public void Show(TutorialMessage m)
        {
            if (m == null) return;
            _current = m;
            _title.text = string.IsNullOrEmpty(m.Title) ? "TIP" : m.Title.ToUpperInvariant();
            _body.text = m.Body ?? "";
            for (int i = 0; i < _keys.Count; i++) if (_keys[i] != null) Destroy(_keys[i].gameObject);
            _keys.Clear();
            bool hasKeys = m.Keys != null && m.Keys.Length > 0;
            if (hasKeys)
            {
                float total = 0f;
                foreach (var k in m.Keys)
                {
                    var cap = UIFactory.KeyCap(_keysRow, k, 40f);
                    _keys.Add(cap);
                    total += cap.sizeDelta.x + 10f;
                }
                float x = -total * 0.5f + 5f;
                for (int i = 0; i < _keys.Count; i++)
                {
                    var cap = _keys[i];
                    cap.anchorMin = cap.anchorMax = new Vector2(0.5f, 0.5f); cap.pivot = new Vector2(0f, 0.5f);
                    cap.anchoredPosition = new Vector2(x, 0f);
                    x += cap.sizeDelta.x + 10f;
                    UITween.Scale(cap, 1f, 0.35f, Ease.OutBack, 0.15f + i * 0.05f, 0f);
                }
            }
            float bodyH = Mathf.Max(34f, UIFactory.Preferred(_body, _body.text, Width - 70f).y);
            _body.rectTransform.sizeDelta = new Vector2(Width - 70f, bodyH);
            float height = 76f + bodyH + (hasKeys ? 64f : 14f) + 40f;
            _rt.sizeDelta = new Vector2(Width, height);
            _duration = m.Important ? 0f : Mathf.Max(0f, m.Duration);
            _elapsed = 0f;
            _timerTrack.gameObject.SetActive(_duration > 0f);
            _timerFill.anchorMax = new Vector2(1f, 1f);
            _rt.gameObject.SetActive(true);
            _visible = true;
            if (!Ducked) UITween.Fade(_group, 1f, 0.2f);
            UITween.Move(_rt, Pos, 0.45f, Ease.OutBack, 0f, OffPos(120f));
            Sfx.Play(SfxId.TutorialPop);
        }

        public void Hide()
        {
            if (!_visible) return;
            _visible = false;
            UITween.Move(_rt, OffPos(80f), 0.25f, Ease.InBack);
            UITween.Fade(_group, 0f, 0.25f, Ease.OutQuad, 0f, null, () => { if (this != null && !_visible) _rt.gameObject.SetActive(false); });
        }

        void Update()
        {
            if (!_visible) return;
            if (ShiftersInput.SkipTutorialPressed) { Sfx.Play(SfxId.UIBack); Hide(); return; }
            if (Docked) { float ds = DockScaleNow(); if (Mathf.Abs(_rt.localScale.x - ds) > 0.01f) _rt.localScale = Vector3.one * ds; }
            if (Ducked) { if (_group.alpha > 0f) _group.alpha = Mathf.MoveTowards(_group.alpha, 0f, UITime.Delta * 8f); return; }
            if (_group.alpha < 1f) _group.alpha = Mathf.MoveTowards(_group.alpha, 1f, UITime.Delta * 6f);
            if (_duration <= 0f) return;
            _elapsed += UITime.Delta;
            float left = Mathf.Clamp01(1f - _elapsed / _duration);
            _timerFill.anchorMax = new Vector2(left, 1f);
            if (_elapsed >= _duration) Hide();
        }
    }
}
