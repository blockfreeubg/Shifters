// =====================================================================================
//  SHIFTERS - PortraitStudio.cs   [CONTRACT API - implementation owned by the VISUALS agent]
//  Renders character portraits for the HUD and the switcher wheel. Builds visual-only clones
//  of the team in an isolated "studio" far below the level (y = -1000) with its own lights and
//  one camera per slot rendering into RenderTextures. Static snapshots for the HUD; live
//  rendering only while the switcher is open.
//  Mirrors team state via GameEvents.CharacterStateChanged: Downed -> KO face, Captured -> Scared,
//  Lost -> desaturated + Sad. Falls back to building itself on GameEvents.TeamSpawned.
// =====================================================================================
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Shifters
{
    public class PortraitStudio : MonoBehaviour
    {
        public static PortraitStudio Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Instance = null;

        public static readonly Vector3 StudioOrigin = new Vector3(0f, -1000f, 0f);
        public const int TextureSize = 256;
        const float SlotSpacing = 25f;

        class Slot
        {
            public Transform Root;
            public CharacterPuppet Puppet;
            public Camera Cam;
            public RenderTexture RT;
            public Light Key, Rim;
            public CharacterProfile Profile;
            public Expression StateExpression = Expression.Neutral;
        }

        readonly Slot[] _slots = new Slot[3];
        bool _live, _dirty, _built;
        int _renderDelay;

        /// <summary>Find or create the scene's studio.</summary>
        public static PortraitStudio Ensure()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("[PortraitStudio]");
            return go.AddComponent<PortraitStudio>();
        }

        public bool IsLive => _live;
        public bool IsBuilt => _built;
        public CharacterPuppet GetPuppet(int slot) => slot >= 0 && slot < 3 && _slots[slot] != null ? _slots[slot].Puppet : null;

        protected virtual void Awake()
        {
            Instance = this;
            GameEvents.CharacterStateChanged += OnStateChanged;
            GameEvents.TeamSpawned += OnTeamSpawned;
        }

        protected virtual void OnDestroy()
        {
            GameEvents.CharacterStateChanged -= OnStateChanged;
            GameEvents.TeamSpawned -= OnTeamSpawned;
            Clear();
            if (Instance == this) Instance = null;
        }

        /// <summary>(Re)build studio clones for this team. Called by CharacterManager after spawning.</summary>
        public virtual void Build(TeamData team)
        {
            Clear();
            if (team == null || team.Members == null) return;
            for (int i = 0; i < 3 && i < team.Members.Length; i++)
            {
                var p = team.Members[i];
                if (p == null) continue;
                _slots[i] = MakeSlot(i, p);
            }
            _built = true;
            SetLive(_live);
            RenderAll();
            _dirty = true; _renderDelay = 1; // re-render next frame once everything settled
        }

        Slot MakeSlot(int i, CharacterProfile p)
        {
            var s = new Slot { Profile = p };
            s.Root = new GameObject("PortraitSlot_" + i).transform;
            s.Root.SetParent(transform, false);
            s.Root.position = StudioOrigin + new Vector3(i * SlotSpacing, 0f, 0f);
            s.Puppet = CharacterPuppet.Create(p, s.Root, Vector3.zero, Quaternion.identity);
            s.Puppet.UseUnscaledTime = true;

            // framing: head + shoulders, flattering 3/4 view from slightly above
            var d = s.Puppet.Rig.D;
            var headC = s.Root.InverseTransformPoint(s.Puppet.HeadCenter);
            float headH = d.HeadHalf.y * 2f + Mathf.Max(0f, d.HairTop - (d.HeadCenterY + d.HeadHalf.y)) * 0.6f;
            float frameH = Mathf.Max(headH * 2.05f, 0.62f);
            var target = headC + new Vector3(0f, -headH * 0.28f + Mathf.Max(0f, d.HairTop - (d.HeadCenterY + d.HeadHalf.y)) * 0.25f, 0f);
            const float fov = 28f;
            float dist = frameH * 0.5f / Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
            var dir = Quaternion.Euler(-7f, 27f, 0f) * Vector3.forward;
            var camGo = new GameObject("PortraitCam");
            camGo.transform.SetParent(s.Root, false);
            camGo.transform.localPosition = target + dir * dist;
            camGo.transform.localRotation = Quaternion.LookRotation(-dir, Vector3.up);
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = fov; cam.nearClipPlane = 0.05f; cam.farClipPlane = Mathf.Min(10f, dist + 3f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            Color.RGBToHSV(p.PrimaryColor, out float h, out float sat, out float v);
            cam.backgroundColor = Color.HSVToRGB(h, Mathf.Clamp01(sat * 0.55f), 0.2f);
            cam.allowHDR = false; cam.allowMSAA = true; cam.useOcclusionCulling = false;
            s.RT = new RenderTexture(TextureSize, TextureSize, 16, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { name = "Portrait_" + i, antiAliasing = 4 };
            s.RT.Create();
            cam.targetTexture = s.RT;
            cam.enabled = false;
            var urp = cam.GetUniversalAdditionalCameraData();
            if (urp != null) { urp.renderPostProcessing = false; urp.renderShadows = false; urp.antialiasing = AntialiasingMode.None; }
            s.Cam = cam;
            s.Puppet.LookTarget = camGo.transform.position;

            // key (warm, front-left-above) + rim (character tinted, behind) - small ranges, no shadows
            var camRight = Vector3.Cross(Vector3.up, -dir).normalized;
            s.Key = MakeLight(s.Root, "Key", target + dir * dist * 0.6f - camRight * 0.9f + Vector3.up * 0.9f, new Color(1f, 0.93f, 0.82f), 3.2f, 6f);
            var rimCol = Color.Lerp(p.PrimaryColor, Color.white, 0.45f);
            s.Rim = MakeLight(s.Root, "Rim", target - dir * 1.1f + camRight * 0.7f + Vector3.up * 0.6f, rimCol, 4.5f, 4f);
            return s;
        }

        static Light MakeLight(Transform parent, string name, Vector3 localPos, Color c, float intensity, float range)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point; l.color = c; l.intensity = intensity; l.range = range; l.shadows = LightShadows.None;
            return l;
        }

        void Clear()
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                var s = _slots[i];
                if (s == null) continue;
                if (s.Cam != null) s.Cam.targetTexture = null;
                if (s.RT != null) { s.RT.Release(); Kit.SafeDestroy(s.RT); }
                if (s.Root != null) Kit.SafeDestroy(s.Root.gameObject);
                _slots[i] = null;
            }
            _built = false;
        }

        /// <summary>Portrait texture for a slot (RenderTexture, 256x256). May be null before Build.</summary>
        public virtual Texture GetPortrait(int slot) => slot >= 0 && slot < 3 && _slots[slot] != null ? _slots[slot].RT : null;

        /// <summary>Live rendering on/off (switcher open). Off = textures keep their last frame.</summary>
        public virtual void SetLive(bool live)
        {
            _live = live;
            for (int i = 0; i < _slots.Length; i++) if (_slots[i] != null && _slots[i].Cam != null) _slots[i].Cam.enabled = live;
            if (!live) _dirty = true;
        }

        public virtual void SetExpression(int slot, Expression e)
        {
            var s = slot >= 0 && slot < 3 ? _slots[slot] : null;
            if (s == null || s.Puppet == null) return;
            s.Puppet.SetExpression(e);
            _dirty = true;
        }

        /// <summary>Play a gesture on a portrait puppet (visible while live).</summary>
        public void Play(int slot, VisualAction a) { var p = GetPuppet(slot); if (p != null) p.Play(a); }

        /// <summary>Re-render static portraits now (e.g. after a state change).</summary>
        public virtual void Refresh() { RenderAll(); }

        void RenderAll()
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                var s = _slots[i];
                if (s == null || s.Cam == null || s.Puppet == null) continue;
                s.Puppet.PrepareSnapshot();
                s.Cam.Render();
            }
            _dirty = false;
        }

        void LateUpdate()
        {
            if (!_dirty || _live) return;
            if (_renderDelay > 0) { _renderDelay--; return; }
            RenderAll();
        }

        void OnTeamSpawned()
        {
            if (_built) return;
            var mgr = CharacterManager.Instance;
            if (mgr == null || mgr.Team.Count == 0) return;
            var team = new TeamData { Members = new CharacterProfile[3] };
            for (int i = 0; i < 3 && i < mgr.Team.Count; i++) team.Members[i] = mgr.Team[i] != null ? mgr.Team[i].Profile : null;
            Build(team);
        }

        void OnStateChanged(ShifterCharacter c)
        {
            if (c == null || c.Slot < 0 || c.Slot >= 3) return;
            var s = _slots[c.Slot];
            if (s == null || s.Puppet == null) return;
            var pup = s.Puppet;
            switch (c.State)
            {
                case CharacterState.Downed: pup.SetExpression(Expression.KO); pup.SetDesaturated(false); pup.SetIdleStyle(0); break;
                case CharacterState.Captured: pup.SetExpression(Expression.Scared); pup.SetDesaturated(false); pup.SetIdleStyle(2); break;
                case CharacterState.Lost: pup.SetExpression(Expression.Sad); pup.SetDesaturated(true); pup.SetIdleStyle(0); break;
                default: pup.SetExpression(Expression.Neutral); pup.SetDesaturated(false); pup.SetIdleStyle(0); break;
            }
            _dirty = true;
        }
    }
}
