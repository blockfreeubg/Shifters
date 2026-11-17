// =====================================================================================
//  SHIFTERS - CampaignChaseMeter.cs   (ui_hud agent)
//  CAMPAIGN §15.2 chase meter (GameEvents.ChaseChanged / ChaseStatus): "PORKCHOP: 40 M"
//  with a track where the pursuer (pink snout) chases the tram (gold), station ticks,
//  green -> yellow -> red as the gap closes, "BUSTED!" stamp. Top centre (under the boss
//  bar when both show).
// =====================================================================================
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Shifters
{
    public class CampaignChaseMeter : MonoBehaviour
    {
        public const float W = 580f, H = 78f, TrackW = 520f;
        const int MaxStations = 6;

        /// <summary>Name printed before the gap ("PORKCHOP: 40 M").</summary>
        public string PursuerName = "PORKCHOP";
        public bool Shown { get; private set; }
        public ChaseStatus Status { get; private set; }
        public RectTransform Rt { get; private set; }
        public string LabelText => _label != null ? _label.text : "";

        CanvasGroup _group;
        TextMeshProUGUI _label, _station, _bustText;
        RectTransform _track, _fill, _tram, _pig, _bust;
        Image _fillImg;
        readonly Image[] _ticks = new Image[MaxStations];
        bool _want, _suppressed, _wasBusted;
        int _lastGapShown = -1;
        float _age;
        Vector2 _home = new Vector2(0f, -18f);

        public static CampaignChaseMeter Create(Transform parent)
        {
            var rt = UIFactory.Rect("ChaseMeter", parent, new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(W, H), new Vector2(0.5f, 1f));
            var c = rt.gameObject.AddComponent<CampaignChaseMeter>();
            c.Rt = rt;
            c.Build();
            return c;
        }

        void Build()
        {
            _group = UIFactory.Group(gameObject);
            _group.alpha = 0f; _group.blocksRaycasts = false; _group.interactable = false;
            var panel = UIFactory.Panel(Rt, "Panel", new Vector2(W, H), UIFactory.WithAlpha(UIColors.Panel, 0.9f), 16, true, UIColors.Hex(0xFF7BAC, 0.8f));
            UIFactory.Stretch(panel.Rect);
            _label = UIFactory.Text(Rt, "Label", "PORKCHOP: 40 M", FontLibrary.Heavy, 24f, UIColors.Text, new Vector2(0f, 1f), new Vector2(26f, -8f), new Vector2(360f, 30f), TextAlignmentOptions.Left);
            _label.characterSpacing = 3f;
            _station = UIFactory.Text(Rt, "Station", "", FontLibrary.Label, 18f, UIColors.TextMuted, new Vector2(1f, 1f), new Vector2(-26f, -12f), new Vector2(200f, 24f), TextAlignmentOptions.Right);
            _station.characterSpacing = 4f;

            _track = UIFactory.Rect("Track", Rt, new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(TrackW, 18f), new Vector2(0.5f, 0f));
            UIFactory.Fill(_track, "Bg", UISprites.Rounded(9), UIColors.Hex(0x0B0D11, 0.95f));
            _fillImg = UIFactory.Image(_track, "Fill", UISprites.Rounded(9), UIColors.Success);
            _fill = _fillImg.rectTransform;
            _fill.anchorMin = new Vector2(0f, 0f); _fill.anchorMax = new Vector2(1f, 1f); _fill.pivot = new Vector2(0f, 0.5f);
            _fill.offsetMin = _fill.offsetMax = Vector2.zero;
            for (int i = 0; i < MaxStations; i++)
            {
                var t = UIFactory.Image(_track, "Station" + i, UISprites.Rounded(2), new Color(1f, 1f, 1f, 0.7f));
                var trt = t.rectTransform; trt.anchorMin = trt.anchorMax = new Vector2(0f, 0.5f); trt.sizeDelta = new Vector2(4f, 26f);
                _ticks[i] = t;
            }
            // pursuer: pink head with a snout
            _pig = UIFactory.Rect("Pursuer", _track, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(34f, 34f), new Vector2(0.5f, 0.5f));
            UIFactory.Fill(_pig, "Head", UISprites.Circle, UIColors.Hex(0xFF8FB8));
            var snout = UIFactory.Image(_pig, "Snout", UISprites.Rounded(6), UIColors.Hex(0xE0608F));
            CampaignHudStyle.Place(snout.rectTransform, 0f, -0.08f, 0.5f, 0.32f);
            var n1 = UIFactory.Image(_pig, "N1", UISprites.Circle, UIColors.Hex(0x7A2A47)); CampaignHudStyle.Place(n1.rectTransform, -0.09f, -0.08f, 0.1f, 0.14f);
            var n2 = UIFactory.Image(_pig, "N2", UISprites.Circle, UIColors.Hex(0x7A2A47)); CampaignHudStyle.Place(n2.rectTransform, 0.09f, -0.08f, 0.1f, 0.14f);
            // tram: gold rounded box with windows
            _tram = UIFactory.Rect("Tram", _track, new Vector2(1f, 0.5f), Vector2.zero, new Vector2(46f, 28f), new Vector2(0.5f, 0.5f));
            UIFactory.Fill(_tram, "Body", UISprites.Rounded(7), CampaignHudStyle.MaxGold);
            var w1 = UIFactory.Image(_tram, "W1", UISprites.Rounded(3), UIColors.Hex(0x223040)); CampaignHudStyle.Place(w1.rectTransform, -0.2f, 0.1f, 0.3f, 0.4f);
            var w2 = UIFactory.Image(_tram, "W2", UISprites.Rounded(3), UIColors.Hex(0x223040)); CampaignHudStyle.Place(w2.rectTransform, 0.2f, 0.1f, 0.3f, 0.4f);

            _bust = CampaignHudStyle.Stamp(Rt, "Busted", "BUSTED!", CampaignHudStyle.StampInk, 40f, -7f, out _bustText, true);
            _bust.anchorMin = _bust.anchorMax = new Vector2(0.5f, 0.5f); _bust.anchoredPosition = Vector2.zero;
            _bust.gameObject.SetActive(false);
        }

        public void Apply(ChaseStatus s)
        {
            Status = s;
            _want = s.Visible;
            float max = Mathf.Max(1f, s.MaxGap);
            float gap = Mathf.Clamp(s.Gap, 0f, max);
            float t = gap / max;
            int g = Mathf.RoundToInt(gap);
            if (g != _lastGapShown) { _lastGapShown = g; _label.text = PursuerName + ": " + g + " M"; }
            _station.text = s.StationCount > 0 ? "STATION " + Mathf.Clamp(s.Station + 1, 1, s.StationCount) + "/" + s.StationCount : "";
            // the tram sits at the right end; the pursuer moves right as the gap closes
            float pigX = (1f - t) * (TrackW - 60f);
            _pig.anchoredPosition = new Vector2(pigX + 12f, 0f);
            _fill.anchorMin = new Vector2((pigX + 12f) / TrackW, 0f);
            _tram.anchoredPosition = new Vector2(-24f, 0f);
            _fillImg.color = t > 0.5f ? UIColors.Success : (t > 0.25f ? UIColors.AccentYellow : UIColors.Danger);
            int st = Mathf.Clamp(s.StationCount, 0, MaxStations);
            for (int i = 0; i < MaxStations; i++)
            {
                bool on = i < st;
                if (_ticks[i].gameObject.activeSelf != on) _ticks[i].gameObject.SetActive(on);
                if (!on) continue;
                float x = (i + 1f) / (st + 1f) * TrackW;
                _ticks[i].rectTransform.anchoredPosition = new Vector2(x, 0f);
                _ticks[i].color = i < s.Station ? UIColors.Success : new Color(1f, 1f, 1f, 0.6f);
            }
            if (s.Busted && !_wasBusted)
            {
                _bust.gameObject.SetActive(true);
                UITween.Scale(_bust, 1f, 0.3f, Ease.OutBack, 0f, 2.4f);
                UITween.Shake(Rt, 16f, 0.4f);
            }
            if (!s.Busted) _bust.gameObject.SetActive(false);
            _wasBusted = s.Busted;
            Refresh();
        }

        public void SetSuppressed(bool s) { _suppressed = s; Refresh(); }

        public void SetHome(Vector2 home)
        {
            if ((home - _home).sqrMagnitude < 0.5f) return;
            _home = home;
            if (Shown) UITween.Move(Rt, home, 0.25f, Ease.OutCubic); else Rt.anchoredPosition = home;
        }

        public void Hide() { _want = false; _wasBusted = false; Refresh(); }

        void Refresh()
        {
            bool show = _want && !_suppressed;
            if (show == Shown) return;
            Shown = show;
            UITween.Kill(_group);
            if (show) { Rt.anchoredPosition = _home; UITween.Fade(_group, 1f, 0.25f); UITween.Scale(Rt, 1f, 0.3f, Ease.OutBack, 0f, 0.8f); }
            else UITween.Fade(_group, 0f, 0.25f);
        }

        void Update()
        {
            if (!Shown) return;
            _age += UITime.Delta;
            float max = Mathf.Max(1f, Status.MaxGap);
            if (Status.Gap / max < 0.25f && !Status.Busted)
            {
                float k = 0.5f + 0.5f * Mathf.Sin(_age * 14f);
                _label.color = Color.Lerp(UIColors.Text, UIColors.Danger, k);
                _pig.localScale = Vector3.one * (1f + 0.12f * k);
            }
            else { _label.color = UIColors.Text; _pig.localScale = Vector3.one; }
        }
    }
}
