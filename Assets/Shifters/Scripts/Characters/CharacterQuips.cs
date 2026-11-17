// =====================================================================================
//  SHIFTERS - CharacterQuips.cs   [owned by the CORE agent]
//  Line pools for switch-in greetings, taunts, idle chatter and AI complaints.
//  Archetype-flavoured. Pure data + a tiny picker (no allocations).
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public static class CharacterQuips
    {
        public const string CantJump = "I can't jump that far!";

        static readonly string[] SwitchGeneric =
        {
            "Finally, some respect.", "Reporting for duty!", "Oh! Me? Okay!", "I wasn't sleeping. I was resting my eyes.",
            "Let's not die this time.", "HR said I'm 'irreplaceable'. Probably.", "Did someone say snacks?",
        };
        static readonly string[] SwitchBrute =
        {
            "Me lift. Me lead.", "Who wants a piggyback?", "I could bench-press this whole facility.",
            "Smash first. Think... later.", "These arms aren't for decoration.", "Leg day was yesterday. Arm day is forever.",
        };
        static readonly string[] SwitchSprinter =
        {
            "Already here. What took you?", "Zoom zoom, baby!", "I've been stretching for this moment.",
            "Blink and you'll miss me.", "Gotta go fast. Legally.", "My calves have been waiting.",
        };
        static readonly string[] SwitchBrainiac =
        {
            "Ah. The intellectual choice.", "I calculated a 3% chance of success. Thrilling!", "Finally, someone with a brain. Me.",
            "Statistically, this was inevitable.", "Let me consult my enormous head.", "I read the manual. All of it.",
        };
        static readonly string[] Taunts =
        {
            "Nyeh nyeh!", "Come and get me, tin can!", "BLEHHH!", "Over here, bolt-brain!", "You call that security?",
            "I'm right here! Wooo!", "Catch me if you can't!", "Beep boop. Your mother was a toaster.",
        };
        static readonly string[] Idle =
        {
            "Are we there yet?", "Can I touch the lasers?", "Is this a paid internship?", "My feet hurt.",
            "Did anyone else hear that?", "I think the floor is judging me.", "Sooo... we just stand here?",
            "I miss the break room.", "Remember to hydrate!", "This place has zero chill.",
        };
        static readonly string[] GiveUp = { "You're too far! I'll wait here.", "Wait up! ...okay, I'm staying." };
        static readonly string[] Stuck = { "I'm stuck! Little help?", "Something's in my way!" };

        static string Pick(string[] pool) => pool[Random.Range(0, pool.Length)];

        /// <summary>Greeting when the character is switched in (archetype flavoured, sometimes generic).</summary>
        public static string SwitchIn(ShifterCharacter c)
        {
            if (c == null || Random.value < 0.3f) return Pick(SwitchGeneric);
            switch (c.Archetype)
            {
                case Archetype.Brute: return Pick(SwitchBrute);
                case Archetype.Sprinter: return Pick(SwitchSprinter);
                default: return Pick(SwitchBrainiac);
            }
        }

        public static string Taunt() => Pick(Taunts);
        public static string IdleLine() => Pick(Idle);
        public static string GiveUpLine() => Pick(GiveUp);
        public static string StuckLine() => Pick(Stuck);
    }
}
