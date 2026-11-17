// =====================================================================================
//  SHIFTERS - Checkpoint.cs   [owned by the LEVEL agent]
//  Beacon pole + holo flag + ground ring + 3 spawn points (children). Inactive = dim blue,
//  active = green glow + flag raised + confetti. Activates when any Alive character enters its
//  zone and its Index is higher than the current checkpoint. A Downed teammate carried into an
//  already-activated checkpoint is revived on the spot. Build with LevelKit.Checkpoint.
// =====================================================================================
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Shifters
{
    public class Checkpoint : MonoBehaviour
    {
        public static readonly List<Checkpoint> All = new List<Checkpoint>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => All.Clear();

        [Tooltip("Activation order. Must increase along the level.")] public int Index;
        public Vector3 ZoneCenter = new Vector3(0f, 1.75f, 0f);
        public Vector3 ZoneSize = new Vector3(6.6f, 3.5f, 4.6f);
        public Transform[] SpawnPoints = new Transform[3];

        [Header("Visuals")]
        public Renderer[] StatusRenderers = new Renderer[0];
        public Transform Flag;
        public float FlagLowY = 0.9f, FlagHighY = 3.05f;
        public Light Lamp;
        [Tooltip("Flag number texts; relabelled at runtime with DisplayNumber.")] public TMP_Text[] NumberTexts = new TMP_Text[0];

        public bool IsActivated { get; private set; }

        /// <summary>1-based position of this checkpoint among the scene's checkpoints (ordered by Index), so the
        /// flag and banners read 1, 2, 3 whether a level numbers its checkpoints from 0 or from 1.</summary>
        public int DisplayNumber => RankOf(Index);

        /// <summary>1-based rank of a checkpoint index among the registered checkpoints (index+1 style fallback).</summary>
        public static int RankOf(int index)
        {
            int lower = 0;
            bool any = false;
            for (int i = 0; i < All.Count; i++)
            {
                if (All[i] == null) continue;
                any = true;
                if (All[i].Index < index) lower++;
            }
            return any ? lower + 1 : DefaultNumber(index);
        }

        /// <summary>Edit-time flag number: index if the level counts from 1, else index + 1.</summary>
        public static int DefaultNumber(int index) => index >= 1 ? index : index + 1;

        float _poll, _flagT, _celebrate;

        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() { All.Remove(this); }

        void Start()
        {
            string n = DisplayNumber.ToString();
            for (int i = 0; i < NumberTexts.Length; i++) if (NumberTexts[i] != null) NumberTexts[i].text = n;
            var mgr = CheckpointManager.Instance;
            if (mgr == null || mgr.Current != this) SetLampLit(false); // scenes baked before the light budget
        }

        public Pose GetSpawnPose(int slot)
        {
            slot = Mathf.Clamp(slot, 0, 2);
            var t = SpawnPoints != null && slot < SpawnPoints.Length ? SpawnPoints[slot] : null;
            if (t != null) return new Pose(t.position, t.rotation);
            return new Pose(transform.TransformPoint(new Vector3((slot - 1) * LevelKit.SpawnSpacing, 0f, 0f)), transform.rotation);
        }

        public List<Pose> GetSpawnPoses() => new List<Pose>(3) { GetSpawnPose(0), GetSpawnPose(1), GetSpawnPose(2) };

        public bool Contains(ShifterCharacter c) => ZoneMath.ContainsCharacter(transform, ZoneCenter, ZoneSize, c);

        /// <summary>Only the current checkpoint keeps its real-time point light (WebGL/Mobile light budget);
        /// the emissive lamp, ring and flag still show the state on the others.</summary>
        public void SetLampLit(bool lit) { if (Lamp != null) Lamp.enabled = lit; }

        /// <summary>Visual state only (CheckpointManager decides activation). celebrate = confetti + sound burst.</summary>
        public void SetVisualState(bool active, bool celebrate)
        {
            IsActivated = active;
            var mat = Kit.Mat(active ? MatKey.LightOn : MatKey.LightBlue);
            for (int i = 0; i < StatusRenderers.Length; i++) if (StatusRenderers[i] != null) StatusRenderers[i].sharedMaterial = mat;
            if (Lamp != null)
            {
                Lamp.color = active ? UIColors.Success : UIColors.Info;
                Lamp.intensity = active ? 2.4f : 0.6f;
            }
            if (celebrate)
            {
                _celebrate = 1.2f;
                Fx.Confetti(transform.position + transform.up * 2.6f);
                Fx.Poof(transform.position + Vector3.up * 0.2f, UIColors.Success, 2f);
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _poll -= dt;
            if (_poll <= 0f)
            {
                _poll = 0.1f;
                var all = ShifterCharacter.All;
                for (int i = 0; i < all.Count; i++)
                {
                    var c = all[i];
                    if (c == null || !Contains(c)) continue;
                    if (c.IsAlive)
                    {
                        var mgr = CheckpointManager.Instance;
                        if (mgr == null || Index > mgr.CurrentIndex) CheckpointManager.Ensure().TryActivate(this, c);
                    }
                    else if (c.State == CharacterState.Downed && IsActivated && CheckpointManager.Instance != null)
                        CheckpointManager.Instance.ReviveAt(this, c);
                }
            }

            // flag raise (ease-out-back) + idle sway
            float target = IsActivated ? 1f : 0f;
            if (Flag != null && (!Mathf.Approximately(_flagT, target) || IsActivated || _celebrate > 0f))
            {
                _flagT = Mathf.MoveTowards(_flagT, target, dt * 0.9f);
                float k = _flagT;
                float e = IsActivated ? 1f + 2.70158f * Mathf.Pow(k - 1f, 3f) + 1.70158f * Mathf.Pow(k - 1f, 2f) : k;
                var p = Flag.localPosition;
                p.y = Mathf.LerpUnclamped(FlagLowY, FlagHighY, e);
                Flag.localPosition = p;
                float sway = Mathf.Sin(Time.time * 2.3f + Index) * (IsActivated ? 6f : 2f);
                Flag.localRotation = Quaternion.Euler(0f, sway, 0f);
            }
            if (_celebrate > 0f)
            {
                _celebrate -= dt;
                if (Lamp != null) Lamp.intensity = 2.4f + 3f * Mathf.Max(0f, _celebrate) * Mathf.Abs(Mathf.Sin(Time.time * 18f));
            }
        }

        void OnDrawGizmos() => ZoneMath.DrawGizmo(transform, ZoneCenter, ZoneSize, new Color(0.24f, 0.86f, 0.59f));
    }
}
