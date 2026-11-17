// =====================================================================================
//  SHIFTERS - HoldingCell.cs   [owned by the WEAPONS_ENEMIES agent]
//  Detention cage for captured characters. The door is a SignalReceiver: powered => open.
//  Wire a lever/button/terminal to it (Inputs) - opening releases every prisoner
//  ("<NAME> RESCUED!"). Bots deliver prisoners at IntakePoint (outside the door) and the
//  prisoner is placed at DropPoint (inside). Built by EnemyKit.HoldingCell(...).
// =====================================================================================
using System.Collections.Generic;
using UnityEngine;

namespace Shifters
{
    public class HoldingCell : SignalReceiver
    {
        /// <summary>All cells in the scene (registry).</summary>
        public static readonly List<HoldingCell> All = new List<HoldingCell>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => All.Clear();

        [Header("Holding cell")]
        public string Id = "cell";
        [Tooltip("Sliding bars door (moves up when powered).")] public Transform Door;
        public Vector3 DoorClosedLocal;
        public float DoorOpenHeight = 3.1f;
        public float DoorSpeed = 3.5f;
        [Tooltip("Inside: where prisoners are placed.")] public Transform DropPoint;
        [Tooltip("Outside in front of the door: where bots hand prisoners over.")] public Transform IntakePoint;
        [Tooltip("Interior half extents (local, around DropPoint's XZ) used by Contains().")] public Vector3 InteriorHalfExtents = new Vector3(1.6f, 1.6f, 1.6f);
        public Renderer[] StatusLights = new Renderer[0];
        public Light Lamp;

        readonly List<ShifterCharacter> _prisoners = new List<ShifterCharacter>();
        /// <summary>Characters currently locked in this cell.</summary>
        public IReadOnlyList<ShifterCharacter> Prisoners => _prisoners;
        public bool IsOpen => Powered;
        public int PrisonerCount => _prisoners.Count;
        public Vector3 IntakePosition => IntakePoint != null ? IntakePoint.position : transform.TransformPoint(new Vector3(0f, 0f, 3.6f));
        public Vector3 DropPosition => DropPoint != null ? DropPoint.position : transform.position + Vector3.up * 0.2f;

        float _door; // 0 closed .. 1 open
        int _placeIndex;
        static readonly string[] CellLines = { "I'd like to speak to a manager.", "This is fine.", "Is there Wi-Fi in here?", "I want my lawyer. And a sandwich.", "Not my best day." };

