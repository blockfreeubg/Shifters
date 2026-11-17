// =====================================================================================
//  SHIFTERS - StoryText.Act3.cs   [story]
//  ACT III (GRAVEYARD SHIFT, the scary act): C08 The Night Shift, ch09..ch11 beats, C09 The Trust Fall,
//  the Boogeyman intro, C10 Unmasked. Every scare resolves in a joke within 3 s (§24.1).
// =====================================================================================
namespace Shifters
{
    public static partial class StoryText
    {
        static void RegisterAct3()
        {
            // ---------------- C08 THE NIGHT SHIFT (blocking, 25 s, CH09 start)
            StoryScripts.Define("C08", b => b.Blocking().Music(MusicId.Spooky)
                .ShotTeam(6f, 50f, 0f).CameraDrift(0.2f)
                .Sfx(SfxId.Thunder)
                .Line("PERCY", "...he cut the power. To everything.")
                .Sfx(SfxId.TVStatic)
                .Broadcast("MAX", Expression.Smug, "My OLD headquarters. Abandoned since... the incident.")
                .Broadcast("MAX", Expression.Smug, "Some say it's haunted.").FullOnly()
                .Broadcast("MAX", Expression.Laugh, "Why don't you SPLIT UP and look around? Much faster.", true)
                .Line("{BRAINIAC}", "Every horror movie ever: don't split up.", true)
                .Line("{BRUTE}", "I don't watch horror movies. They watch me.")
                .Line("{SPRINTER}", "I'll just run really fast.")
                .Line("PERCY", "Stay together. Please."));
            StoryScripts.Alias("ch09.start", "C08");

            // ---------------- SHIFT 09 LIGHTS OUT
            StoryScripts.Define("ch09.garage", b => b.Once().Sfx(SfxId.Creak).Line("{SPRINTER}", Expression.Scared, "That's fine. That's totally fine."));
            StoryScripts.Define("ch09.dummy", b => b.Once()
                .Line("DUMMY", "MANDATORY FUN")
                .Line("PERCY", "They only move when nobody's looking. So... look."));
            StoryScripts.Define("ch09.copier", b => b.Once().Sfx(SfxId.PrinterWhir)
                .Line("PRINTER", "HELP. HELP. TEAM PHOTO (UNAUTHORIZED)."));
            StoryScripts.Define("ch09.phone", b => b.Once().Sfx(SfxId.Heartbeat).Wait(1.2f)
                .Line("PERCY", "Sorry. Coffee machines don't have lungs. I'm just steaming."));
            StoryScripts.Define("ch09.elevator", b => b.Once().Line("PA", "Your elevator will arrive shortly. Please enjoy the music."));
            StoryScripts.Define("ch09.reveal", b => b.Once().Priority().Sfx(SfxId.CrowdCheer)
                .Toast("WELCOME TO THE TEAM!")
                .Line("{BRUTE}", Expression.Happy, "...aww."));

            // ---------------- SHIFT 10 THE ME-MORIAL PARK
            StoryScripts.Define("ch10.start", b => b.Music(MusicId.Spooky)
                .Line("PERCY", "The ME-morial Park. Where teams go when they're let go.")
                .Line("PERCY", "Stay warm. Stay together."));
            StoryScripts.Define("ch10.gate", b => b.Once().Line("GATE", "HUDDLE DETECTED. FINALLY, SOMEONE GETS IT."));
            StoryScripts.Define("ch10.ghosts", b => b.Line("GHOST TRIO", "WE ONLY HELP TEAMS."));
            StoryScripts.Define("ch10.ghosts.alone", b => b.Line("GHOST TRIO", "...oh. You're a solo act. Never mind."));
            StoryScripts.Define("ch10.ownGrave", b => b.Once().Line("{CLONE}", "...huh. I look good for my age."));

            // ---------------- C09 THE TRUST FALL (blocking, 25 s)
            StoryScripts.Define("C09", b => b.Blocking()
                .Shot("story.mausoleum", "story.statue", 45f, 0f).ShotTeam(5f, 45f, 0f).FullOnly()
                .Sfx(SfxId.Creak).Wait(0.8f)
                .Toast("TEAM-BUILDING RETREAT, 1999. THE TRUST FALL. NOBODY CAUGHT HIM.")
                .Line("{BRAINIAC}", Expression.Surprised, "Intern number zero... Maximilian Me.", true)
                .Line("PERCY", "They were getting coffee. From me. I'd just brewed a fresh pot.", true)
                .Line("PERCY", "Everyone turned around. He hit the floor. He's been alone ever since.")
                .Face("{BRUTE}", Expression.Sad).Wait(0.6f).FullOnly()
                .Scare("c09.ghost")
                .Sfx(SfxId.Boo)
                .Line("GHOST", "BOO.")
                .Face("{BRUTE}", Expression.Scared).Face("{SPRINTER}", Expression.Scared).Face("{BRAINIAC}", Expression.Scared)
                .Sfx(SfxId.SlideWhistleDown)
                .Line("GHOST", "Sorry. Have you seen my team?", true));
            StoryScripts.Alias("ch10.mausoleum", "C09");

            // ---------------- SHIFT 11 UNMASKED - THE BOOGEYMAN
            StoryScripts.Define("ch11.intro", b => b.Priority().Music(MusicId.Boss)
                .Sfx(SfxId.Whisper)
                .Line("BOOGEYMAN", "...split... up...")
                .Banner("THE BOOGEYMAN", "DEPT. OF FEAR · IT ONLY HUNTS THE LONELY", BannerStyle.Boss));
            StoryScripts.Define("ch11.p1", b => b.Priority()
                .Line("BOOGEYMAN", "...split... up...")
                .Line("MAX", "Stay in the light? Cowards. Smart cowards."));
            StoryScripts.Define("ch11.spook", b => b.Sfx(SfxId.SlideWhistleDown)
                .Line("PA", "Lonely intern delivered to the BROOM CLOSET.")
                .Line("PA", "Please collect your coworker."));
            StoryScripts.Define("ch11.p2", b => b.Priority()
                .Line("{BRAINIAC}", Expression.Surprised, "Is that... a floor buffer in a bedsheet?")
                .Line("SCARE-O-MATIC", "...boo?"));

            // CH11 beats requested by the lead (fixes/story.md). Non-blocking barks.
            StoryScripts.Define("ch11.tell", b => b.Priority().Line("PERCY", "Eyes in the dark? Shine your light. Or hold a hand."));
            StoryScripts.Define("ch11.fizzle", b => b.Sfx(SfxId.Squeak).Line("BOOGEYMAN", "...never mind..."));
            StoryScripts.Define("ch11.rescue", b => b.Line("{RESCUED}", Expression.Happy, "I wasn't scared. I was organizing the brooms."));
            StoryScripts.Define("ch11.lightW", b => b.Sfx(SfxId.Klaxon).Line("PA", "GENERATOR ONLINE. PLEASE STOP SCREAMING IN THE WEST WING."));
            StoryScripts.Define("ch11.lightN", b => b.Sfx(SfxId.Klaxon).Line("PA", "BREAKER RESET. WHOEVER DID THAT: SLOW DOWN."));
            StoryScripts.Define("ch11.lightE", b => b.Sfx(SfxId.Klaxon).Line("PA", "FUSE REPLACED. NOTE: ADULT SUPERVISION REQUIRED."));
            StoryScripts.Define("ch11.boo", b => b
                .Line("{VICTIM}", Expression.Scared, "AAAH!")
                .Sfx(SfxId.SlideWhistleDown)
                .Line("{VICTIM}", Expression.Surprised, "...that was mostly the whistle."));
            StoryScripts.Define("ch11.unmasked", b => b.Priority().Line("SCARE-O-MATIC", "MY SHEET! Now everyone knows I'm a floor buffer!"));

            // ---------------- C10 UNMASKED (blocking, 25 s)
            StoryScripts.Define("C10", b => b.Blocking()
                .ShotTeam(6f, 50f, 0f)
                .Sfx(SfxId.TVStatic)
                .Broadcast("MAX", Expression.Angry, "You were supposed to SPLIT UP! That's how horror WORKS!", true)
                .Line("{BRAINIAC}", "Not if you stick together.")
                .Broadcast("MAX", Expression.Angry, "WHO taught you that?!")
                .Line("PERCY", "...me.")
                .Broadcast("MAX", Expression.Shocked, "PERCY?! My own COFFEE MACHINE?!", true)
                .Toast("EXHIBIT C: THE BEDSHEET")
                .Beat("c10.kidnap")
                .Line("PERCY", "Unhand me! I'm ninety degrees!").FullOnly()
                .Broadcast("MAX", Expression.Smug, "You took my color, my books and my bedsheet. I'm taking my COFFEE.", true)
                .Toast("(T_T) DON'T COME ALONE")
                .Do(() => ActCardNow(3))
                .Wait(1.5f));
            StoryScripts.Alias("ch11.outro", "C10");

            StoryScripts.Define("react.CH09", b => b.Priority().Line("MAX", Expression.Pout, "Forty dummies and NOBODY split up? Unbelievable."));
            StoryScripts.Define("react.CH10", b => b.Priority().Line("MAX", Expression.Pout, "That was a PRIVATE cemetery. For my PRIVATE feelings."));
        }
    }
}
