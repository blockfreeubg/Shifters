// =====================================================================================
//  SHIFTERS - StoryText.Act2.cs   [story]
//  ACT II (SWING SHIFT, the bank heist): C05, ch04..ch08 beats, C06 The Color Explosion,
//  the Porkchop intro, C07 The Getaway. Percy is the radio PiP.
// =====================================================================================
namespace Shifters
{
    public static partial class StoryText
    {
        static void RegisterAct2()
        {
            // ---------------- C05 CH04 start (non-blocking radio)
            StoryScripts.Define("C05", b => b.Music(MusicId.Heist)
                .Line("PERCY", "Walk in like strangers. Three customers. No eye contact. No high-fives.")
                .Line("{SPRINTER}", "How do strangers walk?", true)
                .Line("PERCY", "Badly. Just don't walk together.", true));

            // ---------------- SHIFT 04 ACT NATURAL beats
            StoryScripts.Define("ch04.detected", b => b.Priority().Sfx(SfxId.CameraSpotted)
                .Line("PA", "Suspicious teamwork detected. Valued customers, please exit...")
                .Line("PA", "...and re-enter ONE AT A TIME."));
            StoryScripts.Define("ch04.alone", b => b
                .Line("TELLER-BOT", "Welcome, valued customer! ...Are you three TOGETHER?")
                .Line("{BRUTE}", "No.")
                .Line("TELLER-BOT", "Carry on."));
            StoryScripts.Define("ch04.keycard", b => b.Sfx(SfxId.PrinterWhir).Line("PRINTER", "KEYCARD PRINTED. VALID FOR ONE (1) EMPLOYEE."));
            StoryScripts.Define("ch04.onedoor", b => b.Line("STAFF DOOR", "ONE. AT. A. TIME."));

            // ---------------- SHIFT 05 THE BACK WAY IN (L3 overlay beats)
            StoryScripts.Define("ch05.start", b => b.Music(MusicId.Heist)
                .Line("PERCY", "The old color lab. Under the bank. Mind the chair.")
                .Line("PERCY", "It says INSERT INTERN. Please don't insert yourself alone."));
            StoryScripts.Define("ch05.chair", b => b.Once().Line("MAX", Expression.Smug, "That chair drained the world's color. It'll LOVE you."));
            StoryScripts.Define("ch05.exhibit", b => b.Once().Sfx(SfxId.ColorBurst)
                .Toast("EXHIBIT A: THE GREY MEMO")
                .Line("PERCY", "'Drain the color. Nobody will notice.' He signed it. Twice."));

            // ---------------- SHIFT 06 THE BANKER'S EXAM (L4)
            StoryScripts.Define("ch06.start", b => b
                .Line("PERCY", "The vault's security exam. Designed for ONE genius.")
                .Line("PERCY", "Luckily, you're three idiots. That's better."));

            // ---------------- SHIFT 07 THE FEDERAL COLOR RESERVE
            StoryScripts.Define("ch07.start", b => b.Music(MusicId.Heist)
                .Line("PERCY", "The Federal Color Reserve. Every color in the world. In one basement.")
                .Line("{BRAINIAC}", "Statistically we're doomed. Emotionally I'm thrilled."));
            StoryScripts.Define("ch07.curtain", b => b.Line("PA", "LASER HALL. PLEASE DO NOT DANCE."));
            StoryScripts.Define("ch07.lockA", b => b.Sfx(SfxId.VaultSpin).Line("VAULT", "TUMBLERS: BALANCED."));
            StoryScripts.Define("ch07.lockB", b => b.Sfx(SfxId.VaultSpin).Line("VAULT", "KEYS: TURNED. BY THREE PEOPLE?!"));
            StoryScripts.Define("ch07.lockC", b => b.Sfx(SfxId.VaultSpin).Line("VAULT", "CODE: CORRECT. HOW DID YOU KNOW MY BEST FEATURES?"));
            StoryScripts.Define("ch07.door", b => b.Sfx(SfxId.VaultOpen).Line("MAX", Expression.Shocked, "That vault door needs THREE people? Who designed— oh. Me."));
            StoryScripts.Define("ch07.scare", b => b.Once().Sfx(SfxId.ScareStinger)
                .Line("PA", "Who's in my vault? NOBODY touches my stuff.")
                .Wait(1.2f)
                .Toast("SMILE, YOU'RE ON CAMERA"));
            StoryScripts.Define("ch07.alarm", b => b.Priority().Sfx(SfxId.Klaxon).Line("PA", "ALARM. PLEASE REMAIN CALM AND GUILTY."));

            // ---------------- C06 THE COLOR EXPLOSION (blocking, 12 s)
            StoryScripts.Define("C06", b => b.Blocking()
                .ShotTeam(6f, 50f, 0f).CameraDrift(-0.4f)
                .Sfx(SfxId.Stamp).Wait(0.8f)
                .Sfx(SfxId.ColorBurst)
                .WorldWave("story.valve", 3f, 0.35f, 0.55f)
                .Wait(1.6f)
                .Line("MAX", Expression.Shocked, "MY COLOR! I hoarded that for TWENTY YEARS!", true)
                .Puppet("CrownSlip")
                .Sfx(SfxId.Klaxon)
                .Line("PA", "VAULT BREACH. RELEASING... PORKCHOP.", true)
                .Sfx(SfxId.Oink).Wait(0.8f));
            StoryScripts.Alias("ch07.valve", "C06");

            // ---------------- SHIFT 08 THE GETAWAY - PORKCHOP
            StoryScripts.Define("ch08.board", b => b.Line("PERCY", "The tram only moves with everyone on it. Like a family car.")
                .Line("PERCY", "With a pig behind it."));
            StoryScripts.Define("ch08.busted", b => b.Priority().Sfx(SfxId.Oink).Line("PORKCHOP", "YOU OWE ME INTEREST!"));
            StoryScripts.Define("ch08.st1", b => b.Line("PA", "ROADBLOCK: VAULT BRICKS."));
            StoryScripts.Define("ch08.st2", b => b.Line("TOLL", "OVERRIDE TOLL? YOU HAVE NO COINS. YOU HAVE ALL THE COINS."));
            StoryScripts.Define("ch08.st3", b => b.Line("PA", "DRAWBRIDGE RAISED FOR YOUR INCONVENIENCE."));
            StoryScripts.Define("ch08.boss", b => b.Priority().Music(MusicId.Boss)
                .Line("MAX", Expression.Angry, "PORKCHOP! FETCH!")
                .Sfx(SfxId.Oink)
                .Line("PORKCHOP", "OINK. INSUFFICIENT FUNDS.")
                .Banner("PORKCHOP", "DEPT. OF FINANCE · WHERE MONEY GOES TO GET BIGGER", BannerStyle.Boss));
            StoryScripts.Define("ch08.p1", b => b.Priority().Sfx(SfxId.BossHit).Line("PORKCHOP", "OINK?!"));
            StoryScripts.Define("ch08.p2", b => b.Priority().Sfx(SfxId.BossHit).Line("PORKCHOP", "SQUEEEE!"));
            StoryScripts.Define("ch08.p3", b => b.Priority().Sfx(SfxId.BossDefeat).Line("PORKCHOP", "SAVINGS ACCOUNT... TERMINATED."));

            // ---------------- C07 THE GETAWAY (blocking, 20 s)
            StoryScripts.Define("C07", b => b.Blocking()
                .ShotTeam(7f, 50f, 0f)
                .Line("PORKCHOP", "...insufficient... funds...")
                .Sfx(SfxId.CoinShower)
                .Toast("EXHIBIT B: THE BOOKS")
                .Beat("c07.van").Wait(0.6f).FullOnly()
                .Line("PERCY", "PERCY'S PERCOLATED DELIVERIES. Definitely not a getaway van.").FullOnly()
                .Line("PERCY", "Get in!")
                .Line("{BRUTE}", "I'll carry the plan.").FullOnly()
                .Sfx(SfxId.Thunder)
                .Line("MAX", Expression.Angry, "You want to be a team? Let's see your team handle the NIGHT SHIFT.", true)
                .Do(() => ActCardNow(2))
                .Wait(1.5f));
            StoryScripts.Alias("ch08.outro", "C07");

            StoryScripts.Define("react.CH04", b => b.Priority().Line("MAX", Expression.Pout, "Three strangers. Same door. Same mustaches. Suspicious."));
            StoryScripts.Define("react.CH07", b => b.Priority().Line("MAX", Expression.Angry, "Give that color BACK! It was in ALPHABETICAL order!"));
        }
    }
}
