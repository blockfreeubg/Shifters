// =====================================================================================
//  SHIFTERS - CampaignRulerPrompt.cs   (ui_hud agent)
//  C15 "WHO RULES TODAY? [RIGHT SHIFT]" visuals (CAMPAIGN §12): a sunrise-gold title with
//  the key cap; while the switcher wheel is open it reads "VOTE: <NAME>" in the
//  highlighted intern's color; the pick (wheel closes on a slot, or a switch) stamps
//  "PRESIDENT OF THE DAY: <NAME>" and raises CampaignRulerPrompt.RulerChosen.
// =====================================================================================
using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Shifters
{
    public class CampaignRulerPrompt : MonoBehaviour
    {
        public const float W = 980f, H = 196f;
        public static readonly Vector2 HomePos = new Vector2(0f, -126f);

        /// <summary>Raised once when a ruler is picked while the prompt is active (story's ShiftocracyEnding may listen).</summary>
        public static event Action<ShifterCharacter> RulerChosen;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { RulerChosen = null; }

        public bool Active { get; private set; }
        public ShifterCharacter Chosen { get; private set; }
        public string SubText => _sub != null ? _sub.text : "";
        public RectTransform Rt { get; private set; }

        CanvasGroup _group;
        TextMeshProUGUI _title, _sub, _stampText;
        RectTransform _keyRow, _stamp, _glow;
        int _hover = -1;
        bool _wheelOpen;
        float _age, _hideAt = -1f;

        public static CampaignRulerPrompt Create(Transform parent)
        {
            var rt = UIFactory.Rect("RulerPrompt", parent, new Vector2(0.5f, 1f), HomePos, new Vector2(W, H), new Vector2(0.5f, 1f));
            var p = rt.gameObject.AddComponent<CampaignRulerPrompt>();
            p.Rt = rt;
            p.Build();
            return p;
        }

        void Build()
        {
            _group = UIFactory.Group(gameObject);
            _group.alpha = 0f; _group.blocksRaycasts = false; _group.interactable = false;
            _glow = UIFactory.Image(Rt, "Sun", UISprites.SoftCircle, new Color(1f, 0.75f, 0.3f, 0.45f)).rectTransform;
            _glow.anchorMin = _glow.anchorMax = new Vector2(0.5f, 0.5f); _glow.sizeDelta = new Vector2(1100f, 300f);
            var rays = UIFactory.Image(_glow, "Rays", UISprites.RadialLines(28), new Color(1f, 0.85f, 0.4f, 0.12f));
            UIFactory.Stretch(rays.rectTransform);
            _title = UIFactory.Text(Rt, "Title", "WHO RULES TODAY?", FontLibrary.Display, 76f, UIColors.Text, new Vector2(0.5f, 1f), new Vector2(0f, -6f), new Vector2(W, 96f), TextAlignmentOptions.Center);
            if (FontLibrary.DisplayShadow != null) _title.fontSharedMaterial = FontLibrary.DisplayShadow;
            _title.enableVertexGradient = true;
            _title.colorGradient = new VertexGradient(UIColors.Hex(0xFFF1A8), UIColors.Hex(0xFFF1A8), UIColors.AccentYellow, UIColors.Accent);
            UIFactory.AutoSize(_title, 44f, 76f);

            _keyRow = UIFactory.Rect("KeyRow", Rt, new Vector2(0.5f, 1f), new Vector2(0f, -112f), new Vector2(W, 50f), new Vector2(0.5f, 1f));
            var bg = UIFactory.Image(_keyRow, "Bg", UISprites.Rounded(25), new Color(0.06f, 0.07f, 0.09f, 0.82f));
            var bgr = bg.rectTransform; bgr.anchorMin = bgr.anchorMax = new Vector2(0.5f, 0.5f); bgr.sizeDelta = new Vector2(640f, 54f);
            var key = UIFactory.KeyCap(_keyRow, "RIGHT SHIFT", 38f);
            key.anchorMin = key.anchorMax = new Vector2(0.5f, 0.5f); key.pivot = new Vector2(1f, 0.5f); key.anchoredPosition = new Vector2(-150f, 2f);
            _sub = UIFactory.Text(_keyRow, "Sub", "PICK TODAY'S PRESIDENT", FontLibrary.Label, 26f, UIColors.Text, new Vector2(0.5f, 0.5f), new Vector2(-134f, 0f), new Vector2(460f, 40f), TextAlignmentOptions.Left, false, new Vector2(0f, 0.5f));
            _sub.characterSpacing = 4f;

            _stamp = CampaignHudStyle.Stamp(Rt, "Elected", "PRESIDENT OF THE DAY", CampaignHudStyle.StampRed, 34f, -6f, out _stampText);
            _stamp.anchorMin = _stamp.anchorMax = new Vector2(0.5f, 1f); _stamp.anchoredPosition = new Vector2(0f, -150f);
            _stamp.gameObject.SetActive(false);
        }

        public void Show(string title = null)
        {
            Active = true; Chosen = null; _hover = -1; _hideAt = -1f; _age = 0f;
            _title.text = string.IsNullOrEmpty(title) ? "WHO RULES TODAY?" : title;
            _sub.text = "PICK TODAY'S PRESIDENT"; _sub.color = UIColors.Text;
            _keyRow.gameObject.SetActive(true);
            _stamp.gameObject.SetActive(false);
            UITween.Kill(_group);
            UITween.Fade(_group, 1f, 0.4f);
            UITween.Scale(Rt, 1f, 0.5f, Ease.OutBack, 0f, 0.6f);
        }

        public void Hide(bool instant = false)
        {
            Active = false; _hideAt = -1f;
            UITween.Kill(_group);
            if (instant) _group.alpha = 0f; else UITween.Fade(_group, 0f, 0.4f);
        }

        public void OnWheelOpen(bool open)
        {
            if (!Active || Chosen != null) return;
            if (open) { _wheelOpen = true; _hover = -1; return; }
            if (_wheelOpen && _hover >= 0) Pick(SlotCharacter(_hover));
            _wheelOpen = false;
        }

        public void OnWheelSelection(int slot)
        {
            if (!Active || Chosen != null) return;
            _hover = slot;
            var c = SlotCharacter(slot);
            if (c == null) { _sub.text = "PICK TODAY'S PRESIDENT"; _sub.color = UIColors.Text; return; }
            _sub.text = "VOTE: " + c.DisplayName.ToUpperInvariant();
            _sub.color = c.Color;
            UITween.Punch(_keyRow, 0.06f, 0.2f);
        }

        public void OnSwitched(ShifterCharacter to)
        {
            if (!Active || Chosen != null || to == null) return;
            Pick(to);
        }

        /// <summary>Stamp a ruler explicitly (story can call this if it resolves the pick itself).</summary>
        public void Pick(ShifterCharacter c)
        {
            if (c == null || Chosen != null) return;
            Chosen = c;
            _keyRow.gameObject.SetActive(false);
            CampaignHudStyle.RefitStamp(_stamp, _stampText, "PRESIDENT OF THE DAY: " + c.DisplayName.ToUpperInvariant());
            _stampText.color = c.Color;
            _stamp.gameObject.SetActive(true);
            UITween.Scale(_stamp, 1f, 0.3f, Ease.OutBack, 0f, 2.4f);
            UITween.Shake(Rt, 10f, 0.25f);
            Sfx.Play(SfxId.Gavel, 0.9f);
            Sfx.Play(SfxId.CrowdCheer, 0.6f);
            _hideAt = UITime.Now + 4f;
            try { RulerChosen?.Invoke(c); } catch (Exception e) { Debug.LogException(e); }
        }

        static ShifterCharacter SlotCharacter(int slot)
        {
            var cm = CharacterManager.Instance;
            if (cm == null || cm.Team == null) return null;
            for (int i = 0; i < cm.Team.Count; i++) if (cm.Team[i] != null && cm.Team[i].Slot == slot) return cm.Team[i];
            return slot >= 0 && slot < cm.Team.Count ? cm.Team[slot] : null;
        }

        void Update()
        {
            if (!Active) return;
            _age += UITime.Delta;
            _glow.localRotation = Quaternion.Euler(0, 0, _age * 6f);
            _glow.localScale = Vector3.one * (1f + 0.04f * Mathf.Sin(_age * 2f));
            if (_hideAt > 0f && UITime.Now >= _hideAt) Hide();
        }
    }
}
