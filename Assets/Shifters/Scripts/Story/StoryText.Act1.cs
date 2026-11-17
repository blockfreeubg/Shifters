// =====================================================================================
//  SHIFTERS - StoryText.Act1.cs   [story]
//  PROLOGUE + ACT I (DAY SHIFT): C00 The Morning Broadcast, act cards, C01, C02, C03 (+ch03 beats),
//  C04 The Break Room, Level-Complete reactions, L1/L2 Percy lines, the placeholder end card.
// =====================================================================================
namespace Shifters
{
    public static partial class StoryText
    {
        // ---- act cards (§6): act label + title; StoryScripts "card.I" .. "card.FINALE"
        public static readonly string[][] ActCards =
        {
            new[] { "I",        "ACT I · DAY SHIFT",          "YOU'RE HIRED. ALONE." },
            new[] { "II",       "ACT II · SWING SHIFT",       "THE GREAT COLOR HEIST" },
            new[] { "III",      "ACT III · GRAVEYARD SHIFT",  "DON'T SPLIT UP." },
            new[] { "IV",       "ACT IV · OVERTIME",          "STORM THE TOWER." },
            new[] { "FINALE",   "FINALE · SHIFT CHANGE",      "ALL IN FAVOR?" },
        };

        public const string GameTitle = "SHIFTERS";
        public const string CampaignTagline = "THREE INTERNS. ONE VILLAIN. ZERO PERMISSION TO WORK TOGETHER.";
        public const string EndCardPlaceholder = "MAX REMAINS IN POWER... FOR NOW.";

        static void ActCard(StoryScriptBuilder b, int i)
        {
            b.Sfx(SfxId.ActCard).Card(ActCards[i][1], ActCards[i][2]);
        }

