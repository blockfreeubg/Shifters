// =====================================================================================
//  SHIFTERS - PerspectiveLabels.cs   [owned by the PERSPECTIVE agent]
//  Pooled world-space label chips for revealed Revealables (created by PerspectiveManager).
//  Rules: labels only for revealed Revealables with a non-empty label; while Focused (after a
//  scan pulse) every label within FocusRange is fully visible, otherwise only within
//  LabelRange (12 m from the viewer) and fading out with distance. Billboarded to the camera,
//  near-constant screen size (clamped), decluttered in screen space (nearer labels keep their
//  spot, farther overlapping ones stack upward) and rendered PULLED toward the camera along the
//  view ray (same screen size), so characters/props between the camera and the anchor never
//  cover a label. Text refresh max 4x/s.
// =====================================================================================
using System.Collections.Generic;
using UnityEngine;

namespace Shifters
{
    public class PerspectiveLabels : MonoBehaviour
    {
        public const int MaxChips = 40;
        public const float LabelRange = 12f, FadeStart = 7f, FocusRange = 70f;
        const float ScalePerMeter = 0.075f, MinScale = 0.4f, MaxScale = 2.4f, TextRefresh = 0.25f;
        /// <summary>Chips are drawn this far from the camera (+ a little per depth rank) when their anchor is farther.</summary>
        const float PullDistance = 2.2f;

        struct Candidate { public Revealable R; public Vector3 Pos; public float Alpha, CamDist; }

        readonly List<PerspectiveLabelChip> _pool = new List<PerspectiveLabelChip>();
        readonly Dictionary<Revealable, PerspectiveLabelChip> _assigned = new Dictionary<Revealable, PerspectiveLabelChip>();
        readonly Dictionary<PerspectiveLabelChip, int> _touched = new Dictionary<PerspectiveLabelChip, int>();
        readonly List<Revealable> _stale = new List<Revealable>();
        readonly Candidate[] _cands = new Candidate[MaxChips];
        readonly Rect[] _placed = new Rect[MaxChips];
        int _frameStamp;
        float _clock;
        bool _wasFocused;

        /// <summary>Number of chips currently visible.</summary>
        public int VisibleCount { get; private set; }

        /// <summary>Chip currently assigned to target (may be hidden when its text is empty) - tests/debug.</summary>
        public PerspectiveLabelChip ChipFor(Revealable target) => target != null && _assigned.TryGetValue(target, out var c) ? c : null;

        void LateUpdate()
        {
            _clock += PerspectiveStyle.UiDelta;
            _frameStamp++;
            int visible = 0;
            var pm = PerspectiveManager.Instance;
            Camera cam = ShiftersCamera.Instance != null && ShiftersCamera.Instance.isActiveAndEnabled && ShiftersCamera.Instance.Cam != null ? ShiftersCamera.Instance.Cam : Camera.main;
            if (pm != null && cam != null && pm.Current != PerspectiveMask.None)
            {
                bool focused = pm.Focused;
                if (focused != _wasFocused) { _wasFocused = focused; foreach (var kv in _assigned) kv.Value.NextRefresh = 0f; }
                int n = Collect(pm, cam, focused);
                SortByDistance(n);
                var viewer = pm.Viewer;
                var camT = cam.transform;
                float tanHalf = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
                int placed = 0;
                for (int i = 0; i < n; i++)
                {
                    var c = _cands[i];
                    if (!_assigned.TryGetValue(c.R, out var chip))
                    {
                        chip = Rent();
                        if (chip == null) continue;
                        chip.Acquire();
                        _assigned[c.R] = chip;
                    }
                    _touched[chip] = _frameStamp;
                    if (_clock >= chip.NextRefresh)
                    {
                        chip.NextRefresh = _clock + TextRefresh;
                        chip.SetText(c.R.GetLabel(viewer), PerspectiveStyle.FirstShared(c.R.Category, pm.Current));
                    }
                    if (string.IsNullOrEmpty(chip.CurrentText)) continue; // assigned but empty (hidden) - re-evaluated 4x/s

                    float s = Mathf.Clamp(c.CamDist * ScalePerMeter, MinScale, MaxScale) * Mathf.Lerp(0.85f, 1f, c.Alpha);
                    Vector3 pos = Declutter(cam, camT, c.Pos, s, chip.Width, tanHalf, ref placed);
                    // pull toward the camera: same screen size, nearer labels slightly in front of farther ones
                    Vector3 ray = pos - camT.position;
                    float dist = ray.magnitude;
                    float pull = PullDistance + 0.03f * i;
                    if (dist > pull + 0.01f) { float k = pull / dist; pos = camT.position + ray * k; s *= k; }
                    var t = chip.transform;
                    t.SetPositionAndRotation(pos, camT.rotation);
                    t.localScale = new Vector3(s, s, s);
                    chip.SetAlpha(c.Alpha);
                    visible++;
                }
            }
            ReleaseUntouched();
            VisibleCount = visible;
        }

