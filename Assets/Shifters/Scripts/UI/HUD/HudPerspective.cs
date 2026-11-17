// =====================================================================================
//  SHIFTERS - HudPerspective.cs   (UI agent)
//  Top-right perspective chips ("STRENGTH VIEW", "SPEED VIEW", "IQ VIEW", "WEAPON VIEW")
//  in the perspective colors; flash on change. Listens: PerspectiveChanged (+ CharacterSwitched
//  as a fallback when no PerspectiveManager raises events).
// =====================================================================================
using System.Collections.Generic;
using UnityEngine;

namespace Shifters
{
    public class HudPerspective : MonoBehaviour
    {
        static readonly PerspectiveMask[] Bits = { PerspectiveMask.Strength, PerspectiveMask.Speed, PerspectiveMask.Intellect, PerspectiveMask.Weapon };
        RectTransform _rt;
        readonly List<RectTransform> _chips = new List<RectTransform>();
        PerspectiveMask _mask = (PerspectiveMask)(-1);

        public PerspectiveMask Mask => _mask;
        public int ChipCount => _chips.Count;

        public static HudPerspective Create(Transform parent)
        {
            var rt = UIFactory.Rect("Perspective", parent, new Vector2(1f, 1f), new Vector2(-26f, -26f), new Vector2(360f, 200f), new Vector2(1f, 1f));
            var p = rt.gameObject.AddComponent<HudPerspective>();
            p._rt = rt;
            return p;
        }

        void OnEnable()
        {
            GameEvents.PerspectiveChanged += OnPerspective;
            GameEvents.CharacterSwitched += OnSwitched;
        }

        void OnDisable()
        {
            GameEvents.PerspectiveChanged -= OnPerspective;
            GameEvents.CharacterSwitched -= OnSwitched;
        }

        public void Clear() { Set(PerspectiveMask.None, false); _mask = (PerspectiveMask)(-1); }

        void OnSwitched(ShifterCharacter prev, ShifterCharacter next)
        {
            if (next != null && PerspectiveManager.Instance == null) Set(next.Perspectives, true);
        }

        void OnPerspective(PerspectiveMask m, ShifterCharacter viewer) => Set(m, true);

        public static string LabelFor(PerspectiveMask bit) =>
            bit == PerspectiveMask.Strength ? "STRENGTH VIEW" : bit == PerspectiveMask.Speed ? "SPEED VIEW" :
            bit == PerspectiveMask.Intellect ? "IQ VIEW" : "WEAPON VIEW";

        public void Set(PerspectiveMask m, bool flash)
        {
            if (m == _mask) return;
            _mask = m;
            for (int i = 0; i < _chips.Count; i++) if (_chips[i] != null) Destroy(_chips[i].gameObject);
            _chips.Clear();
            float y = 0f;
            int n = 0;
            foreach (var bit in Bits)
            {
                if ((m & bit) == 0) continue;
                var color = UIColors.ForPerspective(bit);
                var chip = UIFactory.Chip(_rt, UIIcons.ForPerspective(bit), LabelFor(bit), color, 40f, n == 0);
                chip.anchorMin = chip.anchorMax = new Vector2(1f, 1f); chip.pivot = new Vector2(1f, 1f);
                chip.anchoredPosition = new Vector2(0f, y);
                y -= 48f;
                _chips.Add(chip);
                if (flash)
                {
                    UITween.Move(chip, chip.anchoredPosition, 0.35f, Ease.OutBack, n * 0.06f, chip.anchoredPosition + new Vector2(80f, 0f));
                    UITween.Scale(chip, 1f, 0.45f, Ease.OutElastic, n * 0.06f, 1.25f);
                }
                n++;
            }
        }
    }
}
