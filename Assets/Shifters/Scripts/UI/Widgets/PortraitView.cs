// =====================================================================================
//  SHIFTERS - PortraitView.cs   (UI agent)
//  Character portrait: PortraitStudio RenderTexture in a rounded/circular mask, with a
//  colored-circle + initials fallback when no portrait texture is available.
// =====================================================================================
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Shifters
{
    public class PortraitView : MonoBehaviour
    {
        RawImage _raw;
        Image _bg, _frame;
        TextMeshProUGUI _initials;
        int _slot = -1;
        CharacterProfile _profile;
        float _nextPoll;
        bool _grey;

        public int Slot => _slot;
        public bool HasTexture => _raw != null && _raw.texture != null && _raw.enabled;

        /// <summary>Create a portrait. circle = circular mask, else rounded square.</summary>
        public static PortraitView Create(Transform parent, float size, bool circle)
        {
            var rt = UIFactory.Rect("Portrait", parent);
            rt.sizeDelta = new Vector2(size, size);
            var pv = rt.gameObject.AddComponent<PortraitView>();
            Sprite shape = circle ? UISprites.Circle : UISprites.Rounded(Mathf.Clamp(Mathf.RoundToInt(size * 0.14f), 4, 40));
            pv._bg = UIFactory.Fill(rt, "Mask", shape, Color.gray);
            var mask = pv._bg.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = true;
            var rawRt = UIFactory.Rect("Texture", pv._bg.transform);
            UIFactory.Stretch(rawRt);
            pv._raw = rawRt.gameObject.AddComponent<RawImage>();
            pv._raw.raycastTarget = false;
            pv._raw.enabled = false;
            pv._initials = UIFactory.Text(pv._bg.transform, "Initials", "?", FontLibrary.Heavy, size * 0.42f, UIColors.Hex(0x12161C), TextAlignmentOptions.Center);
            UIFactory.Stretch(pv._initials.rectTransform);
            pv._frame = UIFactory.Fill(rt, "Frame", circle ? UISprites.Ring(8) : UISprites.RoundedOutline(Mathf.Clamp(Mathf.RoundToInt(size * 0.14f), 4, 40), 3), new Color(1f, 1f, 1f, 0.9f));
            return pv;
        }

        public void Bind(int slot, CharacterProfile profile)
        {
            _slot = slot;
            _profile = profile;
            if (profile != null)
            {
                _initials.text = UIFactory.Initials(profile.Name);
                _frame.color = profile.PrimaryColor;
            }
            Refresh();
        }

        public void SetFrameColor(Color c) { if (_frame != null) _frame.color = c; }

        /// <summary>Grey out (unavailable characters).</summary>
        public void SetGrey(bool grey)
        {
            _grey = grey;
            Refresh();
        }

        public void Refresh()
        {
            if (_raw == null) return;
            Texture tex = null;
            if (_slot >= 0 && PortraitStudio.Instance != null) tex = PortraitStudio.Instance.GetPortrait(_slot);
            _raw.texture = tex;
            _raw.enabled = tex != null;
            _raw.color = _grey ? new Color(0.45f, 0.45f, 0.48f, 1f) : Color.white;
            Color baseC = _profile != null ? _profile.PrimaryColor : Color.gray;
            if (_grey) baseC = Color.Lerp(baseC, new Color(0.3f, 0.3f, 0.32f), 0.7f);
            _bg.color = tex != null ? UIColors.Hex(0x1B222C) : baseC;
            _initials.enabled = tex == null;
            _initials.color = UIFactory.ContrastText(baseC);
        }

        void Update()
        {
            if (UITime.Now < _nextPoll) return;
            _nextPoll = UITime.Now + 0.5f;
            if (_slot < 0) return;
            Texture tex = PortraitStudio.Instance != null ? PortraitStudio.Instance.GetPortrait(_slot) : null;
            if (tex != _raw.texture) Refresh();
        }
    }
}
