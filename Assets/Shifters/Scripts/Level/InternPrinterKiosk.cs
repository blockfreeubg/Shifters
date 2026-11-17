// =====================================================================================
//  SHIFTERS - InternPrinterKiosk.cs   [perspective_level kit, added by SEAMS for the campaign (CAMPAIGN §16, P1)]
//  THE INTERN PRINTER: an office-printer kiosk placed at checkpoints. While an intern is Lost the console
//  offers "[E] REPRINT {NAME}"; a PrintSeconds "PAPERWORK" animation runs, then CharacterManager.Reprint
//  re-clones them on the output tray (CloneGeneration + 1, -1 secondary stat) and the current checkpoint
//  is re-saved (CheckpointManager.ActivateFromScript(current)) so a Restart Checkpoint keeps the reprint.
//  Build with LevelKit.ReprintKiosk.
// =====================================================================================
using TMPro;
using UnityEngine;

namespace Shifters
{
    public class InternPrinterKiosk : MonoBehaviour, IInteractable
    {
        [Tooltip("PAPERWORK animation length (s).")] public float PrintSeconds = 10f;
        [Tooltip("Where the reprinted intern appears (local to Root).")] public Vector3 OutputLocal = new Vector3(0f, 0.05f, 1.8f);
        [Tooltip("Kiosk root (floor level, faces +Z). Null = this transform.")] public Transform Root;
        public TextMeshPro Screen;
        public Transform Paper;

        public bool IsPrinting { get; private set; }
        public int PrintingSlot { get; private set; } = -1;
        public float Progress01 { get; private set; }
        public int ReprintCount { get; private set; }
        public ShifterCharacter LastReprinted { get; private set; }

        float _t, _whir;
        int _shownPct = -1;
        static readonly string[] Lines = { "PAPERWORK", "FILLING FORM 27-B", "LOW ON INK", "INTERN JAM IN TRAY 2", "WARMING UP", "ALMOST..." };

        public int InteractPriority => 4;
        public Vector3 InteractPoint { get { var r = Root != null ? Root : transform; return r.TransformPoint(new Vector3(0f, 1.1f, 0.6f)); } }
        public Pose OutputPose { get { var r = Root != null ? Root : transform; return new Pose(r.TransformPoint(OutputLocal), r.rotation); } }

        /// <summary>First Lost slot that can be reprinted, or -1.</summary>
        public static int FirstLostSlot()
        {
            var cm = CharacterManager.Instance;
            if (cm == null) return -1;
            for (int i = 0; i < cm.Team.Count; i++) if (cm.CanReprint(i)) return i;
            return -1;
        }

        public bool CanInteract(ShifterCharacter who, out string blockedReason)
        {
            blockedReason = null;
            if (who == null) return false;
            if (IsPrinting) { blockedReason = "PRINTING... " + Mathf.CeilToInt(PrintSeconds * (1f - Progress01)) + " S"; return false; }
            if (LevelManager.Instance != null && LevelManager.Instance.IsCompleted) return false;
            return FirstLostSlot() >= 0; // nobody Lost: no prompt at all
        }

        public string GetPrompt(ShifterCharacter who)
        {
            var cm = CharacterManager.Instance;
            int s = FirstLostSlot();
            var c = cm != null ? cm.Get(s) : null;
            return c != null ? "REPRINT " + c.DisplayName.ToUpperInvariant() : "REPRINT";
        }

        public void Interact(ShifterCharacter who)
        {
            if (!CanInteract(who, out _)) return;
            StartPrint(FirstLostSlot());
            if (who != null) who.Say("One intern, extra crispy.");
        }

        /// <summary>Starts printing slot (tests / scripts). False when it is not Lost or a print is running.</summary>
        public bool StartPrint(int slot)
        {
            var cm = CharacterManager.Instance;
            if (IsPrinting || cm == null || !cm.CanReprint(slot)) return false;
            IsPrinting = true;
            PrintingSlot = slot;
            _t = 0f;
            _whir = 0f;
            _shownPct = -100;
            Progress01 = 0f;
            GameEvents.RaiseToast("PRINTING " + cm.Get(slot).DisplayName.ToUpperInvariant() + "... PLEASE WAIT", UIColors.AccentYellow);
            Sfx.At(SfxId.PrinterWhir, InteractPoint, 0.9f);
            return true;
        }

        void Update()
        {
            if (!IsPrinting) return;
            if (LevelManager.Instance != null && LevelManager.Instance.IsCompleted) { Abort(); return; }
            float dt = Time.deltaTime;
            _t += dt;
            Progress01 = Mathf.Clamp01(_t / Mathf.Max(0.05f, PrintSeconds));
            _whir -= dt;
            if (_whir <= 0f) { _whir = 2.2f; Sfx.At(SfxId.PrinterWhir, InteractPoint, 0.6f, 0.9f + 0.2f * Progress01); }
            int pct = Mathf.FloorToInt(Progress01 * 100f);
            if (Screen != null && pct / 5 != _shownPct / 5)
            {
                _shownPct = pct;
                Screen.text = Lines[Mathf.Min(Lines.Length - 1, pct / 20)] + "\n<size=70%>" + pct + "%</size>";
            }
            if (Paper != null)
            {
                var p = Paper.localPosition;
                p.z = 0.55f + 0.35f * Mathf.PingPong(_t * 2f, 1f);
                Paper.localPosition = p;
            }
            if (Progress01 >= 1f) Finish();
        }

        void Abort()
        {
            IsPrinting = false;
            PrintingSlot = -1;
            if (Screen != null) Screen.text = "INTERN\nPRINTER";
        }

        void Finish()
        {
            int slot = PrintingSlot;
            Abort();
            var cm = CharacterManager.Instance;
            if (cm == null || !cm.CanReprint(slot)) return;
            var pose = OutputPose;
            var c = cm.Reprint(slot, pose.position, pose.rotation);
            if (c == null) return;
            ReprintCount++;
            LastReprinted = c;
            Fx.Poof(pose.position + Vector3.up, c.Color, 1.4f);
            Fx.Confetti(pose.position + Vector3.up * 1.5f);
            Sfx.At(SfxId.Revive, pose.position);
            GameEvents.RaiseToast("PRINTED " + c.DisplayName.ToUpperInvariant() + ". LOW ON INK.", UIColors.Success);
            c.Say("Did I... exist before?");
            var cpm = CheckpointManager.Instance;
            if (cpm != null && cpm.CurrentIndex >= 0) CheckpointManager.ActivateFromScript(cpm.CurrentIndex);
        }
    }
}
