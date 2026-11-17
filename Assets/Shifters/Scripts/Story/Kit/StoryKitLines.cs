// =====================================================================================
//  SHIFTERS - StoryKitLines.cs   [owned by STORY_KIT]
//  Local string constants used by the story props until / besides story's StoryText (CAMPAIGN §8.3, §24.4, §24.5).
//  TODO(story): when StoryText publishes keyed getters, Resolve() can prefer them; the ids below stay valid.
// =====================================================================================
using System.Collections.Generic;
using UnityEngine;

namespace Shifters
{
    public static class StoryKitLines
    {
        public const string MaxId = "MAX", MaxName = "MAX ME", PercyId = "PERCY", PercyName = "PERCY";
        public static readonly Color MaxColor = new Color(0.91f, 0.72f, 0.23f), PercyColor = new Color(0.545f, 0.353f, 0.169f);

        /// <summary>§24.4 propaganda posters (index with Poster(i)).</summary>
        public static readonly string[] Posters =
        {
            "THERE'S NO TEAM IN 'ME'.", "TEAMWORK IS CHEATING.", "REPORT ALL COOPERATION TO: ME", "TWO'S A CROWD. THREE'S A CRIME.",
            "HIGH-FIVES ARE LIMITED TO ONE (1) HAND.", "EMPLOYEE OF THE MONTH: ME (AGAIN)", "SOLO IS THE NEW TOGETHER.", "WHY SHARE? - ME",
            "HELPING IS HOW ACCIDENTS HAPPEN.", "THINK FOR YOURSELF. ONLY YOURSELF.", "GROUP PROJECTS ARE BANNED. YES, ALL OF THEM.",
            "THIS FACILITY HAS GONE 7,305 DAYS WITHOUT A TEAM.", "COLOR IS A LUXURY. LUXURY IS FOR ME.", "TODAY'S FORECAST: GREY.",
            "LUNCH: EAT ALONE. DIGEST ALONE.", "THE SOLO ACT: IT'S THE LAW.", "ARTICLE 3 DOES NOT EXIST.", "SMILE! YOU'RE ON ME-TV.",
            "ONE WORLD. ONE RULER. ONE CHAIR.", "SHIFT CHANGE IN: NEVER.", "TRUST NO ONE. ESPECIALLY ON TRUST FALLS.",
            "NAMES ARE FOR PEOPLE WHO PLAN TO STICK AROUND.", "THE COFFEE MACHINE IS NOT YOUR FRIEND.",
        };
        public static string Poster(int i) => Posters[((i % Posters.Length) + Posters.Length) % Posters.Length];

        /// <summary>§24.5 ME-morial Park tombstones.</summary>
        public static readonly string[] Tombstones =
        {
            "HERE LIES THE MARKETING TEAM.\n1998-2003. GREAT SYNERGY.", "HERE LIES CASUAL FRIDAY.\nNOW JUST \"FRIDAY\".",
            "HERE LIES THE OFFICE BIRTHDAY CAKE.\nEATEN BY ONE MAN.", "HERE LIES THE BOWLING LEAGUE.\nA GOOD RUN. AND A GUTTER BALL.",
            "HERE LIES \"WE\".\n\"ME\" IS FINE, THANKS.", "HERE LIES THE SUGGESTION BOX.\nNO SUGGESTIONS WERE TAKEN.",
            "HERE LIES THE GROUP CHAT.\nLAST MESSAGE: \"LOL\".", "HERE LIES THE BAND.\nNOW A ONE-MAN BAND.",
            "HERE LIES THE TUG-OF-WAR TEAM.\nBOTH SIDES LOST.", "HERE LIES THE BOARD OF DIRECTORS.\nFIRED BY THE CHAIRMAN, WHO WAS ALSO THE BOARD.",
            "HERE LIES THE THREE-LEGGED RACE.\nIT FELL OVER.", "HERE LIES THE HIGH-FIVE.\nLEFT HANGING.",
        };
        public const string TombTemplate = "HERE LIES {NAME}.\n{CAUSE}. SHIFT {NN}.\nREPRINTED AS {CLONE}.\n(THE REPRINT IS FINE.)";
        public const string TombReserved = "RESERVED FOR YOU.\n- MANAGEMENT";
        public const string TombNobody = "HERE LIES NOBODY.\nGOOD JOB.\n- PERCY";

        public static readonly string[] Ticker =
        {
            "SHIFT CHANGE IN: NEVER", "TODAY'S FORECAST: GREY", "EMPLOYEE OF THE MONTH: ME (AGAIN)", "MARKETS: UP. ME: UP. YOU: STAY PUT.",
            "BREAKING: TEAMWORK STILL ILLEGAL", "SPORTS: ME WINS AGAIN (SOLE ENTRANT)",
        };

        public static readonly string[] PercyIdle =
        {
            "Psst. Nice teamwork. I wrote those tutorials. Don't tell him.",
            "I've overheard every meeting since 1987. Most could've been an email.",
            "He calls me an appliance. I call him... a regular.",
            "Three interns. One coffee machine. Now THAT'S a team.",
        };

        static Dictionary<string, string> _lines;

        /// <summary>Line text for an id (taunt ids "L1.S1.D1", beat ids "ch05.chair", "percy.L1.cp5"...). Unknown ids that
        /// look like text (contain a space) are returned as-is; other unknown ids return "".</summary>
        public static string Resolve(string id)
        {
            if (string.IsNullOrEmpty(id)) return "";
            if (_lines == null)
            {
                _lines = new Dictionary<string, string>
                {
                    { "L1.S1.D1", "This door needs TWO people? I'd use a remote." },
                    { "L1.S2.D2", "Three lanes. Three interns. Who approved a TEAM-BUILDING EXERCISE?!" },
                    { "L1.S6.D6", "Sacrifice someone? Management material!" },
                    { "L1.S7.Gate", "All three plates? ...Ugh." },
                    { "L2.S2.Panel", "MAX 150 KG. That's me, my crown and my ego." },
                    { "L2.S4.Pulley", "Coworkers as ballast. I approve." },
                    { "L3.S1", "Perfect employees: no friends, no lunch breaks." },
                    { "ch05.chair", "That chair drained the world's color. It'll LOVE you." },
                    { "L3.S4", "Two bots. Three interns. Someone's getting a little room." },
                    { "L4.S1", "Designed for ONE genius. Hi." },
                    { "L4.S2", "An invisible path! Only a genius can see it. Or a nerd." },
                    { "L4.S4.Latch", "Three people for ONE door? Who designed- oh." },
                    { "L5.S0", "I built this tower myself. Now I'm demolishing it. Myself." },
                    { "L5.S2", "Lasers. Because I couldn't hire ANYONE." },
                    { "L5.S4", "Calibrated for personnel over 1.75 m. I'm 1.20. Unrelated." },
                    { "percy.L1.cp5", "Psst. Nice teamwork. I wrote those tutorials. Don't tell him." },
                    { "percy.wink", ";)" },
                };
            }
            if (_lines.TryGetValue(id, out var s)) return s;
            return id.IndexOf(' ') >= 0 ? id : "";
        }
    }
}
