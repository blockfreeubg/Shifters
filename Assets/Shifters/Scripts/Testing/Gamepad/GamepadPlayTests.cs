// =====================================================================================
//  SHIFTERS - GamepadPlayTests.cs   (gp_play, DevPlay)
//  Gameplay prompts outside UI / LevelDesigns follow the device:
//  P1 HintSystem generic hints ({Scan}, {AIMode}): keyboard text identical to before, pad shows R3 / D-LEFT, audit-clean
//  P2 ExitConsole first-press toast ({Interact}): "Press E again ..." / "Press X again ..."
//  P3 Breakable cracked-wall chip ({Interact}): "(E)" / "(X)", re-evaluated live on a real device switch
//  P4 InteractionSensor prompts on a real IInteractable (cap follows the device)
//  Shots: gpplay_kb_hint_toast_wall / gpplay_pad_hint_toast_wall
//  Run: devplay.sh <clone> gp_play -devSetup Shifters.GamepadPlayTests.RunAll
//         -devScene Assets/Shifters/Scenes/L1_TrainingFacility.unity -devSize 1920x1080 -devSeconds 1
// =====================================================================================
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace Shifters
{
    public static class GamepadPlayTests
    {
        static DevPlayContext _ctx;

        static readonly string[] OldGeneric =
        {
            "Nothing to solve right here. Remember: every coworker sees the world differently - SHIFT to another one and press R to scan.",
            "The Brute lifts and pushes, the Sprinter outruns timers, the Brainiac sees hidden paths and hacks terminals.",
            "Teammates on STAY make excellent pressure-plate weights. Press G to toggle Follow/Stay.",
            "Stuck on a high ledge? Stack: stand on a teammate's head, or let the Brute throw someone up.",
        };

        public static IEnumerator RunAll(DevPlayContext ctx)
        {
            _ctx = ctx;
            StoryPlayer.StoryModeOverride = 2;
            ShiftersInput.SetUsingGamepad(false);
            Strings();
            yield return Scene();
            GamepadTestKit.Cleanup();
            StoryPlayer.StoryModeOverride = -1;
        }

        static void Strings()
        {
            // P1
            var f = typeof(HintSystem).GetField("GenericReal", BindingFlags.NonPublic | BindingFlags.Static);
            var real = f != null ? (string[])f.GetValue(null) : null;
            if (_ctx.Check(real != null && real.Length == OldGeneric.Length, "P1 HintSystem.GenericReal found (" + (real != null ? real.Length : -1) + ")"))
            {
                for (int i = 0; i < real.Length; i++)
                {
                    string kb = InputPrompts.Format(real[i], false, true), pad = InputPrompts.Format(real[i], true, false);
                    _ctx.Check(kb == OldGeneric[i], "P1 hint " + i + " keyboard identical: '" + kb + "'");
                    _ctx.Check(InputPrompts.IsClean(real[i]) && InputPrompts.IsClean(pad), "P1 hint " + i + " audit-clean (template + pad): '" + pad + "'");
                }
                _ctx.Check(InputPrompts.Format(real[0], true, false).EndsWith("press R3 to scan."), "P1 pad scan hint: " + InputPrompts.Format(real[0], true, false));
                _ctx.Check(InputPrompts.Format(real[2], true, false).EndsWith("Press D-LEFT to toggle Follow/Stay."), "P1 pad team hint: " + InputPrompts.Format(real[2], true, false));
            }
            // P2
            string t = ExitConsole.ArmToast + "Gary behind";
            _ctx.Check(InputPrompts.Format(t, false, true) == "Press E again to leave Gary behind", "P2 kb toast: " + InputPrompts.Format(t, false, true));
            _ctx.Check(InputPrompts.Format(t, true, false) == "Press X again to leave Gary behind", "P2 pad toast: " + InputPrompts.Format(t, true, false));
            _ctx.Check(InputPrompts.IsClean(t), "P2 toast template audit-clean");
        }

        static IEnumerator Scene()
        {
            float t0 = Time.realtimeSinceStartup;
            while ((CharacterManager.Instance == null || CharacterManager.Instance.Team == null || CharacterManager.Instance.Team.Count < 3
                    || UIManager.Instance == null || !UIManager.Instance.HudVisible) && Time.realtimeSinceStartup - t0 < 40f)
                yield return null;
            yield return _ctx.Wait(1.5f);
            ScreenFader.Set(0f);
            GameEvents.RaiseTutorialHide();
            var chud = CampaignHud.Instance;
            if (chud != null && chud.Card.Showing) chud.Card.Close(true);
            var cm = CharacterManager.Instance;
            if (!_ctx.Check(cm != null, "P3 team up")) yield break;
            int brute = -1;
            for (int i = 0; i < cm.Team.Count; i++) if (cm.Team[i] != null && cm.Team[i].Archetype == Archetype.Brute) brute = i;
            if (brute >= 0 && cm.ActiveIndex != brute) cm.SwitchTo(brute, true);
            yield return _ctx.Wait(1.2f);
            foreach (var c in cm.Team) if (c != null) c.ExternalControl = true;
            var me = cm.Active;
            if (!_ctx.Check(me != null && me.Archetype == Archetype.Brute, "P3 Brute active (" + (me != null ? me.DisplayName : "null") + ")")) yield break;

            // P3: cracked wall 4.5 m in front of the Brute, facing it
            var cam = Camera.main;
            Vector3 fwd = cam != null ? Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized : me.transform.forward;
            if (fwd.sqrMagnitude < 0.1f) fwd = me.transform.forward;
            var holder = new GameObject("GpPlayTestWall").transform;
            Vector3 wallPos = me.transform.position + fwd * 4.5f;
            var wall = PuzzleKit.CrackedWall(holder, wallPos, Quaternion.LookRotation(-fwd));
            yield return _ctx.Wait(0.8f);
            bool ok = wall != null && wall.Reveal != null;
            if (_ctx.Check(ok, "P3 cracked wall built with a Revealable"))
            {
                ShiftersInput.SetUsingGamepad(false);
                string kb = wall.Reveal.GetLabel(me);
                _ctx.Check(kb == "CRACKED WALL - PUNCH IT (E)", "P3 kb wall chip identical: '" + kb + "'");
            }

            // P4: the wall is an IInteractable: walk the Brute next to it -> sensor prompt with {Interact}
            me.transform.position = wallPos - fwd * 1.3f;
            me.transform.rotation = Quaternion.LookRotation(fwd);
            var rb = me.GetComponent<Rigidbody>();
            if (rb != null) { rb.position = me.transform.position; rb.linearVelocity = Vector3.zero; }
            var prompt = Object.FindAnyObjectByType<HudPrompt>(FindObjectsInactive.Include);

            ShiftersInput.SetUsingGamepad(false);
            GameEvents.RaiseHint(InputPrompts.FormatRich(OldGenericTemplate(0)));
            GameEvents.RaiseToast(InputPrompts.FormatRich(ExitConsole.ArmToast + "Gary behind"), UIColors.Danger);
            yield return _ctx.Wait(1.4f);
            if (prompt != null) _ctx.Log("P4 kb prompt [" + prompt.ShownKey + "] " + prompt.ShownText);
            if (prompt != null && prompt.ShownText == "Punch wall") _ctx.Check(prompt.ShownKey == "E", "P4 kb wall prompt cap E: " + prompt.ShownKey);
            _ctx.Shot("gpplay_kb_hint_toast_wall");

            yield return GamepadTestKit.UseGamepad();
            yield return _ctx.Wait(0.6f);
            _ctx.Check(ShiftersInput.UsingGamepad, "P3 pad: UsingGamepad after a real pad flick");
            if (ok)
            {
                string pad = wall.Reveal.GetLabel(me);
                _ctx.Check(pad == "CRACKED WALL - PUNCH IT (X)", "P3 pad wall chip live: '" + pad + "'");
            }
            if (prompt != null && prompt.ShownText == "Punch wall") _ctx.Check(prompt.ShownKey == "X", "P4 pad wall prompt cap X: " + prompt.ShownKey);
            GameEvents.RaiseHint(InputPrompts.FormatRich(OldGenericTemplate(0)));
            GameEvents.RaiseToast(InputPrompts.FormatRich(ExitConsole.ArmToast + "Gary behind"), UIColors.Danger);
            var hint = HintSystem.Instance;
            if (hint != null)
            {
                string h = hint.RequestHint();
                _ctx.Check(h == null || InputPrompts.IsClean(h), "P1 pad RequestHint output audit-clean: '" + h + "'");
                GameEvents.RaiseHint(InputPrompts.FormatRich(OldGenericTemplate(0)));
            }
            yield return _ctx.Wait(1.4f);
            if (prompt != null) _ctx.Log("P4 pad prompt [" + prompt.ShownKey + "] " + prompt.ShownText);
            _ctx.Shot("gpplay_pad_hint_toast_wall");

            yield return GamepadTestKit.UseKeyboard();
            yield return _ctx.Wait(0.5f);
            if (ok) _ctx.Check(wall.Reveal.GetLabel(me) == "CRACKED WALL - PUNCH IT (E)", "P3 back to keyboard: '" + wall.Reveal.GetLabel(me) + "'");
            Object.Destroy(holder.gameObject);
        }

        static string OldGenericTemplate(int i)
        {
            var f = typeof(HintSystem).GetField("GenericReal", BindingFlags.NonPublic | BindingFlags.Static);
            var real = f != null ? (string[])f.GetValue(null) : null;
            return real != null && i < real.Length ? real[i] : "";
        }
    }
}
