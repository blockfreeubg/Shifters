// =====================================================================================
//  SHIFTERS - CampaignBossBar.cs   (ui_hud agent)
//  CAMPAIGN §15.2 boss bar (GameEvents.BossChanged / BossStatus): top centre, boss name +
//  department title, segmented hits for the current phase with the bar label on it
//  ("QUOTA: FIRED 0/3"), phase pips, "VULNERABLE!" flash, phase-change stamp and a
//  "TERMINATED" stamp on defeat. Hidden while a blocking scene runs.
// =====================================================================================
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Shifters
{
    public class CampaignBossBar : MonoBehaviour
    {
        public const float W = 580f, H = 124f, BarW = 540f, BarH = 32f;
        const int MaxSegments = 8, MaxPips = 6;

        public bool Shown { get; private set; }
        public BossStatus Status { get; private set; }
        public RectTransform Rt { get; private set; }
        /// <summary>Text of the defeat stamp (bosses may override per boss, e.g. "AUDITED").</summary>
        public string DefeatStampText = "TERMINATED";
        /// <summary>The defeat stamp is on screen (CampaignHud holds a chapter card until it is gone).</summary>
        public bool DefeatStampUp => Shown && _defeatStamp != null && _defeatStamp.gameObject.activeSelf;

        CanvasGroup _group;
        TextMeshProUGUI _name, _title, _label, _vulnText, _phaseStampText, _defeatText;
        RectTransform _bar, _segRow, _pipRow, _vuln, _phaseStamp, _defeatStamp;
        Image _barBorder;
        readonly Image[] _segs = new Image[MaxSegments];
        readonly Image[] _pips = new Image[MaxPips];
        bool _hasStatus, _wantVisible, _suppressed;
        float _vulnAge, _hideAt = -1f, _age;
        Vector2 _home = new Vector2(0f, -18f);

        public static CampaignBossBar Create(Transform parent)
        {
            var rt = UIFactory.Rect("BossBar", parent, new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(W, H), new Vector2(0.5f, 1f));
            var b = rt.gameObject.AddComponent<CampaignBossBar>();
            b.Rt = rt;
            b.Build();
            return b;
        }

        void Build()
        {
            _group = UIFactory.Group(gameObject);
            _group.alpha = 0f; _group.blocksRaycasts = false; _group.interactable = false;
            var panel = UIFactory.Panel(Rt, "Panel", new Vector2(W, H), UIFactory.WithAlpha(UIColors.Panel, 0.9f), 16, true, UIColors.Hex(0xFF4D5E, 0.55f));
            UIFactory.Stretch(panel.Rect);

            _name = UIFactory.Text(Rt, "Name", "", FontLibrary.Display, 38f, UIColors.Text, new Vector2(0.5f, 1f), new Vector2(0f, -4f), new Vector2(W - 40f, 44f), TextAlignmentOptions.Center);
            if (FontLibrary.DisplayShadow != null) _name.fontSharedMaterial = FontLibrary.DisplayShadow;
            _name.enableVertexGradient = true;
            _name.colorGradient = new VertexGradient(UIColors.AccentYellow, UIColors.AccentYellow, UIColors.Accent, UIColors.Accent);
            UIFactory.AutoSize(_name, 24f, 38f);
            _title = UIFactory.Text(Rt, "Title", "", FontLibrary.Label, 18f, UIColors.TextMuted, new Vector2(0.5f, 1f), new Vector2(0f, -44f), new Vector2(W - 40f, 22f), TextAlignmentOptions.Center);
            _title.characterSpacing = 8f;

            _bar = UIFactory.Rect("Bar", Rt, new Vector2(0.5f, 1f), new Vector2(0f, -68f), new Vector2(BarW, BarH), new Vector2(0.5f, 1f));
            UIFactory.Fill(_bar, "Track", UISprites.Rounded(9), UIColors.Hex(0x0B0D11, 0.94f));
            _segRow = UIFactory.Rect("Segments", _bar); UIFactory.Stretch(_segRow, 5f, 5f, 5f, 5f);
            for (int i = 0; i < MaxSegments; i++)
            {
                var s = UIFactory.Image(_segRow, "Seg" + i, UISprites.Rounded(5), UIColors.Danger);
                s.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                _segs[i] = s;
            }
            _barBorder = UIFactory.Fill(_bar, "Border", UISprites.RoundedOutline(9, 3), UIColors.Hex(0xFFFFFF, 0.5f));
            _label = UIFactory.Text(_bar, "Label", "", FontLibrary.Heavy, 19f, UIColors.Text, TextAlignmentOptions.Center);
            _label.characterSpacing = 4f;
            UIFactory.Stretch(_label.rectTransform, 8f, 8f, 0f, 2f);
            UIFactory.AutoSize(_label, 13f, 19f);

            _pipRow = UIFactory.Rect("Pips", Rt, new Vector2(0.5f, 1f), new Vector2(0f, -106f), new Vector2(W, 14f), new Vector2(0.5f, 1f));
            for (int i = 0; i < MaxPips; i++)
            {
                var p = UIFactory.Image(_pipRow, "Pip" + i, UISprites.Circle, UIColors.AccentYellow);
                var prt = p.rectTransform; prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f); prt.sizeDelta = new Vector2(13f, 13f);
                _pips[i] = p;
            }

            _vuln = UIFactory.Rect("Vulnerable", Rt, new Vector2(1f, 1f), new Vector2(18f, -48f), new Vector2(190f, 36f), new Vector2(0.5f, 0.5f));
            UIFactory.Fill(_vuln, "Bg", UISprites.Rounded(18), UIColors.AccentYellow);
            _vulnText = UIFactory.Text(_vuln, "Text", "VULNERABLE!", FontLibrary.Heavy, 21f, CampaignHudStyle.Ink, TextAlignmentOptions.Center);
            UIFactory.Stretch(_vulnText.rectTransform);
            _vuln.localRotation = Quaternion.Euler(0, 0, -6f);
            _vuln.gameObject.SetActive(false);

            _phaseStamp = CampaignHudStyle.Stamp(Rt, "PhaseStamp", "PHASE 2", CampaignHudStyle.StampInk, 26f, 7f, out _phaseStampText, true);
            _phaseStamp.anchorMin = _phaseStamp.anchorMax = new Vector2(0.5f, 1f); _phaseStamp.anchoredPosition = new Vector2(0f, -82f);
            _phaseStamp.gameObject.SetActive(false);
            _defeatStamp = CampaignHudStyle.Stamp(Rt, "DefeatStamp", DefeatStampText, CampaignHudStyle.StampInk, 40f, -8f, out _defeatText, true);
            _defeatStamp.anchorMin = _defeatStamp.anchorMax = new Vector2(0.5f, 1f); _defeatStamp.anchoredPosition = new Vector2(0f, -70f);
            _defeatStamp.gameObject.SetActive(false);
        }

        /// <summary>Apply a BossStatus (GameEvents.BossChanged).</summary>
        public void Apply(BossStatus s)
        {
            var prev = Status;
            bool first = !_hasStatus || prev.BossId != s.BossId;
            _hasStatus = true;
            Status = s;
            _name.text = (s.Name ?? "").ToUpperInvariant();
            _title.text = (s.Title ?? "").ToUpperInvariant();
            _label.text = s.BarLabel ?? "";
            int req = Mathf.Clamp(s.HitsRequired, 0, MaxSegments);
            int left = Mathf.Clamp(s.HitsRequired - s.Hits, 0, req);
            LayoutSegments(req, left, !first && s.Phase == prev.Phase && s.Hits > prev.Hits);
            LayoutPips(Mathf.Clamp(s.PhaseCount, 0, MaxPips), s.Phase);

            bool vuln = s.State == BossState.Vulnerable;
            if (vuln && (first || prev.State != BossState.Vulnerable))
            {
                _vuln.gameObject.SetActive(true); _vulnAge = 0f;
                UITween.Scale(_vuln, 1f, 0.3f, Ease.OutBack, 0f, 1.8f);
            }
            if (!vuln) _vuln.gameObject.SetActive(false);
            if (!first && s.Phase > prev.Phase && s.State != BossState.Defeated)
            {
                CampaignHudStyle.RefitStamp(_phaseStamp, _phaseStampText, "PHASE " + (s.Phase + 1));
                ShowStamp(_phaseStamp, 1.6f);
            }
            if (s.State == BossState.Defeated && (first || prev.State != BossState.Defeated))
            {
                CampaignHudStyle.RefitStamp(_defeatStamp, _defeatText, DefeatStampText);
                ShowStamp(_defeatStamp, 3f);
                UITween.Shake(Rt, 12f, 0.35f);
                _hideAt = UITime.Now + 2.6f;
            }
            else if (s.State != BossState.Defeated) _hideAt = -1f;
            if (s.Visible && s.State == BossState.Dormant) _wantVisible = false;
            else _wantVisible = s.Visible;
            Refresh(first);
        }

        void ShowStamp(RectTransform stamp, float seconds)
        {
            stamp.gameObject.SetActive(true);
            UITween.Kill(stamp);
            UITween.Scale(stamp, 1f, 0.25f, Ease.OutBack, 0f, 2.2f);
            UITween.Delay(seconds, () => { if (stamp != null) stamp.gameObject.SetActive(false); }, stamp);
        }

        void LayoutSegments(int req, int left, bool hitNow)
        {
            float inner = BarW - 10f, gap = 6f;
            float w = req > 0 ? (inner - gap * (req - 1)) / req : 0f;
            for (int i = 0; i < MaxSegments; i++)
            {
                var s = _segs[i];
                bool on = i < req;
                if (s.gameObject.activeSelf != on) s.gameObject.SetActive(on);
                if (!on) continue;
                var rt = s.rectTransform;
                rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(0f, 1f);
                rt.sizeDelta = new Vector2(w, 0f);
                rt.anchoredPosition = new Vector2(i * (w + gap) + w * 0.5f, 0f);
                bool full = i < left;
                var target = full ? UIColors.Danger : UIColors.Hex(0x2A313C, 0.9f);
                if (hitNow && i == left) { s.color = Color.white; UITween.Color(s, target, 0.4f); UITween.Punch(rt, 0.25f, 0.3f); }
                else s.color = target;
            }
            if (hitNow) { UITween.Shake(_bar, 10f, 0.3f); Sfx.Play(SfxId.BossHit, 0.5f); }
        }

        void LayoutPips(int count, int phase)
        {
            float spacing = 22f;
            float x0 = -(count - 1) * spacing * 0.5f;
            for (int i = 0; i < MaxPips; i++)
            {
                var p = _pips[i];
                bool on = i < count && count > 1;
                if (p.gameObject.activeSelf != on) p.gameObject.SetActive(on);
                if (!on) continue;
                p.rectTransform.anchoredPosition = new Vector2(x0 + i * spacing, 0f);
                p.sprite = i < phase ? UISprites.Circle : UISprites.Ring(10);
                p.color = i < phase ? UIColors.Success : (i == phase ? UIColors.AccentYellow : UIColors.Hex(0x9AA4B2, 0.6f));
                p.rectTransform.localScale = Vector3.one * (i == phase ? 1.25f : 1f);
            }
        }

        /// <summary>Blocking scenes hide the bar (it returns afterwards if still visible).</summary>
        public void SetSuppressed(bool s) { _suppressed = s; Refresh(false); }

        /// <summary>Vertical home (CampaignHud drops the bar under the timed-door timer when that shows).</summary>
        public void SetHome(Vector2 home)
        {
            if ((home - _home).sqrMagnitude < 0.5f) return;
            _home = home;
            UITween.Move(Rt, home, 0.25f, Ease.OutCubic);
        }

        public void Hide() { _wantVisible = false; _hasStatus = false; Refresh(false); }

        void Refresh(bool first)
        {
            bool show = _wantVisible && !_suppressed;
            if (show == Shown) return;
            Shown = show;
            UITween.Kill(_group);
            if (show)
            {
                UITween.Fade(_group, 1f, 0.25f);
                UITween.Move(Rt, _home, 0.4f, Ease.OutBack, 0f, _home + new Vector2(0f, 90f));
                if (first) Sfx.Play(SfxId.BossRoar, 0.35f);
            }
            else UITween.Fade(_group, 0f, 0.25f);
        }

        void Update()
        {
            if (!Shown) return;
            _age += UITime.Delta;
            if (_hideAt > 0f && UITime.Now >= _hideAt) { _hideAt = -1f; _wantVisible = false; Refresh(false); return; }
            if (_vuln.gameObject.activeSelf)
            {
                _vulnAge += UITime.Delta;
                float k = 0.5f + 0.5f * Mathf.Sin(_vulnAge * 12f);
                _barBorder.color = Color.Lerp(UIColors.Hex(0xFFFFFF, 0.5f), UIColors.AccentYellow, k);
                _vuln.localScale = Vector3.one * (1f + 0.06f * k);
            }
            else _barBorder.color = UIColors.Hex(0xFFFFFF, 0.5f);
        }
    }
}
