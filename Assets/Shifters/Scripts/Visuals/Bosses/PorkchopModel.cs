// =====================================================================================
//  SHIFTERS - PorkchopModel.cs   [owned by VISUALS_BOSSES]
//  PORKCHOP (CH08): the bank's 3.5 m armored robo-piggy-bank. Built by BossModels.Porkchop. Root on the floor, snout +Z.
//  Weak-point anchors: BellyPlug (left flank, 1.2 m up, GUN), Snout (front, STR punch), EarPort (right ear, IQ hack).
//  Legs pivot at the hips (LegFL/FR/BL/BR, Paw* children); eyes swap to glowing red for the HAM CHARGE tell.
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public sealed class PorkchopModel : BossModelBase
    {
        public static readonly string[] PartNames = { "Body", "Snout", "BellyPlug", "EarL", "EarR", "EarPort", "EyeL", "EyeR",
            "LegFL", "LegFR", "LegBL", "LegBR", "PawFL", "PawFR", "PawBL", "PawBR", "Tail", "CoinSlot" };
        public override string[] RequiredParts => PartNames;

        public Transform Body, Snout, BellyPlug, EarL, EarR, EarPort, EyeL, EyeR, Tail, CoinSlot;
        public Transform[] Legs = new Transform[4];   // FL, FR, BL, BR
        public Renderer[] EyeRenderers = new Renderer[2];
        public Material EyeNormal, EyeRed;
        public Renderer SnoutCrack;
        /// <summary>Height of the top of the coin slot (overall height without ears).</summary>
        public float Height = 3.5f;

        public bool EyesRed { get; private set; }

        /// <summary>HAM CHARGE tell: glowing red eyes.</summary>
        public void SetEyesRed(bool red)
        {
            EyesRed = red;
            for (int i = 0; i < EyeRenderers.Length; i++) if (EyeRenderers[i] != null) EyeRenderers[i].sharedMaterial = red ? EyeRed : EyeNormal;
        }

        /// <summary>Trot pose: phase in radians (advance ~ speed * 2), amplitude in degrees (0 = stand).</summary>
        public void SetGait(float phase, float amplitudeDeg = 25f)
        {
            for (int i = 0; i < 4; i++)
            {
                if (Legs[i] == null) continue;
                float s = (i == 0 || i == 3) ? 1f : -1f;   // diagonal pairs
                Legs[i].localRotation = Quaternion.Euler(Mathf.Sin(phase) * amplitudeDeg * s, 0f, 0f);
            }
            if (Body != null) Body.localPosition = new Vector3(0f, Mathf.Abs(Mathf.Sin(phase)) * amplitudeDeg * 0.002f, 0f);
        }

        /// <summary>Paw scrape (tell): front-right paw swings back, t 0..1.</summary>
        public void SetScrape(float t)
        {
            if (Legs[1] != null) Legs[1].localRotation = Quaternion.Euler(-35f * Mathf.Sin(t * Mathf.PI), 0f, 0f);
        }

        /// <summary>Ear wiggle angle (degrees) for idle / dizzy.</summary>
        public void SetEars(float degrees)
        {
            if (EarL != null) EarL.localRotation = Quaternion.Euler(-20f + degrees, 0f, 25f);
            if (EarR != null) EarR.localRotation = Quaternion.Euler(-20f - degrees, 0f, -25f);
        }
    }
}