        /// <summary>Cell by id (null if none). Level module: HoldingCell.Find(id)?.Imprison(c) after checkpoint reloads.</summary>
        public static HoldingCell Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < All.Count; i++) if (All[i] != null && All[i].Id == id) return All[i];
            return null;
        }

        protected override void OnEnable()
        {
            if (!All.Contains(this)) All.Add(this);
            // [integrator] LevelManager expects the enemies module to restore checkpoint captives (see its doc comment).
            if (Application.isPlaying) LevelManager.CaptureRestoreHook = RestoreCaptive;
            base.OnEnable();
        }

        /// <summary>LevelManager.CaptureRestoreHook: re-imprisons a character that was Captured in the restored
        /// checkpoint snapshot (silent state restore, then locked inside). False if the cell is gone (-> Downed).</summary>
        static bool RestoreCaptive(ShifterCharacter c, string cellId)
        {
            var cell = Find(cellId);
            if (c == null || cell == null || c.State == CharacterState.Lost) return false;
            c.RestoreState(CharacterState.Captured, Mathf.Max(1f, c.Health), cell.Id);
            cell.Imprison(c);
            return true;
        }

        protected override void OnDisable()
        {
            All.Remove(this);
            base.OnDisable();
        }

        /// <summary>Wire door inputs at runtime (re-subscribes; door snaps to the new logic). Edit-mode builders can
        /// also set Inputs directly before saving the scene.</summary>
        public void SetInputs(params SignalSource[] inputs)
        {
            bool was = enabled;
            enabled = false;
            Inputs = inputs ?? new SignalSource[0];
            enabled = was;
        }

        /// <summary>Lock a character in: frees it from a bot if carried, places it inside with physics restored,
        /// and makes sure it is Captured with this cell's id. If the door is open it is released right away.</summary>
        public void Imprison(ShifterCharacter c)
        {
            if (c == null || c.State == CharacterState.Lost || c.State == CharacterState.Downed) return;
            if (c.State == CharacterState.Alive) c.Capture(Id, "was locked in a holding cell");
            c.CapturedCellId = Id;
            Vector3 spot = NextSpot();
            Quaternion face = Quaternion.LookRotation(transform.forward, Vector3.up);
            Fx.Poof(c.Center, UIColors.Danger, 0.8f);
            CaptiveBody.Free(c, spot, face);
            Fx.Poof(spot + Vector3.up, UIColors.Danger, 0.8f);
            if (!_prisoners.Contains(c)) _prisoners.Add(c);
            Sfx.At(SfxId.DoorSlam, transform.position + Vector3.up, 0.8f);
            Sfx.At(SfxId.Captured, spot, 0.9f);
            if (c.Visual != null) { c.Visual.SetPose(PoseOverride.None); c.Visual.SetExpression(Expression.Sad, 3f); }
            c.Say(CellLines[(_placeIndex + c.Slot + 7) % CellLines.Length]);
            if (Powered && Application.isPlaying) ReleaseAll();
        }

        Vector3 NextSpot()
        {
            Vector3 basePos = DropPosition;
            Vector3 right = transform.right, back = -transform.forward;
            Vector3 off;
            switch (_placeIndex++ % 3)
            {
                case 0: off = Vector3.zero; break;
                case 1: off = right * 1.0f + back * 0.4f; break;
                default: off = -right * 1.0f + back * 0.4f; break;
            }
            return basePos + off;
        }

        /// <summary>Release every prisoner (door opened): ReleaseFromCapture + sound/confetti/quip per character.</summary>
        public void ReleaseAll()
        {
            for (int i = _prisoners.Count - 1; i >= 0; i--)
            {
                var c = _prisoners[i];
                _prisoners.RemoveAt(i);
                if (c == null || c.State != CharacterState.Captured) continue;
                if (!string.IsNullOrEmpty(c.CapturedCellId) && c.CapturedCellId != Id) continue;
                c.ReleaseFromCapture();
                // "<NAME> HAS BEEN RESCUED" banner: raised by CharacterManager on Captured -> Alive
                Sfx.At(SfxId.Revive, c.Center);
                Fx.Confetti(c.Center + Vector3.up * 0.5f);
                if (c.Visual != null) c.Visual.SetExpression(Expression.Happy, 2f);
                c.Say("FREEDOM!");
            }
            _placeIndex = 0;
        }

        /// <summary>Is a world point inside the cell interior?</summary>
        public bool Contains(Vector3 world)
        {
            Vector3 l = transform.InverseTransformPoint(world);
            Vector3 c = transform.InverseTransformPoint(DropPosition);
            return Mathf.Abs(l.x - c.x) <= InteriorHalfExtents.x && Mathf.Abs(l.z - c.z) <= InteriorHalfExtents.z && l.y > -0.5f && l.y < 3.5f;
        }

        protected override void OnPowerChanged(bool powered, bool initial)
        {
            if (initial || !Application.isPlaying) { _door = powered ? 1f : 0f; ApplyDoor(); ApplyLights(powered); return; }
            ApplyLights(powered);
            Sfx.At(powered ? SfxId.DoorOpen : SfxId.DoorClose, transform.position + Vector3.up * 1.5f);
            if (powered) ReleaseAll();
        }

        protected override void Update()
        {
            base.Update();
            float target = Powered ? 1f : 0f;
            if (!Mathf.Approximately(_door, target))
            {
                _door = Mathf.MoveTowards(_door, target, Time.deltaTime * DoorSpeed / Mathf.Max(0.1f, DoorOpenHeight));
                ApplyDoor();
            }
            for (int i = _prisoners.Count - 1; i >= 0; i--)
            {
                var c = _prisoners[i];
                if (c == null || c.State != CharacterState.Captured || (c.CapturedCellId != Id && !string.IsNullOrEmpty(c.CapturedCellId))) _prisoners.RemoveAt(i);
            }
        }

        void ApplyDoor()
        {
            if (Door == null) return;
            float e = _door * _door * (3f - 2f * _door);
            Door.localPosition = DoorClosedLocal + Vector3.up * (DoorOpenHeight * e);
        }

        void ApplyLights(bool open)
        {
            var mat = Kit.Mat(open ? MatKey.LightOn : MatKey.LightRed);
            for (int i = 0; i < StatusLights.Length; i++) if (StatusLights[i] != null) StatusLights[i].sharedMaterial = mat;
            if (Lamp != null) Lamp.color = open ? UIColors.Success : UIColors.Danger;
        }
    }
}
