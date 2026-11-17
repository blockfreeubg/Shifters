// =====================================================================================
//  SHIFTERS - MenuStage.cs   (UI agent)
//  3D backdrop of the main menu: a dark round stage with spotlights, the current (or a
//  preview) team as CharacterPuppets idling/emoting, and a slowly swaying orbit camera that
//  frames the team on the right half of the screen (the menu column sits on the left).
//  Lives in the active scene (destroyed with it). Degrades gracefully with stub visuals.
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public class MenuStage : MonoBehaviour
    {
        static MenuStage _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _instance = null;

        /// <summary>Name of the empty marker the scene builder puts into MainMenu.unity (the stage is parented to it).</summary>
        public const string SceneMarkerName = "MenuStage";

        public static MenuStage Instance => _instance;
        public CharacterPuppet[] Puppets { get; private set; } = new CharacterPuppet[0];
        public Camera Cam => _cam;
        /// <summary>The team currently standing on the stage.</summary>
        public TeamData Team => _team;

        static readonly Vector3 Center = new Vector3(0f, 0f, 0f);
        static readonly VisualAction[] Emotes = { VisualAction.Wave, VisualAction.Dance, VisualAction.FistPump, VisualAction.Shrug, VisualAction.Taunt, VisualAction.Point, VisualAction.Celebrate };

        Camera _cam;
        Transform _cast;
        TeamData _team;
        float _nextEmote = 1.2f;
        System.Random _rng = new System.Random(1234);

        /// <summary>Create the stage (if needed) and show this team (null => a preview team).</summary>
        public static MenuStage Ensure(TeamData team)
        {
            if (_instance == null)
            {
                var go = new GameObject("[MenuStage]");
                var marker = GameObject.Find(SceneMarkerName);
                if (marker != null) go.transform.SetParent(marker.transform, false);
                _instance = go.AddComponent<MenuStage>();
                _instance.Build();
            }
            _instance.SetTeam(team ?? CharacterGenerator.GenerateTeam(20261113));
            return _instance;
        }

        public static void DestroyStage()
        {
            if (_instance != null) Destroy(_instance.gameObject);
            _instance = null;
        }

        void OnDestroy() { if (_instance == this) _instance = null; }

        void Build()
        {
            _cam = Camera.main;
            if (_cam == null)
            {
                var cgo = new GameObject("Menu Camera");
                cgo.tag = "MainCamera";
                cgo.transform.SetParent(transform, false);
                _cam = cgo.AddComponent<Camera>();
            }
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = UIColors.Hex(0x0B0E13);
            _cam.fieldOfView = 40f;
            _cam.nearClipPlane = 0.1f;
            _cam.farClipPlane = 200f;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.16f, 0.18f, 0.24f);
            LevelLighting.Apply(LevelTheme.Menu);

            var floorMat = MaterialLibrary.Colored(UIColors.Hex(0x232A35), 0.35f);
            var rimMat = MaterialLibrary.Colored(UIColors.Accent, 0.4f, 1.5f);
            var darkMat = MaterialLibrary.Colored(UIColors.Hex(0x12161C), 0.2f);
            ProcMesh.Part("Stage", transform, ProcMesh.Cylinder(4.2f, 0.4f, 48, true), floorMat, Center + new Vector3(0f, -0.2f, 0f));
            ProcMesh.Part("StageRim", transform, ProcMesh.Torus(4.2f, 0.06f, 64, 8), rimMat, Center + new Vector3(0f, 0.0f, 0f));
            ProcMesh.Part("Plinth", transform, ProcMesh.Cylinder(4.8f, 0.6f, 48, true), darkMat, Center + new Vector3(0f, -0.65f, 0f), null, null, false);
            ProcMesh.Part("Backdrop", transform, ProcMesh.Box(new Vector3(30f, 14f, 0.5f), 0.1f), darkMat, Center + new Vector3(0f, 5f, 9f), null, null, false);
            for (int i = 0; i < 4; i++)
            {
                float x = i * 3f;
                ProcMesh.Part("Stripe" + i, transform, ProcMesh.Box(new Vector3(1.2f, 0.12f, 0.1f)), MaterialLibrary.Colored(UIColors.AccentYellow, 0.3f, 1.2f),
                    Center + new Vector3(x, 0.9f + (i % 2) * 0.4f, 8.7f), Quaternion.Euler(0f, 0f, 35f), null, false);
            }

            AddSpot("KeySpot", new Vector3(-3.5f, 7f, -4f), new Color(1f, 0.82f, 0.62f), 70f, 38f, true);
            AddSpot("RimSpot", new Vector3(4f, 6f, 4f), new Color(0.45f, 0.7f, 1f), 40f, 40f, false);
            AddSpot("FillSpot", new Vector3(3.5f, 5f, -5f), new Color(1f, 0.55f, 0.25f), 25f, 45f, false);
            _cast = new GameObject("Cast").transform;
            _cast.SetParent(transform, false);
            _cast.localPosition = Center;
        }

        void AddSpot(string n, Vector3 pos, Color c, float intensity, float angle, bool shadows)
        {
            var go = new GameObject(n);
            go.transform.SetParent(transform, false);
            go.transform.position = Center + pos;
            go.transform.LookAt(Center + Vector3.up * 0.8f);
            var l = go.AddComponent<Light>();
            l.type = LightType.Spot;
            l.color = c;
            l.intensity = intensity;
            l.range = 25f;
            l.spotAngle = angle;
            l.innerSpotAngle = angle * 0.6f;
            l.shadows = shadows ? LightShadows.Soft : LightShadows.None;
        }

        /// <summary>Rebuild the puppets for a team.</summary>
        public void SetTeam(TeamData team)
        {
            if (team == null || team == _team) return;
            _team = team;
            for (int i = _cast.childCount - 1; i >= 0; i--) Destroy(_cast.GetChild(i).gameObject);
            int n = team.Members != null ? team.Members.Length : 0;
            Puppets = new CharacterPuppet[n];
            for (int i = 0; i < n; i++)
            {
                var p = team.Members[i];
                if (p == null) continue;
                float x = (i - (n - 1) * 0.5f) * 1.7f;
                var rot = Quaternion.Euler(0f, 180f + (i - 1) * -14f, 0f);
                try
                {
                    Puppets[i] = CharacterPuppet.Create(p, _cast, new Vector3(x, 0f, (i == 1 ? 0.4f : 0f)), rot);
                    Puppets[i].SetIdleStyle(i == 1 ? 1 : 0);
                }
                catch (System.Exception e) { Debug.LogWarning("[UI] MenuStage puppet failed: " + e.Message); }
            }
            _nextEmote = 0.8f;
        }

        void LateUpdate()
        {
            if (_cam == null) return;
            float t = UITime.Now;
            float yaw = Mathf.Sin(t * 0.12f) * 16f;
            var rot = Quaternion.Euler(8f, yaw, 0f);
            var focus = Center + new Vector3(0f, 1.05f, 0f);
            Vector3 pos = focus + rot * new Vector3(0f, 0.9f, -9.2f);
            _cam.transform.position = pos;
            _cam.transform.LookAt(focus);
            // put the team on the right half of the frame (menu column is on the left)
            _cam.transform.position -= _cam.transform.right * 2.55f;

            _nextEmote -= UITime.Delta;
            if (_nextEmote <= 0f && Puppets.Length > 0)
            {
                _nextEmote = 2.6f + (float)_rng.NextDouble() * 2.8f;
                var p = Puppets[_rng.Next(Puppets.Length)];
                if (p != null)
                {
                    p.Play(Emotes[_rng.Next(Emotes.Length)]);
                    p.SetExpression(_rng.Next(2) == 0 ? Expression.Happy : Expression.Smug, 2f);
                }
            }
        }
    }
}
