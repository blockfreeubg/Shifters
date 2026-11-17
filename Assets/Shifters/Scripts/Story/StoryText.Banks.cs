// =====================================================================================
//  SHIFTERS - StoryText.Banks.cs   [story]
//  Line banks: Max's wrong names (§24.1), catchphrases (§2.1), event barks (§8.2), counter-barks keyed to
//  tutorial ids (§8.4), ME-TV channels (§7.1), hologram taunts (§8.3), Percy (§24.2), intern reactions
//  (§24.3), posters (§24.4), tombstones (§24.5), HR (ALSO ME) lines (§15.2).
// =====================================================================================
using System.Collections.Generic;

namespace Shifters
{
    public static partial class StoryText
    {

        public static readonly string[] WrongNames =
        {
            "GARBANZO", "BRENDALINE", "TALL KEVIN", "SMALL KEVIN", "LEGS", "BIG HEAD", "THE ONE WITH THE FACE",
            "SANDWICH", "INTERN #2", "MUSCLES", "TINY LEGS", "GLASSES PERSON", "WHOEVER YOU ARE",
        };

        public static readonly string[] Catchphrases =
        {
            "There's no 'I' in TEAM. There's no TEAM in 'ME', either.",
            "Me, myself and I: the only team I need.",
            "If you want something done right, do it yourself. If you want EVERYTHING done right...",
            "I don't delegate. I DOMINATE.",
            "SHIFT CHANGE IN: NEVER.",
            "Table for one. As always.",
            "Names are for people who plan to stick around.",
            "Applause? No need. I'll do it myself.",
            "Never trust a team. Teams drop you.",
            "This has been ME. Thank you for watching ME.",
        };

        // ---- §8.2 event barks (rotate within each bank). Keys are VillainBarks event ids.
        public static readonly Dictionary<string, string[]> EventBarks = new Dictionary<string, string[]>
        {
            { "puzzle",     new[] { "Was that... cooperation? Ew.", "That's a violation. I'm logging it. In my diary." } },
            { "switching",  new[] { "Stop SHIFTING! Pick a favorite like a normal tyrant.", "Commit to someone! I committed to ME." } },
            { "lost",       new[] { "HA! {NAME} is gone. See? Teams are FRAGILE.", "Don't worry. I'll print another one." } },
            { "captured",   new[] { "My bots love a team player. They give them a little room." } },
            { "rescued",    new[] { "You RESCUED them? That's... loyalty. Disgusting." } },
            { "wiped",      new[] { "TEAM WIPED. That's what teams do. They wipe." } },
            { "flop",       new[] { "Don't FALL like that! Nobody's going to catch you. Trust me." } },
            { "thrown",     new[] { "Throwing coworkers? Finally, some leadership." } },
            { "leftBehind", new[] { "Leaving someone behind? NOW you're thinking like ME!" } },
            { "reprint",    new[] { "Ah, {CLONE}. I preferred the original. Kidding. I preferred neither." } },
            { "checkpoint", new[] { "Checkpoint. How cute. I never need checkpoints. I never fail." } },
            { "idle",       new[] { "Take your time. I'm only running the ENTIRE world over here." } },
            { "exitAll",    new[] { "Everyone? EVERYONE? Ugh." } },
        };
        public const string ReprintPrinterLine = "PRINTING {CLONE}... LOW ON INK";

        // ---- §8.4 counter-barks keyed to the existing tutorial ids (L1..L5 builders)
        public static readonly Dictionary<string, string> CounterBarks = new Dictionary<string, string>
        {
            { "welcome",      "Correction: you have ONE character. Yourself. The other two are strangers." },
            { "movement",     "W-A-S-D. Like me: Wonderful, Awesome, Superior, Dominant." },
            { "switching",    "Stop switching! Pick ONE favorite. I picked me." },
            { "perspective",  "Everyone sees the world differently? Wrong. Everyone sees it MY way." },
            { "bridge",       "Do NOT use your teammate as a bridge. That's SUPPORT. Support is ILLEGAL." },
            { "stacking",     "Standing on a coworker's head. That's how I got promoted." },
            { "throwing",     "Throwing coworkers? Finally, some leadership." },
            { "sacrifice",    "Sacrifice someone? Now you're management material!" },
            { "graduation",   "Who wrote these tutorials?! I'll find out. I always find out." },
            { "exit",         "Everyone in the elevator? EVERYONE?!" },
            { "l2_weights",   "Plates show the weight they need. Like my ego: ALL of it." },
            { "l3_bots",      "No friends, no feelings, no lunch breaks. Perfect." },
            { "l3_cells",     "Detention: where team players think about what they did." },
            { "l3_timed",     "You have four seconds. I have all the time in the world. It's mine." },
            { "l3_sacrifice", "INSERT INTERN TO CONTINUE. Finally, a chair that understands me." },
            { "l3_distract",  "Distracting my robots with a DANCE? ...They do love dancing." },
            { "l4_smart",     "The Banker's Exam. Designed for ONE genius. Hi." },
            { "l4_logic",     "Needs THREE? Who designed— oh. Me." },
            { "l5_meltdown",  "Self-destruct armed. Nothing personal. It's VERY personal." },
            { "l5_bots",      "More bots. They never ask for a raise." },
            { "l5_pulley",    "Heavier side sinks. Like your morale." },
            { "l5_pod",       "The escape pod fits ONE VIP. Count them. ONE." },
        };

