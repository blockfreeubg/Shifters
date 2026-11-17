// =====================================================================================
//  SHIFTERS - VisionConeMesh.cs   (RENDERING, campaign wave)
//  Readable vision cones for security cameras and guard bots: a hologram floor FAN (sector on the floor under the eye,
//  optionally clipped by walls with raycasts) plus an optional faint VOLUME cone from the lens. States color it:
//  Idle white -> Spotting orange -> Alarm red (or any custom color). Purely visual: detection is the enemy's own code.
//  Usage: var cone = VisionConeMesh.Create(eyeTransform, Vector3.zero, null, range, fovDeg, VisionConeMesh.Style.FloorFan);
//         cone.SetState(VisionConeMesh.State.Spotting);   // the fan follows the eye's yaw every frame
// =====================================================================================
using UnityEngine;
using UnityEngine.Rendering;

namespace Shifters
{
    [DisallowMultipleComponent]
    public sealed class VisionConeMesh : MonoBehaviour
    {
        public enum Style { FloorFan, Volume, Both }
        public enum State { Idle, Spotting, Alarm, Off }

        public static readonly Color IdleColor = new Color(0.92f, 0.96f, 1f, 0.22f);
        public static readonly Color SpottingColor = new Color(1f, 0.6f, 0.12f, 0.32f);
        public static readonly Color AlarmColor = new Color(1f, 0.16f, 0.18f, 0.4f);

        [Tooltip("Sight range (m).")] public float Range = 12f;
        [Tooltip("Full horizontal field of view (deg).")] public float Fov = 40f;
        public Style ConeStyle = Style.FloorFan;
        [Tooltip("Clip the floor fan against walls (one raycast per segment, every frame the eye moves).")]
        public bool Occlude = true;
        [Tooltip("World height of the floor fan; NaN = raycast down from the eye at start.")]
        public float FloorY = float.NaN;
        [Tooltip("Lift above the floor (avoid z-fighting).")] public float FloorLift = 0.04f;
        public int Segments = 20;
        public LayerMask OcclusionMask = ~0;

        State _state = State.Idle;
        Color? _custom;
        MeshFilter _fanMf, _volMf;
        MeshRenderer _fanMr, _volMr;
        Mesh _fan, _vol;
        Vector3[] _fanVerts, _fanNormals; Color[] _fanCols; int[] _fanTris;
        Vector3 _lastPos; Quaternion _lastRot; float _lastRange, _lastFov;
        bool _built;

        public State CurrentState => _state;

        /// <summary>Creates a cone on a child of parent at local pos/rot (the eye). Edit-mode safe (scene builders).</summary>
        public static VisionConeMesh Create(Transform parent, Vector3 pos, Quaternion? rot, float range, float fovDeg, Style style = Style.FloorFan)
        {
            var go = new GameObject("VisionCone");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot ?? Quaternion.identity;
            var v = go.AddComponent<VisionConeMesh>();
            v.Range = range; v.Fov = fovDeg; v.ConeStyle = style;
            v.Build();
            return v;
        }

        public void SetState(State s)
        {
            if (_state == s && _custom == null) return;
            _state = s; _custom = null;
            ApplyMaterial();
        }

        /// <summary>Custom tint (alpha = opacity). SetState clears it.</summary>
        public void SetColor(Color c) { _custom = c; ApplyMaterial(); }

        public void SetShape(float range, float fovDeg) { Range = range; Fov = fovDeg; Rebuild(); }

        void Awake() { if (!_built) Build(); }

        void Build()
        {
            _built = true;
            if (ConeStyle != Style.Volume) EnsurePart("Fan", out _fanMf, out _fanMr, ref _fan);
            if (ConeStyle != Style.FloorFan) EnsurePart("Volume", out _volMf, out _volMr, ref _vol);
            ApplyMaterial();
            Rebuild();
        }

        void EnsurePart(string n, out MeshFilter mf, out MeshRenderer mr, ref Mesh mesh)
        {
            var t = transform.Find(n);
            if (t == null) { t = new GameObject(n).transform; t.SetParent(transform, false); }
            mf = t.GetComponent<MeshFilter>(); if (mf == null) mf = t.gameObject.AddComponent<MeshFilter>();
            mr = t.GetComponent<MeshRenderer>(); if (mr == null) mr = t.gameObject.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = ShadowCastingMode.Off; mr.receiveShadows = false;
            mr.lightProbeUsage = LightProbeUsage.Off; mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            mesh = new Mesh { name = "VisionCone_" + n };
            mesh.MarkDynamic();
            mf.sharedMesh = mesh;
        }

        Color CurrentColor()
        {
            if (_custom.HasValue) return _custom.Value;
            switch (_state)
            {
                case State.Spotting: return SpottingColor;
                case State.Alarm: return AlarmColor;
                default: return IdleColor;
            }
        }

