// =====================================================================================
//  SHIFTERS - CharacterPools.cs   [owned by the CORE_SESSION agent]
//  Flavour data for CharacterGenerator: names, palette, titles, personalities, catchphrases,
//  skin/hair colors, accessory biases and re-clone suffixes. Pure data - no logic.
//  Names must not contain roman numerals (re-clones append " II", " III"...).
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    /// <summary>Weighted accessory choice (higher weight = more likely).</summary>
    public struct AccessoryWeight
    {
        public AccessoryType Type;
        public int Weight;
        public AccessoryWeight(AccessoryType type, int weight) { Type = type; Weight = weight; }
    }

    public static class CharacterPools
    {
        // ------------------------------------------------------------------ names (unique within a team)
        public static readonly string[] SharedNames =
        {
            "Gary", "Brenda", "Linda From Accounting", "Mildred", "Kevin", "Bartholomew", "Doug", "Agnes", "Reginald",
            "Beans", "Wobbles McGee", "Pam", "Dave (Other Dave)", "Nugget", "Gertrude", "Fernando", "Ingrid", "Biscuit",
            "Priya", "Jamal", "Mei", "Sven", "Yolanda", "Kofi", "Rosa", "Boris", "Fatima", "Nigel", "Esperanza", "Kenji",
            "Svetlana", "Toby", "Ursula", "Wendell", "Mr. Pickles", "Barb", "Lord Wiggles", "Patricia", "Hank", "Delphine",
            "Olu", "Ravi", "Tuesday", "Greg (Intern)", "Cheryl", "Ignatius", "Pickle", "Marge", "Dimitri", "Aunt Carol",
        };

        public static readonly string[] BruteNames =
        {
            "Big Steve", "Chad Thunderthighs", "Tank", "Brick", "Moose", "Big Ron", "Helga", "Bronco", "Thor (Not That One)",
            "Mama Bear", "Gus the Bus", "Boulder",
        };

        public static readonly string[] SprinterNames =
        {
            "Zippy", "Tina Turbo", "Sprocket", "Dash", "Speedy Gonzaga", "Flash Gordo", "Skidmark", "Nitro Nancy",
            "Hops", "Rocket Rita", "Jet", "Zoom Zoom",
        };

        public static readonly string[] BrainiacNames =
        {
            "Dr. Toast", "Professor Noodle", "Dr. Fizzwick", "Einstein Jr.", "Big Brain Brian", "Doctor Doctor",
            "Prof. Wobblesworth", "Dr. Kettle", "Ada Byte", "Dr. Clipboard", "Professor Plum Jam", "The Algorithm",
        };

        // ------------------------------------------------------------------ colors: bright but controlled, hue-separated
        // (no teal/cyan: that's the puzzle-part color language)
        public static readonly Color[] Palette =
        {
            Hex(0xFF5A4E), // tomato
            Hex(0xFF9F43), // tangerine
            Hex(0xFFD23F), // sunflower
            Hex(0xA8E04A), // lime
            Hex(0x3DCB6C), // grass
            Hex(0x4EA8FF), // sky
            Hex(0x4361EE), // royal
            Hex(0x9B5DE5), // violet
            Hex(0xF15BB5), // magenta
            Hex(0xFF85A1), // bubblegum
            Hex(0xC68B59), // caramel
            Hex(0xE84A8A), // raspberry
        };

        public static readonly string[] PaletteNames =
        {
            "Tomato", "Tangerine", "Sunflower", "Lime", "Grass", "Sky", "Royal", "Violet", "Magenta", "Bubblegum", "Caramel", "Raspberry",
        };

        // ------------------------------------------------------------------ titles
        public static readonly string[] BruteTitles =
        {
            "THE BRUTE", "THE BRUTE", "THE MUSCLE", "THE FORKLIFT", "THE HUMAN WALL", "THE UNIT", "THE MEAT SHIELD", "THE BIG ONE",
            "THE LIFTER", "THE BULLDOZER",
        };
        public static readonly string[] SprinterTitles =
        {
            "THE SPRINTER", "THE SPRINTER", "THE BLUR", "THE ZOOMER", "THE CARDIO", "THE FAST ONE", "THE SPEED DEMON", "THE NOODLE",
            "THE LEG DAY", "THE ROADRUNNER",
        };
        public static readonly string[] BrainiacTitles =
        {
            "THE BRAINIAC", "THE BRAINIAC", "THE EGGHEAD", "THE BIG BRAIN", "THE NERD", "THE GENIUS (SELF-PROCLAIMED)", "THE THINK TANK",
            "THE CALCULATOR", "THE KNOW-IT-ALL", "THE PROFESSOR",
        };

        // ------------------------------------------------------------------ personalities (>= 12 per archetype)
        public static readonly string[] BrutePersonalities =
        {
            "Lifts. Mostly sandwiches.",
            "Has never lost an arm-wrestle. Or a sandwich.",
            "Thinks stairs are a personal insult.",
            "Once bench-pressed a vending machine for a granola bar.",
            "Communicates exclusively in grunts and thumbs-ups.",
            "Was banned from the gym for 'too much gym'.",
            "Carries coworkers like groceries. Drops them like groceries.",
            "Believes every problem is a door that needs pushing.",
            "Gentle giant. Mostly giant.",
            "Cries at commercials. Then punches a wall.",
            "Was hired as a forklift. Nobody corrected the paperwork.",
            "Can open any jar. Cannot open emails.",
            "Warms up by stretching the furniture.",
            "Has a tattoo that just says 'MOM?'",
            "Counts reps out loud. Gets lost after four.",
        };

        public static readonly string[] SprinterPersonalities =
        {
            "Has never finished a sent",
            "Runs to the coffee machine. Runs from responsibility.",
            "Legs: 90%. Brain: buffering.",
            "Arrived yesterday. For tomorrow's meeting.",
            "Thinks walking is a conspiracy.",
            "Owns 47 pairs of sneakers and zero brakes.",
            "Got a speeding ticket. On foot.",
            "Vibrates slightly when idle.",
            "Treats every hallway like a final lap.",
            "Once outran their own shadow. It's still looking.",
            "Powered entirely by energy drinks and spite.",
            "Takes the stairs. All of them. At once.",
            "Has a personal record for leaving meetings early.",
            "Doesn't know where they're going, but they'll be there first.",
        };

        public static readonly string[] BrainiacPersonalities =
        {
            "Thinks gravity is a social construct.",
            "Has read the manual. Wrote a better one.",
            "IQ so high it needs a ladder.",
            "Corrects the grammar on the safety signs.",
            "Solves puzzles. Causes problems.",
            "Has a PhD in Overthinking.",
            "Brought a calculator to a fistfight.",
            "Speaks fluent binary. And sarcasm.",
            "Head is 40% brain, 60% more brain.",
            "Once hacked a toaster just to apologize to it.",
            "Knows exactly how doomed we are, to three decimal places.",
            "Allergic to cardio. And to being wrong.",
            "Claims the whole experiment was their idea.",
            "Has a five-year plan. For lunch.",
        };

        // ------------------------------------------------------------------ catchphrases
        public static readonly string[] BruteCatchphrases =
        {
            "SMASH first. Think never.", "I lift things.", "Who wants a piggyback?", "Stand back. I'm doing a strength.",
            "Is it heavy? Good.", "Protein time!", "Me go. You stay.", "Doors are just walls that gave up.",
        };
        public static readonly string[] SprinterCatchphrases =
        {
            "Gotta go fast!", "Already there!", "Wait, where are we?", "Last one there is me. Never.",
            "Zoom zoom, losers!", "My legs are writing checks!", "Cardio is my cardio.", "Blink and you'll miss me!",
        };
        public static readonly string[] BrainiacCatchphrases =
        {
            "Technically, I'm right.", "Fascinating. Terrible, but fascinating.", "Let me calculate... nope.",
            "I have a theory.", "Science, baby!", "That's not how physics works. Oh. It is.", "Per my last email...",
            "Hypothesis: we die. Let's test it!",
        };

        // ------------------------------------------------------------------ bodies
        public static readonly Color[] SkinTones =
        {
            Hex(0xFFE0C7), Hex(0xF7CBA6), Hex(0xEDB88B), Hex(0xD69E6E), Hex(0xB97D4B), Hex(0x8D5A35), Hex(0x6B4226), Hex(0x4A2E1B),
        };
        /// <summary>Rare novelty skins (lab accidents). ~4% chance.</summary>
        public static readonly Color[] NoveltySkins = { Hex(0xA6E3A1), Hex(0xB9C6FF), Hex(0xFFB3D1) };

        public static readonly Color[] HairColors =
        {
            Hex(0x1C1410), Hex(0x3B2416), Hex(0x6A4126), Hex(0x8E3B1F), Hex(0xD9B26A), Hex(0xC2652B), Hex(0x9A9A9A), Hex(0xECECEC),
        };
        /// <summary>Fun dyed hair (~12% chance).</summary>
        public static readonly Color[] DyedHair = { Hex(0xFF5FA2), Hex(0x4FA3FF), Hex(0x6BE36B), Hex(0xB07CFF) };

        public static readonly AccessoryWeight[] BruteAccessories =
        {
            new AccessoryWeight(AccessoryType.HardHat, 4), new AccessoryWeight(AccessoryType.Headband, 3), new AccessoryWeight(AccessoryType.Mustache, 4),
            new AccessoryWeight(AccessoryType.Beanie, 1), new AccessoryWeight(AccessoryType.Crown, 1), new AccessoryWeight(AccessoryType.None, 1),
        };
        public static readonly AccessoryWeight[] SprinterAccessories =
        {
            new AccessoryWeight(AccessoryType.Visor, 4), new AccessoryWeight(AccessoryType.Headband, 3), new AccessoryWeight(AccessoryType.PropellerCap, 4),
            new AccessoryWeight(AccessoryType.Headphones, 2), new AccessoryWeight(AccessoryType.Beanie, 1), new AccessoryWeight(AccessoryType.None, 1),
        };
        public static readonly AccessoryWeight[] BrainiacAccessories =
        {
            new AccessoryWeight(AccessoryType.Glasses, 5), new AccessoryWeight(AccessoryType.BowTie, 4), new AccessoryWeight(AccessoryType.Antenna, 3),
            new AccessoryWeight(AccessoryType.Headphones, 1), new AccessoryWeight(AccessoryType.Crown, 1), new AccessoryWeight(AccessoryType.None, 1),
        };

        // ------------------------------------------------------------------ re-clone suffixes (appended to the personality)
        public static readonly string[] RecloneSuffixes =
        {
            "(reprinted, slightly damp)", "(clone, still warm)", "(now with 12% fewer memories)", "(printer was low on ink)",
            "(smells faintly of toner)", "(assembled from leftovers)", "(warranty void)", "(factory refurbished)",
            "(some assembly required)", "(slightly off-color)", "(remembers nothing, regrets everything)", "(returned, no receipt)",
        };

        public const string RecloneMarker = " (";

        static Color Hex(int rgb) => new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
    }
}
