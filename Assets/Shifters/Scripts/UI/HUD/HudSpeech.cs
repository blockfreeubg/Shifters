// =====================================================================================
//  SHIFTERS - HudSpeech.cs   (UI agent)
//  Pooled speech bubbles over characters (GameEvents.Speech): world -> screen every frame,
//  border + tail in the character color, 2.5 s, max one bubble per character, edge-clamped.
// =====================================================================================
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Shifters
{
    public class HudSpeech : MonoBehaviour
    {
        public const float Lifetime = 2.5f;
        const int PoolSize = 6;

        class Bubble
        {
            public RectTransform Rt;
            public CanvasGroup Group;
            public TextMeshProUGUI Text;
            public Image Border, Tail;
            public ShifterCharacter Who;
            public float Until;
            public bool Active;
        }

        RectTransform _rt;
        readonly List<Bubble> _pool = new List<Bubble>(PoolSize);

        public int ActiveCount { get { int n = 0; foreach (var b in _pool) if (b.Active) n++; return n; } }

        public static HudSpeech Create(Transform parent)
        {
            var rt = UIFactory.Rect("Speech", parent);
            UIFactory.Stretch(rt);
            var s = rt.gameObject.AddComponent<HudSpeech>();
            s._rt = rt;
            for (int i = 0; i < PoolSize; i++) s._pool.Add(s.MakeBubble(i));
            return s;
        }

        Bubble MakeBubble(int i)
        {
            var b = new Bubble();
            b.Rt = UIFactory.Rect("Bubble" + i, _rt, new Vector2(0f, 0f), Vector2.zero, new Vector2(240f, 70f), new Vector2(0.5f, 0f));
            b.Group = UIFactory.Group(b.Rt.gameObject);
            var sh = UIFactory.Image(b.Rt, "Shadow", UISprites.Shadow(16, 18), new Color(0f, 0f, 0f, 0.45f));
            UIFactory.Expand(sh.rectTransform, 16f, new Vector2(0f, -5f));
            b.Tail = UIFactory.Image(b.Rt, "Tail", UISprites.Triangle, Color.white);
            var trt = b.Tail.rectTransform;
            trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 0f); trt.pivot = new Vector2(0.5f, 1f);
            trt.sizeDelta = new Vector2(30f, 22f); trt.anchoredPosition = new Vector2(0f, 4f);
            trt.localEulerAngles = new Vector3(0f, 0f, 180f);
            b.Border = UIFactory.Fill(b.Rt, "Border", UISprites.Rounded(18), Color.white);
            var face = UIFactory.Fill(b.Rt, "Face", UISprites.Rounded(15), UIColors.Hex(0xF7F8FA));
            UIFactory.Stretch(face.rectTransform, 4f, 4f, 4f, 4f);
            b.Text = UIFactory.Text(b.Rt, "Text", "", FontLibrary.Body, 24f, UIColors.Hex(0x161B22), TextAlignmentOptions.Center, true);
            UIFactory.Stretch(b.Text.rectTransform, 16f, 16f, 8f, 10f);
            b.Rt.gameObject.SetActive(false);
            return b;
        }

        void OnEnable() => GameEvents.Speech += Say;
        void OnDisable() => GameEvents.Speech -= Say;

        public void Clear()
        {
            foreach (var b in _pool) { b.Active = false; b.Who = null; b.Rt.gameObject.SetActive(false); }
        }

        public void Say(ShifterCharacter who, string text)
        {
            if (who == null || string.IsNullOrEmpty(text)) return;
            Bubble b = null;
            foreach (var x in _pool) if (x.Active && x.Who == who) { b = x; break; }
            if (b == null) foreach (var x in _pool) if (!x.Active) { b = x; break; }
            if (b == null)
            {
                float oldest = float.MaxValue;
                foreach (var x in _pool) if (x.Until < oldest) { oldest = x.Until; b = x; }
            }
            if (b == null) return;
            b.Who = who;
            b.Active = true;
            b.Until = UITime.Now + Lifetime;
            b.Text.text = text;
            var pref = UIFactory.Preferred(b.Text, text, 330f);
            float w = Mathf.Clamp(pref.x + 40f, 110f, 380f);
            float h = Mathf.Max(56f, UIFactory.Preferred(b.Text, text, w - 32f).y + 26f);
            b.Rt.sizeDelta = new Vector2(w, h);
            var col = who.Color;
            b.Border.color = col;
            b.Tail.color = col;
            b.Rt.gameObject.SetActive(true);
            b.Rt.SetAsLastSibling();
            UITween.Fade(b.Group, 1f, 0.12f, Ease.OutQuad, 0f, 0f);
            UITween.Scale(b.Rt, 1f, 0.32f, Ease.OutBack, 0f, 0.4f);
            Position(b);
        }

        void LateUpdate()
        {
            for (int i = 0; i < _pool.Count; i++)
            {
                var b = _pool[i];
                if (!b.Active) continue;
                if (b.Who == null || UITime.Now > b.Until + 0.25f) { b.Active = false; b.Rt.gameObject.SetActive(false); continue; }
                if (UITime.Now > b.Until && b.Group.alpha > 0.99f) UITween.Fade(b.Group, 0f, 0.2f);
                Position(b);
            }
        }

        void Position(Bubble b)
        {
            var cam = Camera.main;
            if (cam == null || b.Who == null) { b.Group.alpha = 0f; return; }
            Vector3 world = b.Who.HeadTop + Vector3.up * 0.25f;
            Vector3 sp = cam.WorldToScreenPoint(world);
            bool behind = sp.z < 0.1f;
            b.Rt.gameObject.SetActive(!behind);
            if (behind) return;
            Vector2 local;
            var canvasRt = _rt;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, sp, null, out local)) return;
            var size = canvasRt.rect.size;
            // local is relative to the center (pivot 0.5); bubble anchors are bottom-left.
            Vector2 pos = local + size * 0.5f + new Vector2(0f, 18f);
            float hw = b.Rt.sizeDelta.x * 0.5f;
            pos.x = Mathf.Clamp(pos.x, hw + 16f, size.x - hw - 16f);
            pos.y = Mathf.Clamp(pos.y, 16f, size.y - b.Rt.sizeDelta.y - 16f);
            b.Rt.anchoredPosition = pos;
        }
    }
}
