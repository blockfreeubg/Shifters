// =====================================================================================
//  SHIFTERS - GamepadUITests.cs   (gp_ui, DevPlay)
//  U1 HUD team panel: TEAM chip cap G -> D-LEFT, character-card slot caps 1/2/3 (hidden on pad: no direct select)
//  U2 switcher wheel: footer + slot caps per device, rebuilt live while the wheel is open; hub key per
//     device (kb digit, pad = the slot's switcher binding = its card cap, one line in the disc), live flips
//  U3 campaign chapter card skip hint, ruler prompt cap, C15 story prompt ([RIGHT SHIFT] -> [D-DOWN])
//  U4 StoryText accepts input-prompt tokens; scene audit of the gp_ui elements (pad mode) = 0 raw keys
//  Keyboard checks assert the exact pre-gamepad wording. Shots gpui_kb_* / gpui_pad_*.
//  Run: devplay.sh <clone> gp_ui -devSetup Shifters.GamepadUITests.RunAll
//         -devScene Assets/Shifters/Scenes/L1_TrainingFacility.unity -devSize 1920x1080 -devSeconds 1
// =====================================================================================
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Shifters
{
    public static class GamepadUITests
    {
        static DevPlayContext _ctx;
        public const string RulerStoryText = "WHO RULES TODAY? [{Switcher|RIGHT SHIFT}]";

        public static IEnumerator RunAll(DevPlayContext ctx)
        {
            _ctx = ctx;
            StoryPlayer.StoryModeOverride = 2;
            ShiftersInput.SetUsingGamepad(false);
            float t0 = Time.realtimeSinceStartup;
            while ((CharacterManager.Instance == null || CharacterManager.Instance.Team == null || CharacterManager.Instance.Team.Count < 3
                    || UIManager.Instance == null || !UIManager.Instance.HudVisible) && Time.realtimeSinceStartup - t0 < 40f)
                yield return null;
            yield return _ctx.Wait(1.5f);
            ScreenFader.Set(0f);
            GameEvents.RaiseTutorialHide();
            var chud = CampaignHud.Instance;
            if (chud != null && chud.Card.Showing) chud.Card.Close(true);
            if (CharacterManager.Instance != null) foreach (var c in CharacterManager.Instance.Team) if (c != null) c.ExternalControl = true;
            yield return _ctx.Wait(0.5f);

            yield return TeamPanel();
            yield return Switcher();
            if (_ctx.Check(chud != null, "U3 campaign HUD present")) yield return Campaign(chud);
            yield return HintToast();
            StoryTokens();
            GamepadTestKit.Cleanup();
            StoryPlayer.StoryModeOverride = -1;
        }

        // ------------------------------------------------------------------ U1
        static IEnumerator TeamPanel()
        {
            var panel = Object.FindAnyObjectByType<HudTeamPanel>(FindObjectsInactive.Include);
            if (!_ctx.Check(panel != null, "U1 HudTeamPanel present")) yield break;
            var ai = panel.transform.Find("AIMode");
            var aiCap = ai != null ? ai.GetComponentInChildren<DeviceKeyCap>(true) : null;
            var cards = panel.GetComponentsInChildren<HudCharacterCard>(true);
            if (!_ctx.Check(aiCap != null && cards.Length == 3, "U1 team chip cap + 3 cards (" + (aiCap != null) + ", " + cards.Length + ")")) yield break;

            ShiftersInput.SetUsingGamepad(false);
            yield return _ctx.Wait(0.3f);
            _ctx.Check(aiCap.Text == "G" && aiCap.HasLabel, "U1 kb: team chip cap '" + aiCap.Text + "'");
            _ctx.Check(SlotCaps(cards) == "1,2,3", "U1 kb: card slot caps " + SlotCaps(cards));
            _ctx.Shot("gpui_kb_team_panel");

            yield return GamepadTestKit.UseGamepad();
            yield return _ctx.Wait(0.4f);
            _ctx.Check(ShiftersInput.UsingGamepad, "U1 pad: UsingGamepad after a real pad flick");
            _ctx.Check(aiCap.Text == "D-LEFT" && aiCap.ShowingGamepad, "U1 pad: team chip cap '" + aiCap.Text + "'");
            _ctx.Check(SlotCaps(cards) == "-,-,-", "U1 pad: slot caps hidden (no direct select on a pad in gameplay) " + SlotCaps(cards));
            var aiRt = (RectTransform)aiCap.transform; var chip = (RectTransform)ai;
            var text = ai.Find("Text") as RectTransform;
            float capLeft = chip.rect.width + aiRt.anchoredPosition.x - aiRt.rect.width;
            float textRight = text != null ? text.anchoredPosition.x + text.GetComponent<TMP_Text>().GetPreferredValues("TEAM: FOLLOW").x : 0f;   // the longer mode text
            _ctx.Check(capLeft >= textRight - 2f, "U1 pad: D-LEFT cap does not overlap the TEAM text (cap x " + capLeft.ToString("0") + " >= text end " + textRight.ToString("0") + ")");
            _ctx.Shot("gpui_pad_team_panel");
            yield return GamepadTestKit.UseKeyboard();
            yield return _ctx.Wait(0.3f);
            _ctx.Check(aiCap.Text == "G" && SlotCaps(cards) == "1,2,3", "U1 kb again: chip '" + aiCap.Text + "' slots " + SlotCaps(cards));
        }

        static string SlotCaps(HudCharacterCard[] cards)
        {
            var l = new List<string>();
            foreach (var c in cards)
            {
                var cap = c.GetComponentInChildren<DeviceKeyCap>(true);
                l.Add(cap == null ? "?" : cap.HasLabel ? cap.Text : "-");
            }
            l.Sort();
            return string.Join(",", l);
        }

        // ------------------------------------------------------------------ U2
        static IEnumerator Switcher()
        {
            var sw = CharacterSwitcher.Instance;
            if (!_ctx.Check(sw != null, "U2 CharacterSwitcher present")) yield break;
            ShiftersInput.SetUsingGamepad(false);
            sw.Open();
            yield return _ctx.Wait(0.6f);
            var wheel = SwitcherWheelUI.Instance;
            if (!_ctx.Check(wheel != null && wheel.IsOpen, "U2 wheel open")) yield break;
            _ctx.Check(Footer(wheel) == "1 2 3 OR MOUSE  SELECT ENTER / LMB SWITCH ESC CANCEL" && !wheel.FooterShowsGamepad, "U2 kb: footer '" + Footer(wheel) + "'");
            _ctx.Check(WheelCaps(wheel) == "1,2,3", "U2 kb: switcher card caps " + WheelCaps(wheel));
            _ctx.Shot("gpui_kb_switcher");
            yield return HubSlots(sw, wheel, false);

            yield return GamepadTestKit.UseGamepad();   // live, while the wheel is open
            yield return _ctx.Wait(0.4f);
            _ctx.Check(wheel.FooterShowsGamepad && Footer(wheel) == "D-LEFT D-UP D-RIGHT OR STICK  SELECT A SWITCH B CANCEL", "U2 pad: footer '" + Footer(wheel) + "'");
            _ctx.Check(WheelCaps(wheel) == "D-LEFT,D-UP,D-RIGHT", "U2 pad: switcher card caps " + WheelCaps(wheel));
            var footer = FooterRt(wheel);
            _ctx.Check(footer != null && footer.sizeDelta.x < 1400f, "U2 pad: footer width " + (footer != null ? footer.sizeDelta.x.ToString("0") : "?"));
            _ctx.Shot("gpui_pad_switcher");
            yield return HubSlots(sw, wheel, true);
            // live flips while open: kb -> digit, pad -> pad label again
            int hs = wheel.Highlight;
            yield return GamepadTestKit.UseKeyboard();
            yield return _ctx.Wait(0.3f);
            _ctx.Check(wheel.HubLabelText == (hs + 1).ToString(), "U2 live kb (open): hub '" + wheel.HubLabelText + "' == '" + (hs + 1) + "'");
            yield return GamepadTestKit.UseGamepad();
            yield return _ctx.Wait(0.3f);
            _ctx.Check(HubMatchesCap(wheel, hs, true, out string why), "U2 live pad (open): " + why);
            sw.Close(false);
            yield return _ctx.Wait(0.5f);

            // device flip while the wheel sleeps -> footer right on the next open
            yield return GamepadTestKit.UseKeyboard();
            yield return _ctx.Wait(0.2f);
            sw.Open();
            yield return _ctx.Wait(0.5f);
            _ctx.Check(!wheel.FooterShowsGamepad && Footer(wheel).StartsWith("1 2 3 OR MOUSE"), "U2 kb: footer back on reopen '" + Footer(wheel) + "'");
            _ctx.Check(WheelCaps(wheel) == "1,2,3", "U2 kb: card caps back " + WheelCaps(wheel));
            _ctx.Check(wheel.Highlight < 0 || wheel.HubLabelText == (wheel.Highlight + 1).ToString(), "U2 kb: hub digit after a flip asleep '" + wheel.HubLabelText + "'");
            sw.Close(false);
            yield return _ctx.Wait(0.4f);
            // flip to pad while asleep -> the sleeping hub already holds the pad label (scene audits read inactive UI)
            yield return GamepadTestKit.UseGamepad();
            yield return _ctx.Wait(0.3f);
            if (wheel.Highlight >= 0) _ctx.Check(HubMatchesCap(wheel, wheel.Highlight, false, out string why2), "U2 pad asleep: " + why2);
            yield return GamepadTestKit.UseKeyboard();
            yield return _ctx.Wait(0.2f);
            if (wheel.Highlight >= 0) _ctx.Check(wheel.HubLabelText == (wheel.Highlight + 1).ToString(), "U2 kb asleep: hub '" + wheel.HubLabelText + "'");
        }

        /// <summary>Highlight each available slot once; the hub shows that slot's key for the device. Shots gpui_{kb|pad}_hub{n}.</summary>
        static IEnumerator HubSlots(CharacterSwitcher sw, SwitcherWheelUI wheel, bool pad)
        {
            string dev = pad ? "pad" : "kb";
            var sizes = new List<float>();
            for (int i = 0; i < 3; i++)
            {
                var card = wheel.Card(i);
                if (card == null || !card.gameObject.activeSelf || !card.Available) continue;
                sw.Highlight(i);
                yield return _ctx.Wait(0.35f);
                if (!_ctx.Check(wheel.Highlight == i, "U2 " + dev + ": slot " + (i + 1) + " highlighted (" + wheel.Highlight + ")")) continue;
                if (pad) _ctx.Check(HubMatchesCap(wheel, i, true, out string why), "U2 pad hub slot " + (i + 1) + ": " + why);
                else _ctx.Check(wheel.HubLabelText == (i + 1).ToString() && Mathf.Approximately(wheel.HubLabelFontSize, 64f),
                    "U2 kb hub slot " + (i + 1) + ": '" + wheel.HubLabelText + "' size " + wheel.HubLabelFontSize.ToString("0"));
                sizes.Add(wheel.HubLabelFontSize);
                _ctx.Shot("gpui_" + dev + "_hub" + (i + 1));
            }
            bool same = sizes.Count > 0; foreach (var z in sizes) same &= Mathf.Approximately(z, sizes[0]);
            _ctx.Check(same && sizes.Count >= 2, "U2 " + dev + " hub: one font size for every slot (" + string.Join("/", sizes) + ")");
        }

        /// <summary>Pad hub = the slot's switcher binding (and, while open, its card cap text), never a bare digit,
        /// one line inside the disc. Asleep (open=false) the inactive card caps are not refreshed, so only the binding is compared.</summary>
        static bool HubMatchesCap(SwitcherWheelUI wheel, int slot, bool open, out string why)
        {
            var card = wheel.Card(slot);
            var cap = card != null ? card.GetComponentInChildren<DeviceKeyCap>(true) : null;
            string capText = cap != null ? cap.Text : "?";
            string hub = wheel.HubLabelText;
            var label = wheel.transform.Find("Hub/Label") as RectTransform;
            var tmp = label != null ? label.GetComponent<TMP_Text>() : null;
            float w = tmp != null ? UIFactory.Preferred(tmp, hub).x : 999f;
            bool digit = hub.Length == 1 && char.IsDigit(hub[0]);
            bool oneLine = tmp != null && tmp.textWrappingMode == TextWrappingModes.NoWrap && w <= 150f;
            why = "hub '" + hub + "' cap '" + capText + "' size " + wheel.HubLabelFontSize.ToString("0") + " w " + w.ToString("0");
            bool capOk = !open || (cap != null && cap.ShowingGamepad && hub == capText);
            return capOk && ShiftersInput.UsingGamepad && hub == KeyLabels.Gamepad("Select" + (slot + 1)).ToUpperInvariant() && !digit && oneLine;
        }

        static RectTransform FooterRt(SwitcherWheelUI w)
        {
            foreach (Transform t in w.transform) if (t.name == "Footer" && t.gameObject.activeSelf) return (RectTransform)t;
            return null;
        }

        static string Footer(SwitcherWheelUI w)
        {
            var f = FooterRt(w);
            if (f == null) return "";
            var parts = new List<string>();
            foreach (var t in f.GetComponentsInChildren<TMP_Text>(false)) if (!string.IsNullOrEmpty(t.text)) parts.Add(t.text);
            return string.Join(" ", parts);
        }

        static string WheelCaps(SwitcherWheelUI w)
        {
            var l = new List<string>();
            for (int i = 0; i < 3; i++)
            {
                var c = w.Card(i);
                if (c == null || !c.gameObject.activeSelf) continue;
                var cap = c.GetComponentInChildren<DeviceKeyCap>(true);
                l.Add(cap == null ? "?" : cap.Text);
            }
            return string.Join(",", l);
        }

        // ------------------------------------------------------------------ U3
        static IEnumerator Campaign(CampaignHud chud)
        {
            var prompt = Object.FindAnyObjectByType<HudPrompt>(FindObjectsInactive.Include);
            ShiftersInput.SetUsingGamepad(false);
            chud.ShowChapterCard("ACT I · DAY SHIFT", "SHIFT 01 · ORIENTATION", "Get to the exit.", "Welcome. Don't touch anything.");
            chud.ShowRuler(true);
            GameEvents.RaisePrompt(PromptInfo.Make("{Switcher|RIGHT SHIFT}", StoryText.Resolve(RulerStoryText)));
            yield return _ctx.Wait(0.6f);
            var rulerCap = chud.Ruler.GetComponentInChildren<DeviceKeyCap>(true);
            _ctx.Check(chud.Card.SkipHintText == "PRESS ANY KEY TO SKIP", "U3 kb: card skip hint '" + chud.Card.SkipHintText + "'");
            _ctx.Check(rulerCap != null && rulerCap.Text == "RIGHT SHIFT", "U3 kb: ruler cap '" + (rulerCap != null ? rulerCap.Text : "null") + "'");
            _ctx.Check(prompt != null && prompt.ShownKey == "RIGHT SHIFT" && prompt.ShownText == "WHO RULES TODAY? [RIGHT SHIFT]",
                "U3 kb: C15 prompt [" + (prompt != null ? prompt.ShownKey + "] " + prompt.ShownText : "null"));
            _ctx.Shot("gpui_kb_card_ruler");

            yield return GamepadTestKit.UseGamepad();
            yield return _ctx.Wait(0.4f);
            string hint = chud.Card.SkipHintText;
            _ctx.Check(chud.Card.Showing && hint.StartsWith("PRESS ") && hint.EndsWith(" TO SKIP") && hint.Contains(">A<"), "U3 pad: card skip hint '" + hint + "'");
            _ctx.Check(rulerCap != null && rulerCap.Text == "D-DOWN", "U3 pad: ruler cap '" + (rulerCap != null ? rulerCap.Text : "null") + "'");
            _ctx.Check(prompt != null && prompt.ShownKey == "D-DOWN" && prompt.ShownText.Contains("D-DOWN") && prompt.ShownText.StartsWith("WHO RULES TODAY? ["),
                "U3 pad: C15 prompt [" + (prompt != null ? prompt.ShownKey + "] " + prompt.ShownText : "null"));
            _ctx.Shot("gpui_pad_card_ruler");

            // audit while everything is up in pad mode
            var hits = GamepadTestKit.AuditScene();
            int mine = 0;
            foreach (var h in hits)
            {
                _ctx.Log("  audit (pad): " + h);
                if (h.StartsWith("[LEVEL]")) continue;   // level world text belongs to the level migration agents
                if (h.Contains("SwitcherWheel") || h.Contains("RulerPrompt") || h.Contains("ChapterCard") || h.Contains("SkipHint") || h.Contains("AIMode")
                    || h.Contains("RIGHT SHIFT") || h.Contains("ANY KEY")) mine++;
            }
            _ctx.Check(mine == 0, "U4 pad scene audit: " + mine + " gp_ui elements with raw key names (" + hits.Count + " hits in total, see log)");

            yield return GamepadTestKit.UseKeyboard();
            yield return _ctx.Wait(0.3f);
            _ctx.Check(chud.Card.SkipHintText == "PRESS ANY KEY TO SKIP" && rulerCap != null && rulerCap.Text == "RIGHT SHIFT" && prompt.ShownKey == "RIGHT SHIFT"
                && prompt.ShownText == "WHO RULES TODAY? [RIGHT SHIFT]", "U3 kb again: hint / ruler / prompt back to keyboard");
            GameEvents.RaisePrompt(null);
            chud.ShowRuler(false);
            if (chud.Card.Showing) chud.Card.Close(true);
        }

        // ------------------------------------------------------------------ U5
        static IEnumerator HintToast()
        {
            var hint = Object.FindAnyObjectByType<HudHint>(FindObjectsInactive.Include);
            var toasts = Object.FindAnyObjectByType<HudToasts>(FindObjectsInactive.Include);
            if (!_ctx.Check(hint != null && toasts != null, "U5 HudHint + HudToasts present")) yield break;
            var hintText = hint.transform.Find("Text").GetComponent<TMP_Text>();
            ShiftersInput.SetUsingGamepad(false);
            GameEvents.RaiseHint("Press {AIMode} to toggle Follow/Stay.");
            GameEvents.RaiseToast("Press {Interact} again to leave Brenda behind");
            yield return _ctx.Wait(0.6f);
            _ctx.Check(hintText.text == "Press G to toggle Follow/Stay.", "U5 kb: hint '" + hintText.text + "'");
            _ctx.Check(toasts.Newest == "Press E again to leave Brenda behind", "U5 kb: toast '" + toasts.Newest + "'");
            _ctx.Shot("gpui_kb_hint_toast");
            yield return GamepadTestKit.UseGamepad();
            yield return _ctx.Wait(0.4f);
            _ctx.Check(hintText.text == "Press <b>D-LEFT</b> to toggle Follow/Stay.", "U5 pad: hint re-rendered live '" + hintText.text + "'");
            _ctx.Check(toasts.Newest != null && toasts.Newest.StartsWith("Press <b><color=") && toasts.Newest.Contains(">X<"), "U5 pad: toast re-rendered live '" + toasts.Newest + "'");
            _ctx.Shot("gpui_pad_hint_toast");
            yield return GamepadTestKit.UseKeyboard();
            yield return _ctx.Wait(0.3f);
            _ctx.Check(hintText.text == "Press G to toggle Follow/Stay." && toasts.Newest == "Press E again to leave Brenda behind", "U5 kb again: hint / toast");
            GameEvents.RaiseToast("PUZZLE SOLVED");
            yield return null;
            _ctx.Check(toasts.Newest == "PUZZLE SOLVED", "U5 token-free toast unchanged");
            hint.Clear(); toasts.Clear();
        }

        // ------------------------------------------------------------------ U4
        static void StoryTokens()
        {
            _ctx.Check(StoryText.TokensResolve(RulerStoryText, out var bad), "U4 StoryText accepts {Switcher|RIGHT SHIFT} (bad: " + bad + ")");
            _ctx.Check(StoryText.TokensResolve("{JUMP} {kb:x}{pad:y} {BRUTE}", out bad), "U4 StoryText accepts {JUMP} / {kb:} / {pad:} next to story tokens (bad: " + bad + ")");
            _ctx.Check(!StoryText.TokensResolve("{FROBNICATE}", out _), "U4 unknown tokens still rejected");
            _ctx.Check(StoryText.Resolve(RulerStoryText) == RulerStoryText, "U4 Resolve leaves input tokens for the HUD formatter");
            _ctx.Check(InputPrompts.Format(RulerStoryText, false, false) == "WHO RULES TODAY? [RIGHT SHIFT]" && InputPrompts.Format(RulerStoryText, true, false) == "WHO RULES TODAY? [D-DOWN]",
                "U4 C15 prompt text kb/pad");
            _ctx.Check(InputPrompts.Format(CampaignChapterCard.SkipHintTemplate, false, false) == "PRESS ANY KEY TO SKIP"
                && InputPrompts.Format(CampaignChapterCard.SkipHintTemplate, true, false) == "PRESS A TO SKIP", "U4 skip hint template kb/pad");
            _ctx.Check(InputPrompts.IsClean(RulerStoryText) && InputPrompts.IsClean(CampaignChapterCard.SkipHintTemplate), "U4 templates audit clean");
        }
    }
}