        static void RegisterAct1()
        {
            for (int i = 0; i < ActCards.Length; i++)
            {
                int k = i;
                StoryScripts.Define("card." + ActCards[i][0], b => { ActCard(b, k); b.Wait(2.5f); });
            }

            // ---------------- C00 THE MORNING BROADCAST (30 s, blocking, L1 Arrival Bay)
            StoryScripts.Define("C00", b => b.Blocking().Music(MusicId.Villain)
                .Tv("ME NEWS").Puppet("Monologue")
                .Broadcast("MAX", Expression.Smug, "Gooood morning, world! It's ME.", true)
                .Broadcast("MAX", Expression.Smug, "Your President, CEO, Banker, Judge and Employee of the Month.", true)
                .Tv("ME WEATHER")
                .Broadcast("MAX", Expression.Neutral, "Today's forecast: grey. Tomorrow: greyer. The weekend: cancelled.")
                .Tv("ME NEWS")
                .Broadcast("MAX", Expression.Angry, "Reminder: the SOLO ACT is the law. No teams. No group projects. No high-fives.", true)
                .Tv("off")
                .ShotTeam(5.5f, 45f, 0f).CameraDrift(0.25f)
                .Gesture("{BRUTE}", VisualAction.Wave).Wait(0.7f)
                .Sfx(SfxId.Klaxon)
                .Line("KLAXON", "UNAUTHORIZED WAVE DETECTED.", true)
                .Face("{BRUTE}", Expression.Sad).Face("{SPRINTER}", Expression.Surprised).Wait(0.6f).FullOnly()
                .Broadcast("MAX", Expression.Smug, "New SHIFTERS. Welcome to orientation. Do it ALONE. Your shift never ends.", true)
                .Shot("story.percy", null, 40f, 0.6f).FullOnly()
                .Sfx(SfxId.Gurgle)
                .Line("PERCY", "(o_o) ... ;)")
                .Banner(GameTitle, CampaignTagline, BannerStyle.Story)
                .Wait(2.5f)
                .Music(MusicId.Training));

            // ---------------- SHIFT 01: ME-TV rule #1 at L1 start (non-blocking) + Percy at checkpoint 5
            StoryScripts.Define("ch01.start", b => b
                .Line("MAX", "Rule number one at ME INDUSTRIES: no teamwork.")
                .Line("MAX", "Rule number two: see rule number one."));
            StoryScripts.Define("l1.percy", b => b.Once().Sfx(SfxId.Gurgle)
                .Line("PERCY", "Psst. Nice teamwork. I wrote those tutorials.")
                .Line("PERCY", "Don't tell him."));

            // ---------------- C01 L1 Level Complete (PiP hold)
            StoryScripts.Define("C01", b => b.Priority()
                .Line("MAX", Expression.Shocked, "Three interns. Graduating. TOGETHER?")
                .Puppet("CrownSlip").Sfx(SfxId.Cymbal)
                .Line("MAX", Expression.Angry, "That's a TEAM. Transfer them to the FACTORY. Permanently.", true));

            // ---------------- C02 L2 start (ME-TV) + Percy corner (proximity / checkpoint.1)
            StoryScripts.Define("C02", b => b
                .Line("MAX", "Welcome to the Grey Paint Factory. Quota: one thousand statues of ME.")
                .Line("MAX", "Current: zero."));
            StoryScripts.Define("c02.percy", b => b.Once().Sfx(SfxId.Gurgle)
                .Line("PERCY", "Psst. Over here. The coffee machine.")
                .Line("PERCY", "I'm Percy. I've heard every meeting since 1987.")
                .Line("PERCY", "That thing you three keep doing? It's called teamwork.")
                .Line("PERCY", "It's illegal. Keep doing it.")
                .Line("{BRUTE}", "I've been talking to coffee for years."));

            // ---------------- C03 boss intro: THE DOWNSIZER (non-blocking, <= 8 s)
            StoryScripts.Define("C03", b => b.Priority().Music(MusicId.Boss)
                .Line("MAX", "You shipped yourselves out of MY factory. In MY boxes.")
                .Line("MAX", Expression.Angry, "Three interns? That's two too many. DOWNSIZER, make it one.", true)
                .Sfx(SfxId.BossRoar)
                .Line("DOWNSIZER", "TEAM DETECTED. INITIATING DOWNSIZING.")
                .Banner("THE DOWNSIZER", "DEPT. OF HUMAN RESOURCES · RATED FOR ONE (1) EMPLOYEE", BannerStyle.Boss));
            StoryScripts.Alias("ch03.intro", "C03");

            StoryScripts.Define("ch03.p1", b => b.Priority().Line("MAX", "Phase one: quota. Hold still, it's for your file."));
            StoryScripts.Define("ch03.jam", b => b.Priority().Sfx(SfxId.Stamp).Line("DOWNSIZER", "PRESS JAMMED. FILING... A COMPLAINT."));
            StoryScripts.Define("ch03.p2", b => b.Priority().Line("MAX", "Downsizing protocol! Rightsizing! Wrongsizing! ...Error."));
            StoryScripts.Define("ch03.p3", b => b.Priority().Line("DOWNSIZER", "MANUAL REVIEW. STEP ON THE SCALE. ONE (1) EMPLOYEE ONLY."));
            StoryScripts.Define("ch03.overload", b => b.Priority()
                .Line("DOWNSIZER", "LOAD: THREE EMPLOYEES. RATED FOR: ONE.")
                .Line("MAX", Expression.Shocked, "It can downsize ANYONE. It just can't downsize... three."));

            // ---------------- C04 THE BREAK ROOM (blocking, after the Downsizer, L6 annex)
            StoryScripts.Define("C04", b => b.Blocking().Music(MusicId.Factory)
                .Shot("story.breakroom", "story.percy", 45f, 0f).ShotTeam(5f, 45f, 0f).FullOnly()
                .Sfx(SfxId.Gurgle)
                .Line("PERCY", "You beat the Downsizer. Together. Nobody's done that in twenty years.")
                .Line("{BRUTE}", Expression.Surprised, "We're a team?")
                .Line("{SPRINTER}", Expression.Happy, "We're a TEAM!")
                .Line("{BRAINIAC}", "Technically, three people is a BOARD.")
                .Line("PERCY", "Exactly.").FullOnly()
                .Beat("c04.scroll")
                .Line("{BRAINIAC}", "Article 3: a team of three may capture the Supreme Executive. And impeach him.", true)
                .Line("PERCY", "That's why he banned teams. It's the only way to remove him.", true)
                .Line("PERCY", "You'll need evidence. And color. He keeps both in his bank.")
                .Beat("c04.whiteboard")
                .Line("PERCY", "It's not a robbery. It's a RE-DISTRIBUTION.", true)
                .Sfx(SfxId.Stamp)
                .Beat("c04.roles")
                .Toast("{BRUTE}: THE MUSCLE · {SPRINTER}: THE WHEELS").FullOnly()
                .Toast("{BRAINIAC}: THE BRAINS · {GUNNER}: ...ALSO THE DISTRACTION").FullOnly()
                .Line("{BRAINIAC}", Expression.Smug, "Flawless.")
                .Do(() => ActCardNow(1)));
            StoryScripts.Alias("ch03.outro", "C04");

            // ---------------- placeholder end card (MVP rung R0 when the campaign stops early)
            StoryScripts.Define("end.placeholder", b => b.Blocking().Music(MusicId.Villain)
                .Line("MAX", Expression.Laugh, "Ha! A team? Of INTERNS? Come back when you're a dynasty.", true)
                .Puppet("OneHandClap").Sfx(SfxId.Cymbal)
                .Banner(EndCardPlaceholder, "TO BE CONTINUED. (THE INTERNS ARE STILL WORKING ON IT.)", BannerStyle.Story)
                .Wait(3f));

            // ---------------- Level-Complete reactions per chapter (PiP; the campaign plays "react.<CH>")
            StoryScripts.Define("react.CH02", b => b.Priority().Line("MAX", Expression.Angry, "You shipped yourselves out of MY factory. In MY boxes."));
            StoryScripts.Define("react.CH05", b => b.Priority().Line("MAX", Expression.Pout, "You found my memo? That was PRIVATE. I signed it. Twice."));
            StoryScripts.Define("react.CH06", b => b.Priority().Line("MAX", Expression.Shocked, "You PASSED my exam? It was designed for ONE genius!"));
            StoryScripts.Define("react.CH12", b => b.Priority().Line("MAX", Expression.Angry, "Destination: HOME? That's MY home! Wipe your feet!"));
        }

        /// <summary>Raise an act card right now (also used by C04/C07/C10 and the campaign).</summary>
        public static void ActCardNow(int actIndex)
        {
            if (actIndex < 0 || actIndex >= ActCards.Length) return;
            GameEvents.RaiseChapterCard(ActCards[actIndex][1], ActCards[actIndex][2]);
            Shifters.Sfx.Play(SfxId.ActCard);
        }
    }
}
