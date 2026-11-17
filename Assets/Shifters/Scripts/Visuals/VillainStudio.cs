// =====================================================================================
//  SHIFTERS - VillainStudio.cs   (VISUALS module, campaign wave)
//  The ME-TV studio, 1200 m under every level: Max at THE DESK OF ONE (gold, hovering, ringing
//  phones) in front of an ME-TV backdrop, and Percy's break-room corner next door. ONE 320x240
//  RenderTexture; the camera renders ONLY while live (a broadcast / a Max or Percy line plays).
//
//  Zero-wiring use (story + ui_hud): the studio listens to GameEvents itself -
//    BroadcastChanged(on, speakerId)   -> goes live / ends, picks Max or Percy
//    StoryLineShown(line)              -> live while the line shows if the speaker is Max/Percy:
//                                         line.Face on the speaker, mouth flaps while the text types
//    StoryLineHidden()                 -> stops talking, ends the line hold
//  ui_hud shows VillainStudio.Instance.Texture (or VillainStudio.Ensure().Texture) in the PiP /
//  DialogueBox portrait. Manual control: Begin/End, SetSpeaker, SetExpression, Play, SetHat,
//  SetTalking, Say(line), RenderStill.
// =====================================================================================
using System;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Shifters
{
    public class VillainStudio : MonoBehaviour
    {
        public static VillainStudio Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Instance = null;

        public static readonly Vector3 Origin = new Vector3(0f, -1200f, 0f);
        public const int Width = 320, Height = 240;
        public const string SpeakerMax = "MAX", SpeakerPercy = "PERCY";
        static readonly Vector3 PercySetOffset = new Vector3(20f, 0f, 0f);

        /// <summary>The ONE render texture (320 x 240). Valid as soon as the studio exists.</summary>
        public RenderTexture Texture { get; private set; }
        public bool IsLive { get; private set; }
        public string Speaker { get; private set; } = SpeakerMax;
        public bool SpeakerIsPercy { get; private set; }
        public VillainPuppet Villain { get; private set; }
        public PercyPuppet Percy { get; private set; }
        public Camera Cam { get; private set; }
        /// <summary>Listen to BroadcastChanged / StoryLineShown / StoryLineHidden (default true).</summary>
        public bool AutoListen = true;
        /// <summary>Pick a fitting gesture for Max on some lines (default true).</summary>
        public bool AutoGestures = true;
        /// <summary>Raised when the live state changes (true = the RT updates every frame).</summary>
        public event Action<bool> LiveChanged;
        public VillainHat Channel { get; private set; } = VillainHat.Crown;

        int _manual; bool _broadcast, _line;
        float _talkUntil = -1f, _t;
        TextMeshPro _caption;
        Transform _maxSet, _percySet, _phones;
        readonly System.Random _rng = new System.Random(1999);
        int _lineCount;

        public static VillainStudio Ensure()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("[VillainStudio]");
            if (Application.isPlaying) DontDestroyOnLoad(go);
            return go.AddComponent<VillainStudio>();
        }

        protected virtual void Awake()
        {
            if (Instance != null && Instance != this) { Kit.SafeDestroy(gameObject); return; }
            Instance = this;
            transform.position = Origin;
            Build();
            GameEvents.BroadcastChanged += OnBroadcast;
            GameEvents.StoryLineShown += OnLine;
            GameEvents.StoryLineHidden += OnLineHidden;
        }

        protected virtual void OnDestroy()
        {
            GameEvents.BroadcastChanged -= OnBroadcast;
            GameEvents.StoryLineShown -= OnLine;
            GameEvents.StoryLineHidden -= OnLineHidden;
            if (Cam != null) Cam.targetTexture = null;
            if (Texture != null) { Texture.Release(); Kit.SafeDestroy(Texture); }
            if (Instance == this) Instance = null;
        }

        // ------------------------------------------------------------------ build
        void Build()
        {
            _maxSet = CharacterModelBuilder.Pivot("MaxSet", transform, Vector3.zero);
            _percySet = CharacterModelBuilder.Pivot("PercySet", transform, PercySetOffset);

            // ---- Max: ME-TV backdrop + THE DESK OF ONE
            var wall = MaterialLibrary.Colored(new Color(0.24f, 0.25f, 0.28f), 0.2f);
            var gold = MaterialLibrary.Colored(VillainLook.Gold, 0.75f, 0.15f);
            var dark = MaterialLibrary.Colored(new Color(0.12f, 0.12f, 0.14f), 0.4f);
            ProcMesh.Part("Backdrop", _maxSet, ProcMesh.CachedBox(new Vector3(4f, 3f, 0.05f), 0f), wall, new Vector3(0f, 1f, -1.3f), null, null, false);
            // halo of dim gold rays behind his head (Max must pop against it) + a small gold ME logo on the wall
            var rayMat = MaterialLibrary.Colored(Color.Lerp(new Color(0.24f, 0.25f, 0.28f), VillainLook.Gold, 0.42f), 0.3f);
            ProcMesh.Part("BackdropRays", _maxSet, ProcMesh.Cached("studio_rays", () =>
            {
                var b = new ProcMesh.Builder();
                for (int i = 0; i < 20; i++)
                {
                    float a = (i + 0.5f) / 20f * Mathf.PI * 2f;
                    var p = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                    b.AddBox(p * 0.95f, new Vector3(0.7f, 0.05f, 0.01f), Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg), Color.white);
                }
                return b.ToMesh("StudioRays");
            }), rayMat, new Vector3(0f, 1.2f, -1.27f), null, null, false);
            ProcMesh.Part("BackdropME", _maxSet, ProcMesh.Cached("studio_me2", () =>
            {
                var b = new ProcMesh.Builder();
                VillainModelBuilder.Letters(b, Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0f, 180f, 0f), Vector3.one * 0.14f), 0.14f);
                return b.ToMesh("StudioME");
            }), gold, new Vector3(0.62f, 1.38f, -1.24f), null, null, false);
            _overlayGold = gold;
            // desk: gold slab + front panel "ME", hover glow, one chair (behind him), phones
            ProcMesh.Part("Desk", _maxSet, ProcMesh.Cached("studio_desk", () =>
            {
                var b = new ProcMesh.Builder();
                b.AddBox(new Vector3(0f, 0.6f, 0.38f), new Vector3(1.3f, 0.05f, 0.5f), Quaternion.identity, Color.white, 0.02f);
                b.AddBox(new Vector3(0f, 0.4f, 0.6f), new Vector3(1.24f, 0.38f, 0.04f), Quaternion.identity, Color.white, 0.015f);
                b.AddBox(new Vector3(-0.6f, 0.4f, 0.38f), new Vector3(0.04f, 0.38f, 0.46f), Quaternion.identity, Color.white, 0.015f);
                b.AddBox(new Vector3(0.6f, 0.4f, 0.38f), new Vector3(0.04f, 0.38f, 0.46f), Quaternion.identity, Color.white, 0.015f);
                b.AddCylinder(new Vector3(0f, 1.15f, -0.55f), 0.03f, 1.1f, Quaternion.identity, Color.white, 8);   // chair back post (tall throne)
                b.AddSphere(new Vector3(0f, 1.72f, -0.55f), 0.06f, Quaternion.identity, Color.white, 8, 6);
                return b.ToMesh("DeskOfOne");
            }), gold, Vector3.zero, null, null, false);
            ProcMesh.Part("DeskME", _maxSet, ProcMesh.Cached("studio_deskme", () =>
            {
                var b = new ProcMesh.Builder();
                VillainModelBuilder.Letters(b, Matrix4x4.TRS(new Vector3(0f, 0.4f, 0.625f), Quaternion.Euler(0f, 180f, 0f), Vector3.one * 0.1f), 0.18f);
                return b.ToMesh("DeskME");
            }), dark, Vector3.zero, null, null, false);
            ProcMesh.Part("HoverGlow", _maxSet, ProcMesh.CachedBox(new Vector3(1.2f, 0.02f, 0.45f), 0f), MaterialLibrary.Unlit(new Color(1f, 0.8f, 0.35f), 2f), new Vector3(0f, 0.17f, 0.38f), null, null, false);
            _phones = CharacterModelBuilder.Pivot("Phones", _maxSet, Vector3.zero);
            var phoneMesh = ProcMesh.Cached("studio_phone", () =>
            {
                var b = new ProcMesh.Builder();
                b.AddBox(new Vector3(0f, 0.025f, 0f), new Vector3(0.11f, 0.05f, 0.08f), Quaternion.Euler(-12f, 0f, 0f), Color.white, 0.01f);
                b.AddCapsule(new Vector3(0f, 0.065f, 0f), 0.014f, 0.13f, Quaternion.Euler(0f, 0f, 90f), Color.white, 8, 2);
                return b.ToMesh("Phone");
            });
            for (int i = 0; i < 6; i++)
            {
                float x = (i < 3 ? -0.58f : 0.32f) + (i % 3) * 0.13f;
                ProcMesh.Part("Phone" + i, _phones, phoneMesh, i % 2 == 0 ? dark : gold, new Vector3(x, 0.625f, 0.32f + (i % 2) * 0.1f), Quaternion.Euler(0f, (i - 2.5f) * 9f, 0f), null, false);
            }
            Villain = VillainPuppet.Create(_maxSet, new Vector3(0f, 0.12f, 0f), Quaternion.identity);
            Villain.PlaySounds = false;
            Villain.UseUnscaledTime = true;

            // ---- Percy: break-room corner
            var brown = MaterialLibrary.Colored(new Color(0.42f, 0.29f, 0.18f), 0.2f);
            var tile = MaterialLibrary.Colored(new Color(0.78f, 0.74f, 0.66f), 0.3f);
            ProcMesh.Part("BreakWall", _percySet, ProcMesh.CachedBox(new Vector3(4f, 3f, 0.05f), 0f), brown, new Vector3(0f, 1f, -0.7f), null, null, false);
            ProcMesh.Part("Counter", _percySet, ProcMesh.CachedBox(new Vector3(2.4f, 0.9f, 0.7f), 0.02f), tile, new Vector3(0f, -0.45f, -0.15f), null, null, false);
            var sign = EnvKit.AddText(_percySet, "BREAK ROOM", new Vector3(0.75f, 1.75f, -0.66f), new Vector2(0.9f, 0.16f), new Color(0.95f, 0.85f, 0.65f), ShiftersFonts.Label, 0.14f);
            sign.alignment = TextAlignmentOptions.Center;
            ProcMesh.Part("Mug", _percySet, ProcMesh.Cached("studio_mug", () =>
            {
                var b = new ProcMesh.Builder();
                b.AddCylinder(new Vector3(0f, 0.05f, 0f), 0.045f, 0.1f, Quaternion.identity, Color.white, 12, true);
                b.AddTorus(new Vector3(0.05f, 0.05f, 0f), 0.025f, 0.008f, Quaternion.Euler(90f, 0f, 0f), Color.white, 10, 5);
                return b.ToMesh("Mug");
            }), MaterialLibrary.Colored(new Color(0.9f, 0.9f, 0.88f), 0.5f), new Vector3(-0.6f, 0f, 0.05f), null, null, false);
            Percy = PercyPuppet.Create(_percySet, new Vector3(0f, 0f, -0.2f));
            Percy.SteamFx = false;

            // ---- lights (no shadows) + camera + RT
            MakeLight(_maxSet, "Key", new Vector3(-1.1f, 1.9f, 1.6f), new Color(1f, 0.95f, 0.85f), 3.4f, 6f);
            MakeLight(_maxSet, "Rim", new Vector3(0.9f, 1.6f, -0.8f), new Color(1f, 0.8f, 0.4f), 4f, 4f);
            MakeLight(_percySet, "Key", new Vector3(-1f, 1.9f, 1.6f), new Color(1f, 0.9f, 0.75f), 3.2f, 6f);
            MakeLight(_percySet, "Rim", new Vector3(0.8f, 1.7f, -0.3f), new Color(1f, 0.7f, 0.45f), 2.5f, 4f);

            var camGo = new GameObject("StudioCam");
            camGo.transform.SetParent(transform, false);
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 30f; cam.nearClipPlane = 0.1f; cam.farClipPlane = 8f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.1f, 0.1f, 0.12f);
            cam.allowHDR = false; cam.allowMSAA = true; cam.useOcclusionCulling = false;
            Texture = new RenderTexture(Width, Height, 16, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { name = "VillainStudioRT", antiAliasing = 2 };
            Texture.Create();
            cam.targetTexture = Texture;
            cam.enabled = false;
            var urp = cam.GetUniversalAdditionalCameraData();
            if (urp != null) { urp.renderPostProcessing = false; urp.renderShadows = false; urp.antialiasing = AntialiasingMode.None; }
            Cam = cam;
            BuildOverlay(_overlayGold);
            Frame();
        }

        Material _overlayGold;

        /// <summary>TV graphics glued to the studio camera: LIVE bug (top-left) + lower-third channel caption.</summary>
        void BuildOverlay(Material gold)
        {
            var ov = CharacterModelBuilder.Pivot("Overlay", Cam.transform, Vector3.zero);
            const float d = 0.5f; float hh = d * Mathf.Tan(Cam.fieldOfView * 0.5f * Mathf.Deg2Rad), hw = hh * Width / (float)Height;
            var bar = MaterialLibrary.Unlit(new Color(0.08f, 0.08f, 0.1f), 1f);
            ProcMesh.Part("LowerThird", ov, ProcMesh.CachedBox(new Vector3(hw * 2f, hh * 0.24f, 0.001f), 0f), bar, new Vector3(0f, -hh * 0.84f, d), null, null, false);
            ProcMesh.Part("LowerThirdGold", ov, ProcMesh.CachedBox(new Vector3(hw * 2f, hh * 0.025f, 0.001f), 0f), MaterialLibrary.Unlit(VillainLook.Gold, 1f), new Vector3(0f, -hh * 0.71f, d - 0.001f), null, null, false);
            _caption = EnvKit.AddText(ov, "ME-TV  LIVE", new Vector3(0f, -hh * 0.845f, d - 0.002f), new Vector2(hw * 1.9f, hh * 0.2f), VillainLook.Gold, ShiftersFonts.Display, hh * 0.12f);
            _caption.transform.localRotation = Quaternion.identity;
            // one line, shrink long channel names to fit (wrapping got clipped by the bar)
            _caption.textWrappingMode = TextWrappingModes.NoWrap;
            _caption.fontSizeMin = Mathf.Min(_caption.fontSizeMin, _caption.fontSizeMax * 0.3f);   // EnvKit autosize shrinks long names
            ProcMesh.Part("LiveBug", ov, ProcMesh.CachedBox(new Vector3(hw * 0.42f, hh * 0.15f, 0.001f), 0f), MaterialLibrary.Unlit(new Color(0.85f, 0.12f, 0.1f), 1f), new Vector3(-hw * 0.74f, hh * 0.8f, d), null, null, false);
            var live = EnvKit.AddText(ov, "LIVE", new Vector3(-hw * 0.74f, hh * 0.8f, d - 0.002f), new Vector2(hw * 0.4f, hh * 0.13f), Color.white, ShiftersFonts.Label, hh * 0.1f);
            live.transform.localRotation = Quaternion.identity;
            _liveText = live;
            foreach (var r in ov.GetComponentsInChildren<Renderer>()) { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false; }
        }

        TextMeshPro _liveText;

        static void MakeLight(Transform parent, string name, Vector3 localPos, Color c, float intensity, float range)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point; l.color = c; l.intensity = intensity; l.range = range; l.shadows = LightShadows.None;
        }

        void Frame()
        {
            if (Cam == null) return;
            Vector3 target, eye;
            if (SpeakerIsPercy) { target = PercySetOffset + new Vector3(0f, 0.95f, -0.2f); eye = target + new Vector3(0.45f, 0.12f, 2.3f); }
            else { target = new Vector3(0f, 1.02f, 0f); eye = target + new Vector3(0.32f, 0.06f, 1.95f); }
            Cam.transform.localPosition = eye;
            Cam.transform.localRotation = Quaternion.LookRotation(target - eye, Vector3.up);
            if (Villain != null) Villain.LookTarget = Cam.transform.position;
            if (_caption != null) _caption.text = SpeakerIsPercy ? "PERCY  PIRATE BREAK-ROOM RADIO" : Caption(Channel);
            if (_liveText != null) _liveText.text = SpeakerIsPercy ? "PIRATE" : "LIVE";
        }

        // ------------------------------------------------------------------ API
        /// <summary>Go live (manual hold; pair with End). speakerId MAX / PERCY (null keeps the current one).</summary>
        public void Begin(string speakerId = null)
        {
            if (speakerId != null) SetSpeaker(speakerId);
            _manual++;
            Refresh(true);
        }

        public void End()
        {
            _manual = Mathf.Max(0, _manual - 1);
            Refresh(false);
        }

        /// <summary>Drop every hold (manual, broadcast, line) and stop rendering.</summary>
        public void EndAll() { _manual = 0; _broadcast = _line = false; Refresh(false); }

        public void SetSpeaker(string speakerId)
        {
            bool percy = VillainLook.IsPercy(speakerId);
            Speaker = percy ? SpeakerPercy : SpeakerMax;
            if (percy != SpeakerIsPercy) { SpeakerIsPercy = percy; Frame(); }
        }

        public void SetExpression(Expression e, float duration = 0f)
        {
            if (SpeakerIsPercy) Percy?.SetExpression(e, duration);
            else Villain?.SetExpression(e, duration);
        }

        public void Play(VillainGesture g) { Villain?.Play(g); }
        public void Play(VisualAction a) { Villain?.Play(a); }

        /// <summary>Channel hat + backdrop caption (his fake jobs).</summary>
        public void SetHat(VillainHat hat)
        {
            Channel = hat;
            Villain?.SetHat(hat);
            if (_caption != null && !SpeakerIsPercy) _caption.text = Caption(hat);
        }

        public void SetTalking(bool on, float seconds = 0f)
        {
            _talkUntil = on ? (seconds > 0f ? _t + seconds : float.MaxValue) : -1f;
            ApplyTalk();
        }

        /// <summary>Show one dialogue line on the speaker (face + mouth flaps while it types). Does not change live state.</summary>
        public void Say(StoryLine line)
        {
            SetSpeaker(line.SpeakerId);
            string text = line.Text ?? "";
            SetExpression(line.Face);
            float typing = text.Length / 45f + 0.3f;
            SetTalking(true, Mathf.Max(0.6f, line.Duration > 0f ? Mathf.Min(typing, line.Duration) : typing));
            if (SpeakerIsPercy) Percy?.Puff();
            else if (AutoGestures && Villain != null) AutoGesture(line.Face, text);
        }

        /// <summary>Render one frame into Texture now (DialogueBox portrait while not live).</summary>
        public Texture RenderStill()
        {
            if (Cam == null) return Texture;
            Villain?.PrepareSnapshot();
            Percy?.Tick(0f);
            Cam.Render();
            return Texture;
        }

        public static string Caption(VillainHat hat)
        {
            switch (hat)
            {
                case VillainHat.President: return "ME-TV  ADDRESS TO THE NATION OF ME";
                case VillainHat.CEO: return "ME-TV  BUSINESS: STOCKS UP (MINE)";
                case VillainHat.Banker: return "ME-TV  FINANCE: IT'S ALL MINE";
                case VillainHat.Judge: return "ME-TV  COURT: ALWAYS IN SESSION";
                case VillainHat.Weatherman: return "ME-TV  WEATHER: GREY. FOREVER.";
                case VillainHat.Janitor: return "ME-TV  CLEAN-UP ON AISLE ME";
                case VillainHat.EmployeeOfTheMonth: return "EMPLOYEE OF THE MONTH: ME (AGAIN)";
                default: return "ME-TV  LIVE";
            }
        }

        // ------------------------------------------------------------------ events
        void OnBroadcast(bool on, string speakerId)
        {
            if (!AutoListen) return;
            if (on && speakerId != null) SetSpeaker(speakerId);
            bool was = _broadcast;
            _broadcast = on;
            if (on && !was && !SpeakerIsPercy) Villain?.KazooToot();
            Refresh(on);
        }

        void OnLine(StoryLine line)
        {
            if (!AutoListen) return;
            bool ours = VillainLook.IsMax(line.SpeakerId) || VillainLook.IsPercy(line.SpeakerId);
            if (!ours) { if (_line) { _line = false; SetTalking(false); Refresh(false); } return; }
            _line = true;
            Say(line);
            Refresh(true);
        }

        void OnLineHidden()
        {
            if (!AutoListen) return;
            SetTalking(false);
            if (_line) { _line = false; Refresh(false); }
        }

        void Refresh(bool hint)
        {
            bool live = _manual > 0 || _broadcast || _line;
            if (live == IsLive) return;
            IsLive = live;
            if (Cam != null) Cam.enabled = live;
            if (Percy != null) Percy.SteamFx = false;
            if (!live) SetTalking(false);
            LiveChanged?.Invoke(live);
        }

        void AutoGesture(Expression face, string text)
        {
            _lineCount++;
            double r = _rng.NextDouble();
            if (face == Expression.Angry) Villain.Play(r < 0.5 ? VillainGesture.Point : VillainGesture.Rage);
            else if (face == Expression.Laugh) { if (r < 0.6) Villain.Play(VillainGesture.OneHandClap); }
            else if (face == Expression.Shocked) { if (r < 0.4) Villain.Play(VillainGesture.CrownSlip); }
            else if (face == Expression.Pout || face == Expression.Defeated) { if (r < 0.5) Villain.Play(VillainGesture.Facepalm); }
            else if (text.Length > 45 && r < 0.55) Villain.Play(VillainGesture.Monologue);
            else if (r < 0.2) Villain.Play(VisualAction.Shrug);
        }

        void ApplyTalk()
        {
            bool talk = _t < _talkUntil;
            if (Villain != null) Villain.Talking = talk && !SpeakerIsPercy;
            if (Percy != null) Percy.Talking = talk && SpeakerIsPercy;
        }

        void LateUpdate()
        {
            float dt = TimeController.RealDeltaTime;
            _t += dt;
            ApplyTalk();
            if (!IsLive || _phones == null) return;
            // ringing phones (one at a time)
            int ring = Mathf.FloorToInt(_t * 0.7f) % _phones.childCount;
            for (int i = 0; i < _phones.childCount; i++)
            {
                var p = _phones.GetChild(i);
                float j = i == ring ? Mathf.Sin(_t * 70f) * 4f : 0f;
                p.localRotation = Quaternion.Euler(j, (i - 2.5f) * 9f, j * 0.5f);
            }
        }
    }
}