        /// <summary>L1 has 18 tutorials: only these counter-barks fire there (bark budget, §8.4).</summary>
        public static readonly string[] L1CounterBarkIds = { "welcome", "movement", "switching", "bridge", "throwing", "graduation" };

        // ---- §7.1 ME-TV channels: name, hat accessory, line
        public static readonly string[][] MeTvChannels =
        {
            new[] { "ME NEWS",       "Crown",        "Breaking news: I'm still in charge." },
            new[] { "ME WEATHER",    "Visor",        "Forecast: grey, with a chance of more grey." },
            new[] { "ME SPORTS",     "Headband",     "Final score: ME 1, EVERYONE 0." },
            new[] { "ME FINANCE",    "Glasses",      "The economy is up. I checked. It's mine." },
            new[] { "ME SCIENCE",    "Antenna",      "Scientists agree: teams cause baldness. I am the scientists." },
            new[] { "ME KIDS",       "PropellerCap", "Kids! Sharing is overrated! Hoard your crayons!" },
            new[] { "ME COOKING",    "Beanie",       "Today: dinner for one. As always." },
            new[] { "ME LATE NIGHT", "Headphones",   "My guest tonight: me. Welcome, me!" },
        };

        // ---- §8.3 hologram taunt lines by lineId (StoryKit.HologramTauntSpot(lineId) looks them up via Taunt())
        public static readonly Dictionary<string, string> Taunts = new Dictionary<string, string>
        {
            { "l1.s1d1",   "This door needs TWO people? I'd use a remote." },
            { "l1.s2d2",   "Three lanes. Three interns. Who approved a TEAM-BUILDING EXERCISE?!" },
            { "l1.s6d6",   "Sacrifice someone? Management material!" },
            { "l1.s7gate", "All three plates? ...Ugh." },
            { "l2.panel",  "MAX 150 KG. That's me, my crown and my ego." },
            { "l2.pulley", "Coworkers as ballast. I approve." },
            { "l3.s1",     "Perfect employees: no friends, no lunch breaks." },
            { "l3.chair",  "That chair drained the world's color. It'll LOVE you." },
            { "l3.s4",     "Two bots. Three interns. Someone's getting a little room." },
            { "l4.s1",     "Designed for ONE genius. Hi." },
            { "l4.s2",     "An invisible path! Only a genius can see it. Or a nerd." },
            { "l4.latch",  "Three people for ONE door? Who designed— oh." },
            { "l5.s0",     "I built this tower myself. Now I'm demolishing it. Myself." },
            { "l5.s2",     "Lasers. Because I couldn't hire ANYONE." },
            { "l5.s4",     "Calibrated for personnel over 1.75 m. I'm 1.20. Unrelated." },
            { "metv.shot", "OW. That's my FACE." },
        };

        /// <summary>Taunt text for a lineId: the taunt table, else a catchphrase script line, else the id itself as text.</summary>
        public static string Taunt(string lineId)
        {
            if (string.IsNullOrEmpty(lineId)) return Catchphrases[0];
            if (Taunts.TryGetValue(lineId, out var t)) return t;
            if (CounterBarks.TryGetValue(lineId, out t)) return t;
            return lineId;
        }

        // ---- §24.2 Percy, §24.3 interns
        public static readonly string[] PercyLines =
        {
            "Every revolution starts in the break room.",
            "Stay warm. Stay together.",
            "I'm not crying, I'm condensing.",
            "Decaf is a weapon. Use it wisely.",
            "I've heard every meeting since 1987. Most could've been an email.",
            "The big one pushes. The fast one runs. The smart one reads. The gun one shoots.",
            "He's not evil. He's lonely. ...Also a little evil.",
        };
        public static readonly string[] BruteLines = { "I lift things.", "Can I punch the government?", "I'll carry the plan.", "Big feelings. Big arms." };
        public static readonly string[] SprinterLines = { "Already done!", "I'll run ahead!", "Too slow, too slow!", "Wait for me— no, I'll wait for you!" };
        public static readonly string[] BrainiacLines =
        {
            "Technically that's treason. Technically, I'm in.", "I have a theory.",
            "Statistically we're doomed. Emotionally I'm thrilled.", "That law is legally adorable.",
        };

