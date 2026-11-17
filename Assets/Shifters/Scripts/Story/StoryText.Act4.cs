// =====================================================================================
//  SHIFTERS - StoryText.Act4.cs   [story]
//  ACT IV (OVERTIME) + FINALE: C11 Overtime, C12 To the Top, ch13 beats, C13 Caught, C14 The Hearing,
//  ch14 beats, C15 Sunrise (interactive, WHO RULES TODAY? -> ShiftocracyEnding), C16 post-credits,
//  and the Shiftocracy decree bank (§24.6).
// =====================================================================================
namespace Shifters
{
    public static partial class StoryText
    {
        /// <summary>ch13.punch rotation (ME-CHA's fist takes a hit).</summary>
        public static readonly string[] PunchLines =
        {
            "KNUCKLE INTEGRITY: 66%.",
            "KNUCKLE INTEGRITY: 33%.",
            "KNUCKLE INTEGRITY: 0%. PILOT SAYS: \"OW! That's assault on a FIST!\"",
        };

        public static readonly string[] Monologues =
        {
            "You think you've won? I've never needed—",
            "Do you know who I AM?",
            "Let me tell you about my first billion—",
        };

        // ---- §24.6 decrees (one per archetype at random; the gunner adds a weapon line)
        public static readonly string[] DecreesBrute =
        {
            "All doors must be pushable. Even the pull ones.",
            "Lunch is four hours. Second lunch is mandatory.",
            "Heavy things now count as carry-on.",
        };
        public static readonly string[] DecreesSprinter =
        {
            "Hallways are one-way, and that way is FAST.",
            "Waiting in line is illegal. Lines must jog.",
            "The weekend starts Wednesday. I got there early.",
        };
        public static readonly string[] DecreesBrainiac =
        {
            "Every citizen gets one free hint per day. Except Gary.",
            "Homework is abolished. I already did it.",
            "The color beige is under review.",
        };
        /// <summary>Vista beat per archetype decree index (fired as "c15.vista.&lt;archetype&gt;.&lt;i&gt;").</summary>
        public static readonly string[] VistaNotes = { "sandwich statues / pushable doors", "chevron banners", "billboards" };

        public static string DecreeFor(Archetype a, int i)
        {
            var bank = a == Archetype.Brute ? DecreesBrute : a == Archetype.Sprinter ? DecreesSprinter : DecreesBrainiac;
            return bank[((i % bank.Length) + bank.Length) % bank.Length];
        }

        /// <summary>The gunner's add-on decree for a weapon (§24.6).</summary>
        public static string GunDecree(WeaponData w)
        {
            if (w == null) return null;
            switch (w.Type)
            {
                case WeaponType.Pistol: return "'Pew-pew' is now a greeting.";
                case WeaponType.Shotgun: return "The boomstick goes in a museum. Behind glass. Thick glass.";
                case WeaponType.LaserGun: return "Disco Fridays. Mandatory.";
                case WeaponType.Slingshot: return "Pebbles are legal tender.";
            }
            switch (w.Variant)
            {
                case ExperimentalVariant.LeafBlower: return "Hot air is now renewable energy.";
                case ExperimentalVariant.TShirtCannon: return "Free shirts for everyone. One size fits Brute.";
                case ExperimentalVariant.SayCheese: return "Every photo must include three people.";
                case ExperimentalVariant.PaintBlaster: return "Grey is now illegal. Ironically.";
            }
            var n = w.Variant.ToString();
            if (n.Contains("Plunger")) return "Every home gets a free plunger.";
            return "Rubber chickens are a protected species.";
        }

        public const string ClosingCardTitle = "THE WORLD IS BRIGHT AGAIN.";
        public const string ClosingCardSub = "(THE INTERNS ARGUE A LOT.)";

