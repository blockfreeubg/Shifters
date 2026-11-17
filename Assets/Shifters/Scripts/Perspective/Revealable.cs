// =====================================================================================
//  SHIFTERS - Revealable.cs   [CONTRACT API - visuals implemented by the PERSPECTIVE agent]
//  Add to anything a perspective should reveal. Configure in code (builders) or the Inspector.
//  Examples: heavy block (Strength, Highlight, Label "160 KG - PUSH"), hologram path tile
//  (Intellect, ShowOnlyWhenRevealed), sequence clue number (Intellect, ShowOnlyWhenRevealed),
//  shoot target (Weapon, Highlight + Label "SHOOT"), turbo route chevrons (Speed, ShowOnly).
//
//  Visuals (play mode only; edit-mode scenes keep every renderer visible for level designers):
//    * Highlight            -> pulsing _RevealColor/_RevealStrength via MaterialPropertyBlock (Shifters/StylizedLit);
//                              other shaders: MPB tint of _BaseColor toward the perspective color (+ _EmissionColor
//                              when the material has emission enabled). No material instances are ever created.
//    * ShowOnlyWhenRevealed -> renderers disabled unless revealed, quick overshoot scale pop-in (only on renderer
//                              transforms that carry no colliders, so physics never changes).
//    * Labels               -> drawn by PerspectiveLabels (pooled chips) for revealed Revealables with a label.
// =====================================================================================
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Shifters
{
    public class Revealable : MonoBehaviour
    {
        public static readonly List<Revealable> All = new List<Revealable>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => All.Clear();

        [Tooltip("Which perspectives reveal this.")] public PerspectiveMask Category = PerspectiveMask.Intellect;
        [Tooltip("Glow/tint the renderers in the perspective color when revealed.")] public bool Highlight = true;
        [Tooltip("Renderers are hidden unless revealed (clues, hologram hints, routes). Colliders unaffected.")] public bool ShowOnlyWhenRevealed;
        [Tooltip("Optional floating label text.")] public string Label = "";
        public Vector3 LabelOffset = new Vector3(0f, 1.6f, 0f);
        [Tooltip("Renderers to affect. Empty = all child renderers.")] public Renderer[] Renderers = new Renderer[0];

        [Tooltip("Peak highlight strength (0..2).")] public float HighlightStrength = 1f;
        [Tooltip("Overshoot scale pop when a ShowOnlyWhenRevealed object appears.")] public bool PopIn = true;
        [Tooltip("Label is shown even far away / unfocused (important signage).")] public bool LabelAlwaysVisible;

        /// <summary>Runtime-only dynamic label (overrides Label), evaluated for the viewer.</summary>
        [NonSerialized] public Func<ShifterCharacter, string> DynamicLabel;

        public bool IsRevealed { get; protected set; }
        public event Action<bool> RevealChanged;

        /// <summary>Animated 0..1 reveal amount (visual only).</summary>
        public float RevealAmount { get; protected set; }
        /// <summary>True when there is any label to show (static or dynamic).</summary>
        public bool HasLabel => DynamicLabel != null || !string.IsNullOrEmpty(Label);
        /// <summary>World position of the floating label.</summary>
        public Vector3 LabelPosition => transform.position + LabelOffset;
        /// <summary>Color used for the highlight (set by PerspectiveManager from the viewer mask).</summary>
        public Color RevealColor { get; protected set; }

        protected virtual void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        protected virtual void OnDisable() { All.Remove(this); }

        public string GetLabel(ShifterCharacter viewer) => DynamicLabel != null ? DynamicLabel(viewer) : Label;

        /// <summary>Called by PerspectiveManager only.</summary>
        public virtual void SetRevealed(bool revealed)
        {
            if (IsRevealed == revealed) return;
            IsRevealed = revealed;
            if (revealed) RevealColor = ColorFor(PerspectiveManager.Instance != null ? PerspectiveManager.Instance.Current : Category);
            _idle = false;
            RevealChanged?.Invoke(revealed);
        }

        /// <summary>Color of the first perspective shared by this Revealable and the viewer mask.</summary>
        public Color ColorFor(PerspectiveMask viewerMask)
        {
            var shared = PerspectiveStyle.FirstShared(Category, viewerMask);
            return PerspectiveStyle.ColorOf(shared != PerspectiveMask.None ? shared : Category);
        }

        /// <summary>Re-collect renderers (call after adding children at runtime).</summary>
        public void RefreshRenderers() { _resolved = false; Resolve(); ApplyImmediate(); }

        // =============================================================== visuals
        enum HighlightMode : byte { None, Reveal, Tint }

        struct Slot
        {
            public Renderer R;
            public HighlightMode Mode;
            public bool Scalable, HasEmission, Touched;
            public Vector3 BaseScale;
            public Color BaseColor, BaseEmission;
        }

        static readonly int RevealColorId = Shader.PropertyToID("_RevealColor");
        static readonly int RevealStrengthId = Shader.PropertyToID("_RevealStrength");
        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static MaterialPropertyBlock _mpb;

        Slot[] _slots = new Slot[0];
        bool _resolved, _idle, _renderersOn = true;
        float _pulseSeed;

        protected virtual void Start()
        {
            _pulseSeed = (transform.position.x * 0.37f + transform.position.z * 0.21f) % 6.28f;
            Resolve();
            if (PerspectiveManager.Instance != null && !IsRevealed && (PerspectiveManager.Instance.Current & Category) != 0)
                SetRevealed(true); // spawned while already in the matching view
            ApplyImmediate();
        }

        void Resolve()
        {
            if (_resolved) return;
            _resolved = true;
            var list = Renderers != null && Renderers.Length > 0 ? Renderers : GetComponentsInChildren<Renderer>(true);
            var scaled = new HashSet<Transform>();
            var slots = new List<Slot>(list.Length);
            foreach (var r in list)
            {
                if (r == null) continue;
                var s = new Slot { R = r, BaseScale = r.transform.localScale, Mode = Highlight ? ModeFor(r) : HighlightMode.None };
                if (s.Mode == HighlightMode.Tint)
                {
                    var m = r.sharedMaterial;
                    s.BaseColor = m.GetColor(BaseColorId);
                    s.HasEmission = m.HasProperty(EmissionColorId) && m.IsKeywordEnabled("_EMISSION");
                    if (s.HasEmission) s.BaseEmission = m.GetColor(EmissionColorId);
                }
                // pop scaling only on collider-free branches, never nested inside another scaled renderer
                bool nested = false;
                for (var t = r.transform.parent; t != null && t != transform.parent; t = t.parent) if (scaled.Contains(t)) { nested = true; break; }
                s.Scalable = PopIn && !nested && r.GetComponentInChildren<Collider>(true) == null && r.transform != transform;
                if (s.Scalable) scaled.Add(r.transform);
                slots.Add(s);
            }
            _slots = slots.ToArray();
        }

        static HighlightMode ModeFor(Renderer r)
        {
            if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) return HighlightMode.None;
            var m = r.sharedMaterial;
            if (m == null || m.shader == null) return HighlightMode.None;
            if (m.HasProperty("_FaceColor") || m.shader.name.Contains("TextMeshPro")) return HighlightMode.None;
            if (m.HasProperty(RevealStrengthId)) return HighlightMode.Reveal;
            return m.HasProperty(BaseColorId) ? HighlightMode.Tint : HighlightMode.None;
        }

        void ApplyImmediate()
        {
            RevealAmount = IsRevealed ? 1f : 0f;
            if (ShowOnlyWhenRevealed) SetRenderersOn(IsRevealed, true);
            ApplyScale(1f);
            if (Highlight && IsRevealed) ApplyHighlight(HighlightStrength);
            _idle = !IsRevealed || !Highlight;
        }

        protected virtual void Update()
        {
            if (_idle || !_resolved) return;
            float target = IsRevealed ? 1f : 0f;
            float dt = PerspectiveStyle.UiDelta;
            float prev = RevealAmount;
            RevealAmount = Mathf.MoveTowards(RevealAmount, target, dt * (IsRevealed ? 5f : 7f));

            if (ShowOnlyWhenRevealed)
            {
                if (IsRevealed) SetRenderersOn(true, false);
                ApplyScale(IsRevealed ? EaseOutBack(RevealAmount) : Mathf.Lerp(0.6f, 1f, RevealAmount));
                if (!IsRevealed && RevealAmount <= 0f) { SetRenderersOn(false, false); ApplyScale(1f); }
            }

            if (Highlight)
            {
                float pulse = 0.72f + 0.28f * Mathf.Sin(Time.unscaledTime * 4.2f + _pulseSeed);
                // a bright flash right when revealed, then the gentle pulse
                float flash = IsRevealed ? (1f - RevealAmount) * 0.8f : 0f;
                ApplyHighlight(RevealAmount * HighlightStrength * pulse + flash);
            }

            if (Mathf.Approximately(RevealAmount, target) && Mathf.Approximately(prev, target) && (!IsRevealed || !Highlight))
            {
                if (Highlight && !IsRevealed) ApplyHighlight(0f);
                _idle = true;
            }
        }

        static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float x = t - 1f;
            return Mathf.Max(0.01f, 1f + c3 * x * x * x + c1 * x * x);
        }

        void SetRenderersOn(bool on, bool force)
        {
            if (!force && _renderersOn == on) return;
            _renderersOn = on;
            for (int i = 0; i < _slots.Length; i++) if (_slots[i].R != null) _slots[i].R.enabled = on;
        }

        void ApplyScale(float s)
        {
            if (!ShowOnlyWhenRevealed || !PopIn) return;
            for (int i = 0; i < _slots.Length; i++)
                if (_slots[i].Scalable && _slots[i].R != null) _slots[i].R.transform.localScale = _slots[i].BaseScale * s;
        }

        void ApplyHighlight(float strength)
        {
            if (_slots.Length == 0) return;
            _mpb ??= new MaterialPropertyBlock();
            var c = RevealColor;
            if (c.a <= 0f) c = ColorFor(Category);
            for (int i = 0; i < _slots.Length; i++)
            {
                ref var s = ref _slots[i];
                if (s.R == null || s.Mode == HighlightMode.None) continue;
                if (strength <= 0f && !s.Touched) continue; // never highlighted: leave the renderer batched
                s.Touched = true;
                s.R.GetPropertyBlock(_mpb);
                if (s.Mode == HighlightMode.Reveal)
                {
                    _mpb.SetColor(RevealColorId, c);
                    _mpb.SetFloat(RevealStrengthId, strength);
                }
                else
                {
                    var tint = Color.Lerp(s.BaseColor, c, Mathf.Clamp01(strength * 0.55f));
                    tint.a = s.BaseColor.a;
                    _mpb.SetColor(BaseColorId, tint);
                    if (s.HasEmission) _mpb.SetColor(EmissionColorId, s.BaseEmission + c * (strength * 1.2f));
                }
                s.R.SetPropertyBlock(_mpb);
            }
        }
    }
}