        int Collect(PerspectiveManager pm, Camera cam, bool focused)
        {
            var viewer = pm.Viewer;
            Vector3 camPos = cam.transform.position, camFwd = cam.transform.forward;
            Vector3 origin = viewer != null ? viewer.Center : camPos;
            int n = 0;
            var all = Revealable.All;
            for (int i = 0; i < all.Count && n < _cands.Length; i++)
            {
                var r = all[i];
                if (r == null || !r.IsRevealed || !r.HasLabel) continue;
                Vector3 pos = r.LabelPosition;
                Vector3 fromCam = pos - camPos;
                float along = Vector3.Dot(fromCam, camFwd);
                if (along < 0.3f) continue; // behind the camera
                float d = Vector3.Distance(origin, pos);
                float alpha;
                if (focused || r.LabelAlwaysVisible) alpha = d <= FocusRange ? 1f : 0f;
                else alpha = d <= FadeStart ? 1f : 1f - Mathf.Clamp01((d - FadeStart) / (LabelRange - FadeStart));
                if (r.ShowOnlyWhenRevealed) alpha *= Mathf.Clamp01(r.RevealAmount * 1.5f);
                if (alpha <= 0.02f) continue;
                _cands[n++] = new Candidate { R = r, Pos = pos, Alpha = alpha, CamDist = fromCam.magnitude };
            }
            return n;
        }

        void SortByDistance(int n)
        {
            for (int i = 1; i < n; i++) // insertion sort, nearest first (n <= 40, allocation-free)
            {
                var key = _cands[i];
                int j = i - 1;
                while (j >= 0 && _cands[j].CamDist > key.CamDist) { _cands[j + 1] = _cands[j]; j--; }
                _cands[j + 1] = key;
            }
        }

        /// <summary>Moves a chip up (in screen space) until it no longer overlaps already placed nearer chips.</summary>
        Vector3 Declutter(Camera cam, Transform camT, Vector3 pos, float scale, float width, float tanHalf, ref int placed)
        {
            Vector3 sp = cam.WorldToScreenPoint(pos);
            if (sp.z <= 0.01f) return pos;
            float pxPerUnit = cam.pixelHeight / (2f * sp.z * tanHalf);
            float h = PerspectiveLabelChip.ChipHeight * scale * pxPerUnit;
            float w = width * scale * pxPerUnit;
            var rect = new Rect(sp.x - w * 0.5f, sp.y - h * 0.5f, w, h);
            float shift = 0f;
            for (int iter = 0; iter < 8; iter++)
            {
                bool moved = false;
                for (int k = 0; k < placed; k++)
                {
                    if (!_placed[k].Overlaps(rect)) continue;
                    float dy = _placed[k].yMax + 2f - rect.y;
                    rect.y += dy;
                    shift += dy;
                    moved = true;
                }
                if (!moved) break;
            }
            if (placed < _placed.Length) _placed[placed++] = rect;
            return shift > 0f ? pos + camT.up * (shift / pxPerUnit) : pos;
        }

        void ReleaseUntouched()
        {
            _stale.Clear();
            foreach (var kv in _assigned)
                if (kv.Key == null || !_touched.TryGetValue(kv.Value, out int stamp) || stamp != _frameStamp) _stale.Add(kv.Key);
            for (int i = 0; i < _stale.Count; i++)
            {
                var key = _stale[i];
                if (_assigned.TryGetValue(key, out var chip)) { chip.Release(); _touched.Remove(chip); }
                _assigned.Remove(key);
            }
        }

        PerspectiveLabelChip Rent()
        {
            for (int i = 0; i < _pool.Count; i++) if (!_pool[i].InUse) return _pool[i];
            if (_pool.Count >= MaxChips) return null;
            var c = PerspectiveLabelChip.Create(transform);
            _pool.Add(c);
            return c;
        }

        /// <summary>Hide every chip (scene change, menus).</summary>
        public void ReleaseAll()
        {
            foreach (var kv in _assigned) if (kv.Value != null) kv.Value.Release();
            _assigned.Clear();
            _touched.Clear();
            VisibleCount = 0;
        }
    }
}