        // ---- §24.4 posters (act 4 painted-over variants via PosterPaintedOver)
        public static readonly string[] Posters =
        {
            "THERE'S NO TEAM IN 'ME'.", "TEAMWORK IS CHEATING.", "REPORT ALL COOPERATION TO: ME", "TWO'S A CROWD. THREE'S A CRIME.",
            "HIGH-FIVES ARE LIMITED TO ONE (1) HAND.", "EMPLOYEE OF THE MONTH: ME (AGAIN)", "SOLO IS THE NEW TOGETHER.", "WHY SHARE? — ME",
            "HELPING IS HOW ACCIDENTS HAPPEN.", "THINK FOR YOURSELF. ONLY YOURSELF.", "GROUP PROJECTS ARE BANNED. YES, ALL OF THEM.",
            "THIS FACILITY HAS GONE 7,305 DAYS WITHOUT A TEAM.", "COLOR IS A LUXURY. LUXURY IS FOR ME.", "TODAY'S FORECAST: GREY.",
            "LUNCH: EAT ALONE. DIGEST ALONE.", "THE SOLO ACT: IT'S THE LAW.", "ARTICLE 3 DOES NOT EXIST.", "SMILE! YOU'RE ON ME-TV.",
            "ONE WORLD. ONE RULER. ONE CHAIR.", "SHIFT CHANGE IN: NEVER.", "TRUST NO ONE. ESPECIALLY ON TRUST FALLS.",
            "NAMES ARE FOR PEOPLE WHO PLAN TO STICK AROUND.", "THE COFFEE MACHINE IS NOT YOUR FRIEND.",
        };
        public const string PosterPaintedOver = "THERE'S A TEAM IN 'TEAM'";

        // ---- §24.5 tombstones
        public static readonly string[] Tombstones =
        {
            "HERE LIES THE MARKETING TEAM. 1998–2003. GREAT SYNERGY.",
            "HERE LIES CASUAL FRIDAY. NOW JUST \"FRIDAY\".",
            "HERE LIES THE OFFICE BIRTHDAY CAKE. EATEN BY ONE MAN.",
            "HERE LIES THE BOWLING LEAGUE. A GOOD RUN. AND A GUTTER BALL.",
            "HERE LIES \"WE\". \"ME\" IS FINE, THANKS.",
            "HERE LIES THE SUGGESTION BOX. NO SUGGESTIONS WERE TAKEN.",
            "HERE LIES THE GROUP CHAT. LAST MESSAGE: \"LOL\".",
            "HERE LIES THE BAND. NOW A ONE-MAN BAND.",
            "HERE LIES THE TUG-OF-WAR TEAM. BOTH SIDES LOST.",
            "HERE LIES THE BOARD OF DIRECTORS. FIRED BY THE CHAIRMAN, WHO WAS ALSO THE BOARD.",
            "HERE LIES THE THREE-LEGGED RACE. IT FELL OVER.",
            "HERE LIES THE HIGH-FIVE. LEFT HANGING.",
        };
        public static readonly string[] TombstoneFallbacks = { "RESERVED FOR YOU. — MANAGEMENT", "HERE LIES NOBODY. GOOD JOB. — PERCY" };

        /// <summary>"HERE LIES {NAME}. {CAUSE}. SHIFT {NN}. REPRINTED AS {CLONE}. (THE REPRINT IS FINE.)"</summary>
        public static string TombstoneFor(string name, string cause, int shiftNumber, string cloneName)
        {
            var c = string.IsNullOrEmpty(cause) ? "LOST IN THE LINE OF DUTY" : cause.ToUpperInvariant();
            var s = "HERE LIES " + (name ?? "AN INTERN").ToUpperInvariant() + ". " + c + ". SHIFT " + shiftNumber.ToString("00") + ".";
            if (!string.IsNullOrEmpty(cloneName)) s += " REPRINTED AS " + cloneName.ToUpperInvariant() + ". (THE REPRINT IS FINE.)";
            return s;
        }

        // ---- §15.2 HR (ALSO ME) SAYS (by TEAMWORK VIOLATIONS)
        public static string HrSays(int violations)
        {
            if (violations <= 5) return "HR (ALSO ME) SAYS: Barely cooperative. Promising.";
            if (violations <= 20) return "HR (ALSO ME) SAYS: Excessive cooperation. See me.";
            return "HR (ALSO ME) SAYS: This is a TEAM. I'm calling HR. I AM HR.";
        }

        /// <summary>Founders' statue plaque line for one intern ("GARY (I, II & III)").</summary>
        public static string FounderPlaque(string baseName, int generations)
        {
            baseName = (baseName ?? "INTERN").ToUpperInvariant();
            if (generations <= 1) return baseName;
            var sb = new System.Text.StringBuilder(baseName).Append(" (");
            for (int i = 1; i <= generations; i++)
            {
                if (i > 1) sb.Append(i == generations ? " & " : ", ");
                sb.Append(Roman(i));
            }
            return sb.Append(')').ToString();
        }

        static string Roman(int n)
        {
            switch (n) { case 1: return "I"; case 2: return "II"; case 3: return "III"; case 4: return "IV"; case 5: return "V"; case 6: return "VI"; case 7: return "VII"; case 8: return "VIII"; case 9: return "IX"; }
            return n.ToString();
        }
    }
}
