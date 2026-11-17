// =====================================================================================
//  SHIFTERS - VillainTypes.cs   (VISUALS module, campaign wave)
//  Plain types of the villain / ally puppets: Max's one-shot gestures, his channel hats
//  (ME-TV fake jobs), Percy's screen faces and the dummy poses. No MonoBehaviours here.
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    /// <summary>Max's own one-shot gestures (VillainPuppet.Play). Base VisualActions (Shrug, Taunt, Wave...) also work.</summary>
    public enum VillainGesture { None, Monologue, OneHandClap, Point, Facepalm, Rage, CrownSlip }

    /// <summary>ME-TV channel hats (his fake jobs). Crown = default look.</summary>
    public enum VillainHat { Crown, President, CEO, Banker, Judge, Weatherman, Janitor, EmployeeOfTheMonth }

    /// <summary>Percy's screen face: (o_o) (^_^) (T_T) (&gt;_&lt;) (-_-)zzz ;) (O_O) and TV static.</summary>
    public enum PercyFace { Neutral, Happy, Crying, Upset, Sleepy, Wink, Shocked, Static }

    /// <summary>Team-Building Dummy poses (creepy-cute). Reach = arms out for a "team-building hug".</summary>
    public enum DummyPose { Stand, Reach, Hug, Peek, Slump, Wave, TPose }

    public static class VillainLook
    {
        /// <summary>Max gold (speaker color, hologram tint).</summary>
        public static readonly Color Gold = new Color(0.91f, 0.72f, 0.23f);           // #E8B83A
        public static readonly Color SuitGrey = new Color(0.43f, 0.45f, 0.48f);       // #6E737B
        public static readonly Color SuitDark = new Color(0.35f, 0.37f, 0.4f);        // #5A5F66
        public static readonly Color PercyBrown = new Color(0.545f, 0.353f, 0.169f);  // #8B5A2B

        public static float GestureDuration(VillainGesture g)
        {
            switch (g)
            {
                case VillainGesture.Monologue: return 3.2f;
                case VillainGesture.OneHandClap: return 1.6f;
                case VillainGesture.Point: return 1.3f;
                case VillainGesture.Facepalm: return 1.7f;
                case VillainGesture.Rage: return 2.2f;
                case VillainGesture.CrownSlip: return 2.2f;
                default: return 0f;
            }
        }

        /// <summary>Face shown while the gesture plays (unless a manual expression is set).</summary>
        public static Expression GestureFace(VillainGesture g, float t)
        {
            switch (g)
            {
                case VillainGesture.Monologue: return Expression.Smug;
                case VillainGesture.OneHandClap: return t < 0.9f ? Expression.Smug : Expression.Laugh;
                case VillainGesture.Point: return Expression.Angry;
                case VillainGesture.Facepalm: return t < 0.5f ? Expression.Pout : Expression.Defeated;
                case VillainGesture.Rage: return Expression.Angry;
                case VillainGesture.CrownSlip: return t < 1.3f ? Expression.Shocked : Expression.Pout;
                default: return Expression.Smug;
            }
        }

        public static string PercyText(PercyFace f)
        {
            switch (f)
            {
                case PercyFace.Happy: return "(^_^)";
                case PercyFace.Crying: return "(T_T)";
                case PercyFace.Upset: return "(>_<)";
                case PercyFace.Sleepy: return "(-_-)zzz";
                case PercyFace.Wink: return "(^_~)";
                case PercyFace.Shocked: return "(O_O)";
                case PercyFace.Static: return "#%&@!";
                default: return "(o_o)";
            }
        }

        /// <summary>Map a dialogue Expression (StoryLine.Face) onto Percy's screen.</summary>
        public static PercyFace PercyFor(Expression e)
        {
            switch (e)
            {
                case Expression.Happy: case Expression.Laugh: return PercyFace.Happy;
                case Expression.Sad: case Expression.Defeated: return PercyFace.Crying;
                case Expression.Angry: case Expression.Strain: case Expression.Pout: return PercyFace.Upset;
                case Expression.KO: case Expression.Dizzy: return PercyFace.Sleepy;
                case Expression.Smug: return PercyFace.Wink;
                case Expression.Shocked: case Expression.Surprised: case Expression.Scared: return PercyFace.Shocked;
                default: return PercyFace.Neutral;
            }
        }

        /// <summary>Speaker-id test used by VillainStudio ("MAX", "max", "VILLAIN", "ME", "TV" -> Max; "PERCY" -> Percy).</summary>
        public static bool IsMax(string speakerId)
        {
            if (string.IsNullOrEmpty(speakerId)) return false;
            var s = speakerId.Trim().ToUpperInvariant();
            return s == "MAX" || s == "VILLAIN" || s == "ME" || s == "TV" || s == "MAX ME" || s.StartsWith("MAXIMILIAN") || s == "ME-TV";
        }

        public static bool IsPercy(string speakerId)
        {
            if (string.IsNullOrEmpty(speakerId)) return false;
            var s = speakerId.Trim().ToUpperInvariant();
            return s == "PERCY" || s.StartsWith("PERCY") || s == "RADIO";
        }
    }
}
