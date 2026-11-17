// =====================================================================================
//  SHIFTERS - TShirtWrap.cs   [owned by the WEAPONS agent]
//  WRAPPED: a T-shirt pulled over a bot's head for 4 s (cosmetic; the blindness itself is the
//  enemies module's reaction to ShotInfo.Variant == TShirtCannon). Re-wrapping extends it.
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    [DisallowMultipleComponent]
    public class TShirtWrap : MonoBehaviour
    {
        public const float WrapSeconds = 4f;
        public float Until { get; private set; }
        public bool IsWrapped => Time.time < Until;

        public static TShirtWrap Wrap(Transform target, Transform head, float seconds)
        {
            if (target == null) return null;
            var w = target.GetComponent<TShirtWrap>();
            if (w == null) { w = target.gameObject.AddComponent<TShirtWrap>(); w.Build(head != null ? head : target); }
            w.Until = Mathf.Max(w.Until, Time.time + seconds);
            if (w._visual != null) w._visual.gameObject.SetActive(true);
            Fx.FloatingText(target.position + Vector3.up * 2.2f, "WRAPPED!", UIColors.Hex(0xFF4D9A), 1.2f);
            return w;
        }

        Transform _visual;

        void Build(Transform head)
        {
            var root = new GameObject("WeaponsWrap").transform;
            root.SetParent(head, false);
            root.localPosition = head == transform ? new Vector3(0f, 1.3f, 0f) : Vector3.zero;
            var sack = ProcMesh.Cached("wrap_sack", () => ProcMesh.Sphere(0.5f, 10, 7, true));
            ProcMesh.Part("Tee", root, sack, MaterialLibrary.Colored(UIColors.Hex(0xFF4D9A), 0.15f), Vector3.zero, null, new Vector3(0.75f, 0.6f, 0.75f), false);
            var tag = ProcMesh.Cached("wrap_tag", () => ProcMesh.Box(new Vector3(0.3f, 0.12f, 0.02f)));
            ProcMesh.Part("Tag", root, tag, MaterialLibrary.Unlit(Color.white, 1f), new Vector3(0f, 0f, 0.37f), null, null, false);
            _visual = root;
        }

        void Update()
        {
            if (_visual == null) return;
            bool on = IsWrapped;
            if (_visual.gameObject.activeSelf != on) _visual.gameObject.SetActive(on);
            if (on) _visual.localRotation = Quaternion.Euler(0f, Mathf.Sin(Time.time * 9f) * 12f, Mathf.Sin(Time.time * 7f) * 6f);
        }
    }
}
