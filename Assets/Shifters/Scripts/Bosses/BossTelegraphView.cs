// =====================================================================================
//  SHIFTERS - BossTelegraphView.cs   [BOSSES module - B3 channels 2 + 3]
//  The floor decal of a telegraph: a circle (outline ring + red fill disc that grows over the windup)
//  or a lane (outline + fill growing along its length), plus the SPD-view chip ("SLAM IN 1.2 s") as a
//  Speed-perspective Revealable label. Built by BossKit.TelegraphDecal; pooled by BossAttackRunner.
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public sealed class BossTelegraphView : MonoBehaviour
    {
        public Transform Disc, Ring, Lane, LaneFill;
        public Revealable Chip;
        public BossTelegraph Telegraph { get; private set; }

        float _pulse;

        // polish 11-17: the fill disc uses the lamp material (LightRed, HDR emission 1.9x). That reads well on a 1.5-3 m
        // slam circle, but a Sweep / Shockwave fills a 10-24 m circle and the bloom blew it out into a white disc over
        // the whole arena (CH13 P3 kazoo sweep). Circles wider than WideFillRadius get a translucent red tint instead.
        const float WideFillRadius = 4.5f;
        Renderer _discR;
        Material _discLamp, _discWide;
        bool _discIsWide;

        void SetWideFill(bool wide)
        {
            if (Disc == null) return;
            if (_discR == null) { _discR = Disc.GetComponent<Renderer>(); if (_discR == null) return; _discLamp = _discR.sharedMaterial; }
            if (wide == _discIsWide) return;
            _discIsWide = wide;
            if (wide && _discWide == null && _discLamp != null)
            {
                var c = UIColors.Hex(0xFF4D5E);
                c.a = 0.34f;
                _discWide = new Material(_discLamp) { name = "bk_tel_fill_wide", hideFlags = HideFlags.DontSave };
                _discWide.SetColor(ShaderIds.BaseColor, c);
                MaterialLibrary.SetEmission(_discWide, new Color(c.r, c.g, c.b) * 0.35f);
                MaterialLibrary.SetTransparent(_discWide, true);
            }
            _discR.sharedMaterial = wide && _discWide != null ? _discWide : _discLamp;
        }

        void OnDestroy() { if (_discWide != null) Destroy(_discWide); }

        public void Show(BossTelegraph t)
        {
            Telegraph = t;
            gameObject.SetActive(t != null);
            if (t == null) return;
            if (Chip != null)
            {
                Chip.DynamicLabel = _ => Telegraph != null ? Telegraph.Chip : "";
                var pm = PerspectiveManager.Instance;
                Chip.SetRevealed(pm != null && (pm.Current & PerspectiveMask.Speed) != 0);
            }
            Refresh();
        }

        public void Hide() { Telegraph = null; gameObject.SetActive(false); }

        void LateUpdate() { if (Telegraph != null) Refresh(); }

        void Refresh()
        {
            var t = Telegraph;
            bool lane = t.Kind == BossAttackKind.Charge || t.Kind == BossAttackKind.Volley;
            bool circle = !lane;
            float y = t.Origin.y + 0.03f;
            float fill = t.Stage == BossTelegraphStage.Windup ? t.Fill01 : 1f;
            _pulse += Time.deltaTime * (t.Stage == BossTelegraphStage.Windup ? 4f + 10f * fill : 18f);
            float pulse = 1f + 0.04f * Mathf.Sin(_pulse);
            if (Disc != null) Disc.gameObject.SetActive(circle);
            if (Ring != null) Ring.gameObject.SetActive(circle);
            if (Lane != null) Lane.gameObject.SetActive(lane);
            if (LaneFill != null) LaneFill.gameObject.SetActive(lane);
            if (circle)
            {
                Vector3 c; float r;
                switch (t.Kind)
                {
                    case BossAttackKind.Shockwave: case BossAttackKind.Sweep: c = t.Origin; r = t.Range; break;
                    case BossAttackKind.Spook: c = t.Target != null ? t.Target.Feet : t.Aim; r = 1.5f; break;
                    case BossAttackKind.SkateLoop: c = t.Origin; r = Mathf.Max(1f, t.Radius); break;
                    case BossAttackKind.Summon: c = t.Aim; r = 1.5f; break;
                    default: c = t.Aim; r = t.Radius; break;
                }
                transform.position = new Vector3(c.x, (t.Kind == BossAttackKind.Spook && t.Target != null ? c.y : y) + 0.03f, c.z);
                transform.rotation = Quaternion.identity;
                if (Ring != null) Ring.localScale = new Vector3(r * pulse, 1f, r * pulse);
                SetWideFill(r > WideFillRadius);
                if (Disc != null) Disc.localScale = new Vector3(Mathf.Max(0.02f, r * fill), 1f, Mathf.Max(0.02f, r * fill));
            }
            else
            {
                var a = t.Origin; var b = t.End; a.y = b.y = y;
                var d = b - a; float len = Mathf.Max(0.5f, d.magnitude);
                float w = (t.Kind == BossAttackKind.Volley ? t.Radius + 0.6f : t.Radius) * 2f;
                transform.position = a;
                transform.rotation = d.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(d.normalized, Vector3.up) : Quaternion.identity;
                if (Lane != null) { Lane.localPosition = new Vector3(0f, 0f, len * 0.5f); Lane.localScale = new Vector3(w * pulse, 1f, len); }
                if (LaneFill != null) { LaneFill.localPosition = new Vector3(0f, 0.005f, len * fill * 0.5f); LaneFill.localScale = new Vector3(w * 0.8f, 1f, Mathf.Max(0.02f, len * fill)); }
            }
            if (Chip != null) Chip.LabelOffset = new Vector3(0f, 1.4f, 0f);
        }
    }
}
