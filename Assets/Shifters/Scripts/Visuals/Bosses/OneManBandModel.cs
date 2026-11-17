// =====================================================================================
//  SHIFTERS - OneManBandModel.cs   [owned by VISUALS_BOSSES]
//  THE ONE-MAN BAND (CH13): Max's 9 m gold ME-CHA. Built by BossModels.OneManBand. Root on the floor, front +Z.
//  Named parts (CAMPAIGN §18.8): Fist, ArmL, ArmR, SkateL, SkateR, Dome, Antenna, Cockpit (+ LegL/LegR, Torso, Kazoo,
//  Pelvis, FlameL/FlameR, MaxSeat). Max (VillainModelBuilder or a stand-in) sits in the Cockpit inside the glass Dome.
//  Arms pivot at the shoulders (ArmR ends in the giant Fist, ArmL holds the Kazoo), legs at the hips (skates at the feet).
//  P3: SetShieldHalf(side, on) / SetDomeShield(on) = hex half-shields ShieldW / ShieldE. P1: SetFistDetached(on) draws the
//  FistChain (6 links) from the ArmR wrist to wherever the level moves Fist (LateUpdate).
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public sealed class OneManBandModel : BossModelBase
    {
        public static readonly string[] PartNames = { "Fist", "ArmL", "ArmR", "SkateL", "SkateR", "Dome", "Antenna", "Cockpit",
            "LegL", "LegR", "Pelvis", "Torso", "Kazoo", "FlameL", "FlameR", "MaxSeat", "AntennaTip" };
        public override string[] RequiredParts => PartNames;

        public Transform Fist, ArmL, ArmR, SkateL, SkateR, Dome, Antenna, AntennaTip, Cockpit, LegL, LegR, Pelvis, Torso, Kazoo, FlameL, FlameR, MaxSeat;
        public GameObject Max;
        public Renderer DomeGlass;
        public Material DomeClear, DomeShield;
        public float Height = 9.6f;
        /// <summary>Shoulder height (arm pivots) and arm length to the fist centre when hanging.</summary>
        public float ShoulderHeight = 6.6f, FistReach = 4.7f;

        /// <summary>Hex half-shields over the dome (-X = west, +X = east) and the detached-fist chain (6 links).</summary>
        public Transform ShieldW, ShieldE, FistChain;
        public Transform[] ChainLinks;
        public bool FistDetached { get; private set; }

        /// <summary>P3 shield, both halves (the dome glass glows while both are up). Turning a half off shatters it.</summary>
        public void SetDomeShield(bool on)
        {
            SetShieldHalf(-1, on);
            SetShieldHalf(1, on);
        }

        /// <summary>One hex half-shield (side -1 = west / -X, +1 = east / +X; P3 FIREWALL W / E). Turning a lit half off plays a
        /// short pixel-confetti shatter on that side. Idempotent.</summary>
        public void SetShieldHalf(int side, bool on)
        {
            var h = side < 0 ? ShieldW : ShieldE;
            if (h != null)
            {
                bool was = h.gameObject.activeSelf;
                if (was != on) h.gameObject.SetActive(on);
                if (was && !on && Application.isPlaying && Dome != null)
                {
                    var outward = Dome.right * (side < 0 ? -1f : 1f);
                    var c = Dome.position + outward * 1.1f + Dome.up * 0.7f;
                    Fx.ConfettiFlare(c, new Color(0.35f, 0.9f, 1f), outward + Vector3.up * 0.6f);
                    Fx.ConfettiFlare(c + Dome.up * 0.5f, new Color(1f, 0.35f, 0.85f), outward + Vector3.up * 0.3f);
                }
            }
            bool both = ShieldW != null && ShieldE != null ? ShieldW.gameObject.activeSelf && ShieldE.gameObject.activeSelf : on;
            if (DomeGlass != null && DomeShield != null && DomeClear != null) DomeGlass.sharedMaterial = both ? DomeShield : DomeClear;
        }

        /// <summary>True when that half-shield is up.</summary>
        public bool ShieldHalfOn(int side)
        {
            var h = side < 0 ? ShieldW : ShieldE;
            return h != null && h.gameObject.activeSelf;
        }

        /// <summary>P1: draws a gold chain from the ArmR wrist to the Fist wherever the level moves the Fist transform (hover,
        /// slam, stuck, yank back). The model never moves the Fist itself; off hides the chain.</summary>
        public void SetFistDetached(bool on)
        {
            FistDetached = on;
            if (FistChain != null) FistChain.gameObject.SetActive(on);
            if (on) UpdateChain();
        }

        /// <summary>World point of the ArmR wrist (chain start).</summary>
        public Vector3 WristPoint => ArmR != null ? ArmR.TransformPoint(0f, -3.7f, 0f) : transform.position;

        void LateUpdate()
        {
            if (FistDetached) UpdateChain();
        }

        void UpdateChain()
        {
            if (ChainLinks == null || Fist == null || ArmR == null) return;
            var a = WristPoint;
            var b = Fist.TransformPoint(0f, 0.7f, 0f);
            var d = b - a;
            float len = d.magnitude;
            var fwd = len > 0.001f ? d / len : Vector3.down;
            var look = Quaternion.LookRotation(fwd, Mathf.Abs(fwd.y) > 0.95f ? Vector3.forward : Vector3.up);
            int n = ChainLinks.Length;
            float seg = len / n;
            float sz = transform.lossyScale.z > 0.0001f ? transform.lossyScale.z : 1f;
            for (int i = 0; i < n; i++)
            {
                var l = ChainLinks[i];
                if (l == null) continue;
                l.position = a + d * ((i + 0.5f) / n);
                // the ring lies in its local XZ plane, so local Z (= the chain direction) runs through it; alternate links roll 90
                l.rotation = look * Quaternion.Euler(0f, 0f, (i & 1) == 0 ? 0f : 90f);
                l.localScale = new Vector3(1f, 1f, Mathf.Max(0.4f, seg * 1.3f / (0.53f * sz)));
            }
        }

        /// <summary>Rocket skate flames (P2 SKATES loop).</summary>
        public void SetBoost(bool on)
        {
            if (FlameL != null) FlameL.gameObject.SetActive(on);
            if (FlameR != null) FlameR.gameObject.SetActive(on);
        }

        /// <summary>ArmR pitch (degrees, negative = raise forward). -90 = fist straight ahead; slam: animate to ~-15 with
        /// the root leaning; the fist reaches the floor at about FistSlamPitch.</summary>
        public void SetFistArm(float pitchDeg, float rollDeg = 0f)
        {
            if (ArmR != null) ArmR.localRotation = Quaternion.Euler(pitchDeg, 0f, rollDeg);
        }

        /// <summary>ArmL (kazoo) yaw/pitch for the kazoo sweep.</summary>
        public void SetKazooArm(float pitchDeg, float yawDeg)
        {
            if (ArmL != null) ArmL.localRotation = Quaternion.Euler(pitchDeg, yawDeg, 0f);
        }

        /// <summary>Skate stride: phase radians, amplitude degrees.</summary>
        public void SetStride(float phase, float amplitudeDeg = 18f)
        {
            if (LegL != null) LegL.localRotation = Quaternion.Euler(Mathf.Sin(phase) * amplitudeDeg, 0f, 0f);
            if (LegR != null) LegR.localRotation = Quaternion.Euler(-Mathf.Sin(phase) * amplitudeDeg, 0f, 0f);
        }
    }
}