        static void RegisterAct4()
        {
            SetToken("MONOLOGUE", Monologues[0]);
            SetToken("PUNCH", PunchLines[0]);

            // ---------------- C11 OVERTIME (L5 load card + start, non-blocking)
            StoryScripts.Define("C11", b => b.Music(MusicId.Chaos)
                .Toast("The van crashed into ME TOWER. Max armed the self-destruct. Of course he did.").FullOnly()
                .Line("MAX", Expression.Angry, "Coming up? Fine. I'll bring the building DOWN.", true)
                .Sfx(SfxId.Klaxon)
                .Line("ALARM", "TEAMWORK DETECTED. SELF-DESTRUCT ARMED.", true)
                .Line("PERCY", "(PERCY: OFFLINE)").FullOnly());
            StoryScripts.Define("ch12.pod", b => b.Once().Line("PA", "AUTOPILOT: DESTINATION... HOME."));

            // ---------------- C12 TO THE TOP (blocking, 15 s, CH13 start)
            StoryScripts.Define("C12", b => b.Blocking().Music(MusicId.Villain)
                .ShotTeam(6f, 55f, 0f).CameraDrift(0.3f)
                .Sfx(SfxId.BossStomp)
                .Toast("(^_^)/ HI").FullOnly()
                .Line("MAX", Expression.Pout, "You made it. Together. Ugh.")
                .Line("MAX", Expression.Laugh, "No matter. I have the ultimate team: ME!", true)
                .Beat("c12.mecha")
                .Sfx(SfxId.Kazoo)
                .Music(MusicId.FinalBoss)
                .Banner("THE ONE-MAN BAND", "THE EXECUTIVE · APPROVAL RATING 100%", BannerStyle.Boss)
                .Wait(2f));
            StoryScripts.Alias("ch13.start", "C12");

            StoryScripts.Define("ch13.p1", b => b.Priority().Line("MAX", Expression.Angry, "The FIST of the strong one!"));
            StoryScripts.Define("ch13.arm", b => b.Priority().Sfx(SfxId.BossHit).Line("MAX", Expression.Pout, "MY ARM! ...Fine. I do everything one-handed anyway."));
            StoryScripts.Define("ch13.p2", b => b.Priority().Line("MAX", Expression.Angry, "The SKATES of the fast one!"));
            StoryScripts.Define("ch13.legs", b => b.Priority().Sfx(SfxId.BossHit).Line("MAX", Expression.Shocked, "MY SKATES! Who puts SPEED BUMPS in a penthouse?!"));
            StoryScripts.Define("ch13.p3", b => b.Priority()
                .Line("MAX", Expression.Dizzy, "The BRAIN of the smart one! ...why am I so... sleepy?")
                .Line("PERCY", "Decaf. Since Tuesday."));
            StoryScripts.Define("ch13.eject", b => b.Priority().Sfx(SfxId.Klaxon)
                .Line("ME-CHA", "EJECT. EJECT. EJECT.")
                .Line("MAX", Expression.Smug, "APPROVAL RATING: 1%. That's ME. I still approve."));
            int mono = 0;
            StoryScripts.Define("ch13.monologue", b => b
                .Do(() => SetToken("MONOLOGUE", Pick(Monologues, ref mono)))
                .Line("MAX", Expression.Smug, "{MONOLOGUE}"));
            // CH13 beats requested by the lead (fixes/story.md). Non-blocking barks. Max still never says real names.
            StoryScripts.Define("ch13.cutout", b => b.Sfx(SfxId.Boo)
                .Line("CARDBOARD MAX", "SURPRISE PERFORMANCE REVIEW!")
                .Line("{SPRINTER}", Expression.Surprised, "...it's cardboard.")
                .Line("PERCY", "Like his personality."));
            StoryScripts.Define("ch13.unionbreak", b => b.Line("MAX", Expression.Pout, "On BREAK? Fine. Union rules. ...I wrote those too."));
            StoryScripts.Define("ch13.stuck", b => b.Line("MAX", Expression.Angry, "Who installed SOFT marble?! ...Me. For the ambience."));
            int punch = 0;
            StoryScripts.Define("ch13.punch", b => b.Sfx(SfxId.MetalHit)
                .Do(() => SetToken("PUNCH", Pick(PunchLines, ref punch)))
                .Line("ME-CHA", "{PUNCH}"));
            StoryScripts.Define("ch13.bumps", b => b.Line("PA", "SPEED BUMPS DEPLOYED. PLEASE SKATE RESPONSIBLY."));
            StoryScripts.Define("ch13.yawn", b => b.Priority()
                .Line("MAX", Expression.Dizzy, "*yawn* ...five more minutes.")
                .Line("PERCY", "Decaf. Told you."));
            StoryScripts.Define("ch13.firewall", b => b.Line("MAX", Expression.Angry, "GLASSES PERSON! That firewall was ALSO me!"));
            StoryScripts.Define("ch13.chase", b => b.Priority().Line("MAX", Expression.Smug, "Catch me? Nobody has EVER caught me."));
            StoryScripts.Define("ch13.tag", b => b.Line("MAX", Expression.Shocked, "TAG?! LEGS, I never agreed to TAG!"));
            StoryScripts.Define("ch13.dizzy", b => b.Line("MAX", Expression.Dizzy, "Ow. I was MID-SENTENCE."));
            StoryScripts.Define("ch13.carried", b => b.Priority().Line("MAX", Expression.Angry, "UNHAND ME! I have lawyers! I AM the lawyers!"));
            StoryScripts.Define("ch13.intake", b => b.Priority().Sfx(SfxId.Stamp).Line("INTAKE", "AUTHORIZED: A TEAM OF THREE. ARTICLE 3."));
            StoryScripts.Define("ch13.wreck", b => b.Line("ME-CHA", "...PLEASE RATE YOUR EXPERIENCE.").Toast("CUSTOMER RATING: 1 STAR"));
            StoryScripts.Define("ch13.argue", b => b.Line("MAX", Expression.Angry, "HOW DARE YOU! I'll have you know I invented dancing!"));

            // ---------------- C13 CAUGHT (blocking, 12 s)
            StoryScripts.Define("C13", b => b.Blocking()
                .ShotTeam(5f, 45f, 0f)
                .Sfx(SfxId.Stamp)
                .Banner("MAXIMILIAN ME HAS BEEN CAPTURED", "Incident report: was caught by a team.", BannerStyle.Danger)
                .Wait(1.2f)
                .Line("MAX", Expression.Angry, "This is ILLEGAL!", true)
                .Line("{BRAINIAC}", Expression.Smug, "Article 3.")
                .Line("MAX", Expression.Pout, "...I hate Article 3.")
                .Beat("c13.cage")
                .Line("PERCY", "I'd hug you all, but I'm ninety degrees."));
            StoryScripts.Alias("ch13.caught", "C13");

            // ---------------- C14 THE HEARING (blocking, 15 s, CH14 start)
            StoryScripts.Define("C14", b => b.Blocking().Music(MusicId.Finale)
                .ShotTeam(7f, 55f, 0f).CameraDrift(0.2f)
                .Line("PERCY", "The People, and three interns, versus Maximilian Me.", true)
                .Line("PERCY", "The charge: ruling the world... alone.", true)
                .Line("MAX", Expression.Smug, "Is that a crime?")
                .Line("PERCY", "It's the ONLY crime.", true)
                .Sfx(SfxId.Gavel)
                .Beat("c14.tally")
                .Toast("AYE 0 · NAY 1 (ME)"));
            StoryScripts.Alias("ch14.start", "C14");

            StoryScripts.Define("ch14.exhibitA", b => b.Sfx(SfxId.CrowdGasp).Sfx(SfxId.Gavel)
                .Line("MAX", Expression.Angry, "OBJECTION!")
                .Line("PERCY", "On what grounds?")
                .Line("MAX", Expression.Pout, "I don't LIKE it.")
                .Line("PERCY", "Overruled."));
            StoryScripts.Define("ch14.exhibitB", b => b.Sfx(SfxId.CrowdGasp).Line("MAX", Expression.Shocked, "Those are... fan fiction."));
            StoryScripts.Define("ch14.exhibitC", b => b.Sfx(SfxId.CrowdGasp).Line("MAX", Expression.Pout, "...it was laundry day."));
            StoryScripts.Define("ch14.defense", b => b
                .Line("MAX", Expression.Smug, "The defense calls... ME.")
                .Line("MAX", Expression.Happy, "Thank you, me.")
                .Sfx(SfxId.Gurgle));
            StoryScripts.Define("ch14.filibuster", b => b.Priority().Line("MAX", Expression.Smug, "I will now speak for nine hours."));
            StoryScripts.Define("ch14.humble", b => b.Sfx(SfxId.Gavel).Line("PERCY", "Objection sustained."));
            StoryScripts.Define("ch14.muted", b => b.Toast("MUTED"));
            StoryScripts.Define("ch14.vote", b => b.Priority()
                .Line("MAX", Expression.Smug, "It's 1–0. I win.")
                .Line("PERCY", "You can't vote in your own impeachment.")
                .Line("MAX", Expression.Angry, "Says WHO?")
                .Line("PERCY", "Article 3, clause B. You wrote it.")
                .Line("MAX", Expression.Pout, "...I was YOUNG."));
            StoryScripts.Define("ch14.bribe", b => b
                .Line("MAX", Expression.Smug, "Psst, {NAME}. Step off and you're VICE PRESIDENT.")
                .Toast("VICE PRESIDENT (OF SANDWICHES)")
                .Line("{ACTIVE}", "Just wanted to see how it felt."));
            StoryScripts.Define("ch14.impeached", b => b.Priority().Sfx(SfxId.Gavel).Sfx(SfxId.CrowdCheer)
                .Line("TALLY", "AYE 3 · NAY 1. MOTION CARRIES.")
                .Line("TALLY", "MAXIMILIAN ME IS HEREBY... IMPEACHED."));

            StoryScripts.Define("ch14.caught", b => b.Priority().Sfx(SfxId.CrowdCheer)
                .Line("CROWD", "AWWW.")
                .Line("PA", "TRUST FALL: SUCCESSFUL. 1999 IS NOW CLOSED."));
            StoryScripts.Define("ch14.landed", b => b.Line("MAX", Expression.Dizzy, "...POPULAR. At last."));

            // ---------------- C15 SUNRISE (blocking, interactive, <= 40 s)
            StoryScripts.Define("C15", b => b.Blocking().Watchdog(70f)
                .ShotTeam(6f, 50f, 0f).CameraDrift(0.15f)
                .Line("MAX", Expression.Sad, "...you caught me.", true)
                .Line("{CATCHER}", Expression.Happy, "That's what teams do.", true)
                .Line("PERCY", "Nobody's getting coffee this time.")
                .Beat("c15.sign")
                .Banner("SHIFT CHANGE: NOW", "", BannerStyle.Story)
                .Music(MusicId.Finale)
                .Sfx(SfxId.ColorBurst)
                .WorldWave(null, 6f, 0.85f, 1.15f)
                .Line("PERCY", "By Article 3, the world needs a new leader.")
                .Line("{BRAINIAC}", "Three leaders.")
                .Line("{SPRINTER}", Expression.Happy, "Taking turns!")
                .Line("{BRUTE}", Expression.Happy, "Every day!")
                .Line("PERCY", "A presidency that... shifts.", true)
                .Prompt("WHO RULES TODAY? [RIGHT SHIFT]", ShiftocracyEnding.PromptId)
                .Beat("c15.statue")
                .Sfx(SfxId.CrowdCheer)
                .Line("MAX", Expression.Pout, "...can I have a job?")
                .Line("PERCY", "Intern number four. Orientation's at nine.", true)
                .Beat("c15.balcony")
                .Banner(ClosingCardTitle, ClosingCardSub, BannerStyle.Story)
                .Wait(2f));
            StoryScripts.Alias("ch14.bench", "C15");

            // ---------------- C16 POST-CREDITS (blocking, 15 s)
            StoryScripts.Define("C16", b => b.Blocking().Music(MusicId.Training)
                .ShotTeam(6f, 50f, 0f)
                .Toast("WELCOME TO SHIFTERS. You have three coworkers. Try not to lose them.")
                .Wait(1.5f)
                .Line("MAX", Expression.Surprised, "...three?", true)
                .Gesture("{BRUTE}", VisualAction.Wave).Gesture("{SPRINTER}", VisualAction.Wave).Gesture("{BRAINIAC}", VisualAction.Wave)
                .Sfx(SfxId.Klaxon)
                .Line("KLAXON", "WAVE DETECTED.", true)
                .Wait(0.8f)
                .Line("KLAXON", "...WAVES ARE LEGAL NOW.", true)
                .Line("MAX", Expression.Happy, "...hi.", true)
                .Banner("SHIFT COMPLETE", "", BannerStyle.Success)
                .Wait(2f));

            StoryScripts.Define("react.CH13", b => b.Priority().Line("MAX", Expression.Pout, "Caught. By a TEAM. Put it in the incident report."));
            StoryScripts.Define("react.CH14", b => b.Priority().Line("MAX", Expression.Happy, "...thank you for watching ME. Really."));
        }
    }
}
