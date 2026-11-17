// =====================================================================================
//  SHIFTERS - ThreatMarker.cs   [perspective_level kit, added by SEAMS for the campaign]
//  IQ-only threat tag ("THREAT: 9 M") floating above a target (stalker, guard, boss). Always visible
//  while the active intern sees the mask (default Intellect), drawn THROUGH walls (ZTest Always on its
//  own material instance at runtime), billboarded, constant on-screen size. "{m}" in the text becomes
//  the distance in meters to the active intern (text re-laid out only when that integer changes).
//  Build with PerspectiveKit.ThreatLabel(target, text). Lives as a child of the target (dies with it).
// =====================================================================================
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace Shifters
{
    public class ThreatMarker : MonoBehaviour
    {
        public Transform Target;
        [Tooltip("{m} = meters to the active intern.")] public string Template = "THREAT: {m} M";
        [Tooltip("World height above the target's pivot.")] public float Height = 2.6f;
        public PerspectiveMask Mask = PerspectiveMask.Intellect;
        [Tooltip("False = visible to everyone (debug / story).")] public bool RequirePerspective = true;
        [Tooltip("World line height at 10 m; grows with distance so it stays readable.")] public float BaseScale = 1f;
        public TextMeshPro Text;

        public bool IsShown { get; private set; }
        public int ShownMeters { get; private set; } = -1;
        public string ShownText => Text != null ? Text.text : "";

        int _m = int.MinValue;
        bool _overlayDone;
        string _lastTemplate;

        void Start() => MakeOverlay();

        void MakeOverlay()
        {
            if (_overlayDone || Text == null || !Application.isPlaying) return;
            _overlayDone = true;
            var mat = Text.fontMaterial; // runtime instance (never the shared font material)
            if (mat == null) return;
            mat.SetFloat("unity_GUIZTestMode", (float)CompareFunction.Always);
            if (mat.HasProperty("_ZTestMode")) mat.SetFloat("_ZTestMode", (float)CompareFunction.Always);
            mat.renderQueue = 4000;
        }

        /// <summary>The visibility rule (also used by tests): target alive in the hierarchy and (if required) the mask is seen.</summary>
        public bool ShouldShow => Target != null && Target.gameObject.activeInHierarchy && (!RequirePerspective || PerspectiveManager.Sees(Mask));

        void LateUpdate()
        {
            if (Text == null) return;
            bool show = ShouldShow;
            if (show != IsShown) { IsShown = show; Text.enabled = show; }
            if (!show) return;
            MakeOverlay();

            Vector3 pos = Target.position + Vector3.up * Height;
            transform.position = pos;
            var cam = Camera.main;
            float dist = 10f;
            if (cam != null)
            {
                Vector3 d = pos - cam.transform.position;
                dist = Mathf.Max(0.5f, d.magnitude);
                if (d.sqrMagnitude > 1e-4f) transform.rotation = Quaternion.LookRotation(d, Vector3.up);
            }
            float s = BaseScale * Mathf.Clamp(dist / 10f, 0.6f, 6f);
            var parent = transform.parent;
            Vector3 ps = parent != null ? parent.lossyScale : Vector3.one;
            transform.localScale = new Vector3(s / Mathf.Max(1e-3f, Mathf.Abs(ps.x)), s / Mathf.Max(1e-3f, Mathf.Abs(ps.y)), s / Mathf.Max(1e-3f, Mathf.Abs(ps.z)));

            int m = -1;
            var cm = CharacterManager.Instance;
            if (cm != null && cm.Active != null) m = Mathf.RoundToInt(Vector3.Distance(cm.Active.transform.position, Target.position));
            if (m != _m || !ReferenceEquals(_lastTemplate, Template))
            {
                _m = m;
                _lastTemplate = Template;
                ShownMeters = m;
                string t = Template ?? "";
                Text.text = t.IndexOf("{m}") >= 0 ? t.Replace("{m}", m >= 0 ? m.ToString() : "?") : t;
            }
        }
    }
}