        void ApplyMaterial()
        {
            bool off = _state == State.Off && _custom == null;
            var c = CurrentColor();
            if (_fanMr != null) { _fanMr.enabled = !off; _fanMr.sharedMaterial = MaterialLibrary.Hologram(c, true); }
            if (_volMr != null) { _volMr.enabled = !off; _volMr.sharedMaterial = MaterialLibrary.Hologram(new Color(c.r, c.g, c.b, c.a * 0.35f), true); }
        }

        void LateUpdate()
        {
            if (_state == State.Off && _custom == null) return;
            var t = transform;
            if (t.position == _lastPos && t.rotation == _lastRot && Range == _lastRange && Fov == _lastFov) return;
            Rebuild();
        }

        /// <summary>Rebuilds the meshes from the current transform (called automatically when the eye moves).</summary>
        public void Rebuild()
        {
            var t = transform;
            _lastPos = t.position; _lastRot = t.rotation; _lastRange = Range; _lastFov = Fov;
            int seg = Mathf.Clamp(Segments, 4, 64);
            if (_fan != null) BuildFan(seg);
            if (_vol != null) BuildVolume(seg);
        }

        void BuildFan(int seg)
        {
            var t = transform;
            Vector3 eye = t.position;
            if (float.IsNaN(FloorY))
            {
                FloorY = Physics.Raycast(eye, Vector3.down, out var hit, 30f, OcclusionMask, QueryTriggerInteraction.Ignore) ? hit.point.y : eye.y - 2.5f;
            }
            float y = FloorY + FloorLift;
            var fwd = t.forward; fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-4f) fwd = t.up; fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
            fwd.Normalize();
            // horizontal reach: a camera tilted down sees less far along the floor
            float reach = Range;
            int nv = seg + 2;
            if (_fanVerts == null || _fanVerts.Length != nv)
            {
                _fanVerts = new Vector3[nv]; _fanNormals = new Vector3[nv]; _fanCols = new Color[nv]; _fanTris = new int[seg * 3];
                for (int i = 0; i < seg; i++) { _fanTris[i * 3] = 0; _fanTris[i * 3 + 1] = i + 1; _fanTris[i * 3 + 2] = i + 2; }
            }
            var apex = new Vector3(eye.x, y, eye.z);
            _fanVerts[0] = t.InverseTransformPoint(apex);
            _fanCols[0] = new Color(1f, 1f, 1f, 1f);
            float half = Fov * 0.5f;
            var castFrom = new Vector3(eye.x, y + 0.6f, eye.z);
            for (int i = 0; i <= seg; i++)
            {
                float a = -half + Fov * i / seg;
                var d = Quaternion.AngleAxis(a, Vector3.up) * fwd;
                float r = reach;
                if (Occlude && Physics.Raycast(castFrom, d, out var h, reach, OcclusionMask, QueryTriggerInteraction.Ignore)) r = h.distance;
                _fanVerts[i + 1] = t.InverseTransformPoint(apex + d * r);
                _fanCols[i + 1] = new Color(1f, 1f, 1f, 0.35f);
            }
            var up = t.InverseTransformDirection(Vector3.up);
            for (int i = 0; i < nv; i++) _fanNormals[i] = up;
            _fan.Clear();
            _fan.vertices = _fanVerts;
            _fan.normals = _fanNormals;
            _fan.colors = _fanCols;
            _fan.triangles = _fanTris;
            _fan.RecalculateBounds();
        }

        void BuildVolume(int seg)
        {
            if (_vol.vertexCount > 0) return;   // local-space cone: built once (it rotates with the eye)
            int ring = seg;
            var v = new Vector3[ring + 1];
            var c = new Color[ring + 1];
            var tris = new int[ring * 6];
            var n = new Vector3[ring + 1];
            n[0] = Vector3.back;
            v[0] = Vector3.zero; c[0] = Color.white;
            float rad = Mathf.Tan(Fov * 0.5f * Mathf.Deg2Rad) * Range;
            for (int i = 0; i < ring; i++)
            {
                float a = i * Mathf.PI * 2f / ring;
                v[i + 1] = new Vector3(Mathf.Cos(a) * rad, Mathf.Sin(a) * rad * 0.6f, Range);
                c[i + 1] = new Color(1f, 1f, 1f, 0f);
                n[i + 1] = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0.3f).normalized;
                tris[i * 3] = 0; tris[i * 3 + 1] = i + 1; tris[i * 3 + 2] = (i + 1) % ring + 1;
                int b = ring * 3 + i * 3;   // back faces (seen from inside the cone too)
                tris[b] = 0; tris[b + 1] = (i + 1) % ring + 1; tris[b + 2] = i + 1;
            }
            _vol.Clear();
            _vol.vertices = v; _vol.normals = n; _vol.colors = c; _vol.triangles = tris;
            _vol.RecalculateBounds();
        }

        void OnDestroy()
        {
            if (_fan != null) { if (Application.isPlaying) Destroy(_fan); else DestroyImmediate(_fan); }
            if (_vol != null) { if (Application.isPlaying) Destroy(_vol); else DestroyImmediate(_vol); }
        }
    }
}
