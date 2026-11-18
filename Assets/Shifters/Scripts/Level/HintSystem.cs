// =====================================================================================
//  SHIFTERS - HintSystem.cs   [owned by the LEVEL agent]
//  H -> a hint for the HintZone containing the active character, quality by the active
//  character's IQ: >= Tuning.HintRealIQ "BIG BRAIN HINT: <real>", >= Tuning.HintVagueIQ
//  "HINT: <vague>", else a random dumb joke. No zone -> generic tips by IQ tier.
//  Raises GameEvents.Hint. 3 s cooldown. Created by LevelManager.
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public class HintSystem : MonoBehaviour
    {
        public static HintSystem Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Instance = null;

        public const float Cooldown = 3f;
        public const string RealPrefix = "BIG BRAIN HINT: ";
        public const string VaguePrefix = "HINT: ";

        /// <summary>Useless "hints" for IQ <= 3.</summary>
        public static readonly string[] Jokes =
        {
            "Have you tried jumping? Harder?",
            "Step 1: be smarter.",
            "Have you tried... being smarter?",
            "Walk forward. No, the other forward.",
            "Hint: the floor is the part you stand on.",
            "If at first you don't succeed, blame Kevin.",
            "Try turning it off and on again. You are the 'it'.",
            "Thinking is hard. Have a nap. (Not here.)",
            "Roses are red, doors are locked, your brain is a potato.",
            "Push the thing. Pull the other thing. Profit?",
            "Big brain time! ...oh no, it's tiny brain time.",
            "The real puzzle was the coworkers we threw along the way.",
            "Error 404: hint not found. Brain not found either.",
            "Wiggle the camera. Feel the vibes.",
            "According to my calculations: yes.",
            "Have you tried standing on something? Like a coworker?",
            "Lick the button. (Do not lick the button.)",
            "Ask someone with a bigger head.",
            "Have you tried shifting into someone smarter?",
            "Shift happens. Hints don't. Not for you.",
        };

        static readonly string[] GenericReal =
        {
            "Nothing to solve right here. Remember: every coworker sees the world differently - SHIFT to another one and press {Scan} to scan.",
            "The Brute lifts and pushes, the Sprinter outruns timers, the Brainiac sees hidden paths and hacks terminals.",
            "Teammates on STAY make excellent pressure-plate weights. Press {AIMode} to toggle Follow/Stay.",
            "Stuck on a high ledge? Stack: stand on a teammate's head, or let the Brute throw someone up.",
        };

        static readonly string[] GenericVague =
        {
            "Something around here probably matters. Probably.",
            "Try looking at things through someone else's eyes.",
            "Maybe a teammate could help? They're good for something.",
            "Heavy things are heavy. Fast things are fast. Think about it.",
        };

        static readonly string[] Thinking = { "Hmm...", "*brain noises*", "Ooh! Wait. No.", "I have an idea!", "Let me think..." };

        public string LastHint { get; private set; } = "";
        public int HintCount { get; private set; }
        public float CooldownLeft { get; private set; }

        System.Random _rng = new System.Random();

        protected virtual void Awake() { Instance = this; }
        protected virtual void OnDestroy() { if (Instance == this) Instance = null; }

        protected virtual void Update()
        {
            if (CooldownLeft > 0f) CooldownLeft = Mathf.Max(0f, CooldownLeft - PerspectiveStyle.UiDelta);
            if (ShiftersInput.HintPressed) RequestHint();
        }

        /// <summary>H key: hint for the active character (null when on cooldown / no active character).</summary>
        public string RequestHint()
        {
            var cm = CharacterManager.Instance;
            var active = cm != null ? cm.Active : null;
            if (active == null) return null;
            if (CooldownLeft > 0f)
            {
                GameEvents.RaiseToast("Brain recharging... " + Mathf.CeilToInt(CooldownLeft) + "s", UIColors.TextMuted);
                return null;
            }
            CooldownLeft = Cooldown;
            int iq = active.Stats != null ? active.Stats.IQ : 5;
            var zone = FindZone(active.Center);
            // gamepad prompts: hint texts may carry input tokens ({Scan}, {AIMode}, HintZone templates) -> current device
            string raw = Compose(iq, zone, _rng);
            string text = InputPrompts.FormatRich(raw);
            LastHint = text;
            HintCount++;
            if (iq < Tuning.HintRealIQ) active.Say(Thinking[_rng.Next(Thinking.Length)]);
            GameEvents.RaiseHint(raw);   // integrator2 gamepad1: HudHint formats the template live (DevicePromptText); return value = formatted
            return text;
        }

        /// <summary>Zone containing point: highest Priority, then smallest volume. Null if none.</summary>
        public static HintZone FindZone(Vector3 point)
        {
            HintZone best = null;
            var all = HintZone.All;
            for (int i = 0; i < all.Count; i++)
            {
                var z = all[i];
                if (z == null || !z.Contains(point)) continue;
                if (best == null || z.Priority > best.Priority || (z.Priority == best.Priority && z.Volume < best.Volume)) best = z;
            }
            return best;
        }

        /// <summary>The hint text a character with this IQ gets for zone (null zone = generic tips).</summary>
        public static string Compose(int iq, HintZone zone, System.Random rng)
        {
            rng ??= new System.Random();
            if (iq >= Tuning.HintRealIQ)
            {
                string real = zone != null ? (!string.IsNullOrEmpty(zone.RealHint) ? zone.RealHint : zone.VagueHint) : null;
                return RealPrefix + (!string.IsNullOrEmpty(real) ? real : GenericReal[rng.Next(GenericReal.Length)]);
            }
            if (iq >= Tuning.HintVagueIQ)
            {
                string vague = zone != null && !string.IsNullOrEmpty(zone.VagueHint) ? zone.VagueHint : null;
                return VaguePrefix + (vague ?? GenericVague[rng.Next(GenericVague.Length)]);
            }
            return Jokes[rng.Next(Jokes.Length)];
        }
    }
}
