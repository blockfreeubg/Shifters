// =====================================================================================
//  SHIFTERS - GamepadIntegrationTests.cs   (integrator2, gamepad1 gates, DevPlay)
//  RunAudit  (gate 3a): MainMenu + every chapter scene L1..L14 in PAD mode (real virtual-pad input):
//            every TutorialTrigger / L1 signal+bridge card Title/Body, HintZone text, Revealable label and
//            DevicePromptText template is audit-clean (InputPrompts.AuditRawKeys), every tutorial Keys entry maps to a
//            pad binding or is a switcher-only slot key (dropped on pad), and every VISIBLE scene text
//            (GamepadTestKit-style: DevicePromptText -> template, live caps skipped) is clean. Allowed exceptions
//            are listed in Allowed() with the reason; everything else fails the gate.
//  RunWalk   (gates 3b + 3c): L1 - every tutorial card shown on keyboard (exact old wording for the converted ones),
//            then a REAL pad flick flips the visible card live, then keyboard again restores it; every live world sign
//            (STEP UP / LIE DOWN HERE / whiteboard / poster) shot through a temporary camera on both devices; hint bubble
//            live switch; skip chip; pause / shift schedule / confirm screens on both devices. Then CH08 (L9 boss):
//            every card + hint on both devices. Shots gpi_<scene>_<kb|pad>_<what>.
//  Run: devplay.sh <clone> gpi_audit -devSetup Shifters.GamepadIntegrationTests.RunAudit
//         -devScene Assets/Shifters/Scenes/MainMenu.unity -devSize 1920x1080 -devSeconds 1
//       devplay.sh <clone> gpi_walk -devSetup Shifters.GamepadIntegrationTests.RunWalk
//         -devScene Assets/Shifters/Scenes/L1_TrainingFacility.unity -devSize 1920x1080 -devSeconds 1
// =====================================================================================
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shifters
{
    public static class GamepadIntegrationTests
    {
        static DevPlayContext _ctx;

        static readonly string[] Levels =
        {
            "L1_TrainingFacility", "L2_WeightProblem", "L3_SacrificeZone", "L4_IQTest", "L5_TotalChaos",
            "L6_PerformanceReview", "L7_ActNatural", "L8_ColorReserve", "L9_TheGetaway", "L10_LightsOut",
            "L11_MeMorialPark", "L12_Unmasked", "L13_HostileTakeover", "L14_AllInFavor",
        };

        // L1 cards converted this round: id -> (exact OLD keyboard body, plain pad body)
        static readonly string[,] L1Cards =
        {
            { "movement", "WASD to walk, MOUSE to look around, SPACE to jump, hold LEFT SHIFT to sprint.", "LS to walk, RS to look around, A to jump, click L3 to sprint." },
            { "switching", "Press RIGHT SHIFT to open the switcher, or 1/2/3 to switch directly. Teammates stay exactly where you leave them.", "Press D-DOWN to open the switcher. Teammates stay exactly where you leave them." },
            { "team_orders", "Press G: inactive teammates FOLLOW you or HOLD position.", "Press D-LEFT: inactive teammates FOLLOW you or HOLD position." },
            { "bridge", "Need a bridge? Try using your teammate. Stand at the edge and press B.", "Need a bridge? Try using your teammate. Stand at the edge and press LB." },
            { "bridge_cross", "Switch and walk across. As the bridge, press B again to recover - you'll snap to the far side.", "Switch and walk across. As the bridge, press LB again to recover - you'll snap to the far side." },
            { "throwing", "Strong characters can pick up (F) and throw (Q) teammates. Stand on THROW FROM HERE, face the balcony, look up a little.", "Strong characters can pick up (Y) and throw (RB) teammates. Stand on THROW FROM HERE, face the balcony, look up a little." },
            { "intellect", "Smart characters hack terminals and see hidden clues. Walk up and press E.", "Smart characters hack terminals and see hidden clues. Walk up and press X." },
            { "speed", "Fast characters beat timed doors. Press E on the timed button, then hold LEFT SHIFT and run.", "Fast characters beat timed doors. Press X on the timed button, then click L3 and run." },
            { "perspective", "Each character sees the world differently. Switch and look for highlights. Press R to scan.", "Each character sees the world differently. Switch and look for highlights. Press R3 to scan." },
            { "weapon", "One of you has a gun - look for the weapon icon. RIGHT MOUSE aims, LEFT MOUSE fires.", "One of you has a gun - look for the weapon icon. LT aims, RT fires." },
        };

        // L1 live world signs: (old keyboard text, plain pad text)
        static readonly string[,] L1Signs =
        {
            { "STEP UP  (SPACE)", "STEP UP  (A)" },
            { "LIE DOWN HERE  [B]", "LIE DOWN HERE  [LB]" },
            { "HOW TO SHIFT:\n1. PANIC\n2. PRESS SHIFT\n3. PANIC LESS", "HOW TO SHIFT:\n1. PANIC\n2. PRESS D-DOWN\n3. PANIC LESS" },
            { "ESPECIALLY WHEN YOU PRESS RIGHT SHIFT", "ESPECIALLY WHEN YOU PRESS D-DOWN" },
        };

        // =========================================================== gate 3a
        public static IEnumerator RunAudit(DevPlayContext ctx)
        {
            _ctx = ctx;
            StoryPlayer.StoryModeOverride = 2;
            ShiftersInput.SetUsingGamepad(false);
            int total = 0, allowed = 0;
            // MainMenu (the -devScene): menu screens in pad mode
            yield return _ctx.Wait(3f);
            yield return GamepadTestKit.UseGamepad();
            yield return _ctx.Wait(0.6f);
            AuditVisible("MainMenu", ref total, ref allowed);
            _ctx.Shot("gpi_mainmenu_pad");
            for (int i = 0; i < Levels.Length; i++)
            {
                yield return Load(Levels[i]);
                if (!Ready(Levels[i])) continue;
                yield return GamepadTestKit.UseGamepad();
                yield return _ctx.Wait(0.6f);
                _ctx.Check(ShiftersInput.UsingGamepad, Levels[i] + ": pad mode active");
                AuditData(Levels[i], ref total, ref allowed);
                AuditVisible(Levels[i], ref total, ref allowed);
                if (i == 0) _ctx.Shot("gpi_audit_l1_pad");
            }
            _ctx.Check(total == 0, "GATE 3a: 0 raw keyboard key names outside tokens in all player-facing texts of MainMenu + L1..L14 (" + total + " hits, " + allowed + " allowed exceptions logged)");
            GamepadTestKit.Cleanup();
            StoryPlayer.StoryModeOverride = -1;
        }

        static void AuditData(string scene, ref int total, ref int allowed)
        {
            int n = 0;
            var msgs = new List<TutorialMessage>();
            foreach (var t in Object.FindObjectsByType<TutorialTrigger>(FindObjectsInactive.Include)) if (t.Message != null) msgs.Add(t.Message);
            foreach (var t in Object.FindObjectsByType<L1SignalTutorial>(FindObjectsInactive.Include)) if (t.Message != null) msgs.Add(t.Message);
            foreach (var t in Object.FindObjectsByType<L1BridgeTutorial>(FindObjectsInactive.Include)) if (t.Message != null) msgs.Add(t.Message);
            foreach (var m in msgs)
            {
                n++;
                Count(scene + " card " + m.Id + " title", m.Title, ref total, ref allowed);
                Count(scene + " card " + m.Id + " body", m.Body, ref total, ref allowed);
                if (m.Keys == null) continue;
                foreach (var k in m.Keys)
                {
                    string a = KeyLabels.ActionForKey(k);
                    if (a == null || (string.IsNullOrEmpty(KeyLabels.Gamepad(a)) && a != "Zoom"))
                    { total++; _ctx.Log("HIT " + scene + " card " + m.Id + ": key '" + k + "' unknown / no pad binding"); }
                    else if (KeyLabels.PadSwitcherOnly(a) || a == "Zoom")
                        _ctx.Log("info " + scene + " card " + m.Id + ": key '" + k + "' is keyboard-only (switcher slot -> shown as D-DOWN / wheel -> dropped) on pad (by design)");
                }
            }
            foreach (var h in Object.FindObjectsByType<HintZone>(FindObjectsInactive.Include))
            { n++; Count(scene + " hint", h.RealHint, ref total, ref allowed); Count(scene + " vague hint", h.VagueHint, ref total, ref allowed); }
            foreach (var r in Object.FindObjectsByType<Revealable>(FindObjectsInactive.Include))
            { n++; Count(scene + " revealable " + r.name, r.Label, ref total, ref allowed); }
            int live = 0;
            foreach (var d in Object.FindObjectsByType<DevicePromptText>(FindObjectsInactive.Include))
            { n++; live++; Count(scene + " live text " + d.name, d.Template, ref total, ref allowed); }
            _ctx.Log(scene + ": data audit over " + n + " texts (" + msgs.Count + " cards, " + live + " live DevicePromptText)");
        }

        static void AuditVisible(string scene, ref int total, ref int allowed)
        {
            var tut = Object.FindAnyObjectByType<HudTutorial>(FindObjectsInactive.Include);
            int n = 0;
            foreach (var t in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Exclude))
            {
                if (!t.isActiveAndEnabled || string.IsNullOrEmpty(t.text)) continue;
                if (t.GetComponentInParent<DeviceKeyCap>(true) != null) continue;          // live caps: device-driven
                if (tut != null && t.transform.IsChildOf(tut.transform)) continue;          // card text: audited via its template (AuditData)
                if (t.GetComponentInParent<PerspectiveLabelChip>(true) != null) continue;   // revealable label chip: rendered for the device from its Label template (audited in AuditData)
                var g = t.GetComponentInParent<CanvasGroup>();
                if (g != null && g.alpha <= 0.01f) continue;                                // faded out = not on screen
                var dpt = t.GetComponent<DevicePromptText>();
                n++;
                Count(scene + " visible " + Path(t.transform), dpt != null ? dpt.Template : t.text, ref total, ref allowed);
            }
            if (tut != null && tut.Visible)
                for (int i = 0; i < tut.KeyCount; i++) Count(scene + " card key cap", "[" + tut.KeyText(i) + "]", ref total, ref allowed, true);
            _ctx.Log(scene + ": visible-text audit over " + n + " texts");
        }

        static void Count(string what, string text, ref int total, ref int allowed, bool padLabel = false)
        {
            if (string.IsNullOrEmpty(text)) return;
            var hits = InputPrompts.AuditRawKeys(text);
            if (hits.Count == 0) return;
            for (int i = hits.Count - 1; i >= 0; i--)
            {
                string why = padLabel ? PadCapReason(hits[i]) : Allowed(hits[i], text);
                if (why == null) continue;
                allowed++;
                _ctx.Log("allowed " + what + ": " + hits[i] + " (" + why + ")  <- \"" + text.Replace("\n", "\\n") + "\"");
                hits.RemoveAt(i);
            }
            if (hits.Count == 0) return;
            total += hits.Count;
            _ctx.Log("HIT " + what + ": " + string.Join(", ", hits) + "  <- \"" + text.Replace("\n", "\\n") + "\"");
        }

        /// <summary>Allowed exceptions (pad mode): the reason, or null when the hit is a real raw key name.</summary>
        static string Allowed(string hit, string text)
        {
            if (hit.StartsWith("HOLD ") && text.Contains(hit + " S")) return "duration 'HOLD n S', not a key";
            if (hit == "PRESS A" && text.Contains("PRESS A TO SKIP")) return "pad-only text: A is the pad button";
            if (hit == "SPACE" && text.Contains("PERSONAL SPACE")) return "story/enemy line, not a key";
            if (hit == "CLICK" && text.Contains("CLICK!")) return "sound word";
            int sp = hit.IndexOf(' ');
            if (sp > 0 && text.IndexOf(hit.Substring(0, sp) + " {", System.StringComparison.OrdinalIgnoreCase) >= 0
                && text.IndexOf(hit, System.StringComparison.Ordinal) < 0)
                return "audit artifact: the verb is followed by a token (stripped), the next word is the next line";
            return null;
        }

        /// <summary>Card key caps in pad mode are pad labels; "[X]"/"[B]"/"[A]" etc. are the pad buttons, never keyboard keys.</summary>
        static string PadCapReason(string hit)
        {
            string inner = hit.Trim('[', ']');
            foreach (var a in KeyLabels.Actions) if (string.Equals(KeyLabels.Gamepad(a), inner, System.StringComparison.OrdinalIgnoreCase)) return "pad button cap";
            return null;
        }

        // =========================================================== gates 3b + 3c
        public static IEnumerator RunWalk(DevPlayContext ctx)
        {
            _ctx = ctx;
            StoryPlayer.StoryModeOverride = 2;
            ShiftersInput.SetUsingGamepad(false);
            yield return WaitReady();
            if (Ready("L1_TrainingFacility"))
            {
                yield return Cards("l1", true);
                yield return Signs();
                yield return HintLive();
                yield return SkipChip();
                yield return Screens();
            }
            yield return Load("L9_TheGetaway");
            if (Ready("L9_TheGetaway")) { yield return Cards("ch08", false); yield return HintLive("ch08"); }
            GamepadTestKit.Cleanup();
            StoryPlayer.StoryModeOverride = -1;
        }

        static IEnumerator Cards(string tag, bool l1Table)
        {
            var tut = Object.FindAnyObjectByType<HudTutorial>(FindObjectsInactive.Include);
            if (!_ctx.Check(tut != null, tag + ": HudTutorial")) yield break;
            var msgs = new List<TutorialMessage>();
            foreach (var t in Object.FindObjectsByType<TutorialTrigger>(FindObjectsInactive.Include)) if (t.Message != null) msgs.Add(t.Message);
            foreach (var t in Object.FindObjectsByType<L1SignalTutorial>(FindObjectsInactive.Include)) if (t.Message != null) msgs.Add(t.Message);
            foreach (var t in Object.FindObjectsByType<L1BridgeTutorial>(FindObjectsInactive.Include)) if (t.Message != null) msgs.Add(t.Message);
            msgs.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            int converted = 0;
            foreach (var src in msgs)
            {
                var m = new TutorialMessage { Id = src.Id + "_gpi", Title = src.Title, Body = src.Body, Keys = src.Keys, Important = true, Duration = 0f };
                yield return GamepadTestKit.UseKeyboard();
                GameEvents.RaiseTutorialShow(m);
                yield return _ctx.Wait(1.0f);
                string kb = tut.BodyText, kbKeys = Keys(tut);
                bool tokens = (src.Body ?? "").IndexOf('{') >= 0;
                _ctx.Check(tut.Visible && kb == InputPrompts.Format(src.Body ?? "", false, false), tag + " kb card " + src.Id + ": '" + kb + "' keys " + kbKeys);
                int row = l1Table ? Row(src.Id) : -1;
                if (row >= 0) { converted++; _ctx.Check(kb == L1Cards[row, 1], tag + " kb card " + src.Id + " = exact pre-gamepad wording"); }
                _ctx.Shot("gpi_" + tag + "_kb_" + src.Id);
                yield return GamepadTestKit.UseGamepad();          // the card stays on screen: live re-render (gate 3c)
                yield return _ctx.Wait(0.6f);
                string pad = Strip(tut.BodyText), padKeys = Keys(tut);
                _ctx.Check(tut.Visible && pad == InputPrompts.Format(src.Body ?? "", true, false) && InputPrompts.IsClean(src.Body ?? ""),
                    tag + " pad card " + src.Id + " (live, same card): '" + pad + "' keys " + padKeys);
                if (row >= 0) _ctx.Check(pad == L1Cards[row, 2], tag + " pad card " + src.Id + " = expected pad wording");
                if (tokens) _ctx.Check(pad != kb, tag + " card " + src.Id + " flipped from keyboard to pad labels without reopening");
                _ctx.Check(PadKeysOk(padKeys), tag + " pad card " + src.Id + ": key caps are pad labels (" + padKeys + ")");
                if (HasSlotKey(src.Keys))   // verify fix 1: slot keys 1/2/3 show the switcher button on a pad, never an empty key row
                    _ctx.Check(("," + padKeys + ",").Contains(",D-DOWN,"), tag + " pad card " + src.Id + ": slot keys shown as D-DOWN (" + padKeys + ")");
                if (src.Id == "graduation") _ctx.Check(padKeys == "D-DOWN", tag + " pad card graduation caps = D-DOWN (" + padKeys + ")");
                _ctx.Shot("gpi_" + tag + "_pad_" + src.Id);
                yield return GamepadTestKit.UseKeyboard();
                yield return _ctx.Wait(0.5f);
                _ctx.Check(tut.Visible && tut.BodyText == kb && Keys(tut) == kbKeys, tag + " card " + src.Id + " back to keyboard live");
                GameEvents.RaiseTutorialHide();
                yield return _ctx.Wait(0.4f);
            }
            if (l1Table) _ctx.Check(converted == L1Cards.GetLength(0), tag + ": all " + L1Cards.GetLength(0) + " converted L1 cards found and checked (" + converted + ")");
            _ctx.Log(tag + ": walked " + msgs.Count + " cards");
        }

        static bool HasSlotKey(string[] keys)
        {
            if (keys == null) return false;
            foreach (var k in keys) if (KeyLabels.PadSwitcherOnly(KeyLabels.ActionForKey(k))) return true;
            return false;
        }

        static int Row(string id) { for (int i = 0; i < L1Cards.GetLength(0); i++) if (L1Cards[i, 0] == id) return i; return -1; }

        static bool PadKeysOk(string keys)
        {
            if (string.IsNullOrEmpty(keys)) return true;
            foreach (var k in keys.Split(','))
            {
                bool ok = false;
                foreach (var a in KeyLabels.Actions) if (string.Equals(KeyLabels.Gamepad(a), k, System.StringComparison.OrdinalIgnoreCase)) ok = true;
                if (!ok) return false;
            }
            return true;
        }

        static IEnumerator Signs()
        {
            var main = Camera.main;
            var camGo = new GameObject("GpiSignCam");
            var cam = camGo.AddComponent<Camera>();
            cam.depth = 90f; cam.fieldOfView = 50f; cam.nearClipPlane = 0.05f;
            if (main != null) main.enabled = false;
            var all = Object.FindObjectsByType<DevicePromptText>(FindObjectsInactive.Include);
            int found = 0;
            for (int s = 0; s < L1Signs.GetLength(0); s++)
            {
                DevicePromptText d = null;
                foreach (var x in all) if (x.Text != null && x.GetComponentInParent<Canvas>() == null && InputPrompts.Format(x.Template, false, false) == L1Signs[s, 0]) d = x;
                if (!_ctx.Check(d != null, "l1 sign '" + L1Signs[s, 0].Replace("\n", "\\n") + "' has a live template")) continue;
                found++;
                var t = d.Text.transform;
                float dist = Mathf.Max(2.2f, d.Text.bounds.size.magnitude * 1.1f);
                camGo.transform.position = t.position - t.forward * dist;
                camGo.transform.rotation = Quaternion.LookRotation(t.forward, t.up);
                string tag = "sign" + s;
                yield return GamepadTestKit.UseKeyboard();
                yield return _ctx.Wait(0.4f);
                _ctx.Check(d.Text.text == L1Signs[s, 0], "l1 kb " + tag + ": '" + d.Text.text.Replace("\n", "\\n") + "' (exact old text)");
                _ctx.Shot("gpi_l1_kb_" + tag, cam);
                yield return GamepadTestKit.UseGamepad();
                yield return _ctx.Wait(0.4f);
                _ctx.Check(Strip(d.Text.text) == L1Signs[s, 1], "l1 pad " + tag + " (live): '" + Strip(d.Text.text).Replace("\n", "\\n") + "'");
                _ctx.Shot("gpi_l1_pad_" + tag, cam);
            }
            yield return GamepadTestKit.UseKeyboard();
            if (main != null) main.enabled = true;
            Object.Destroy(camGo);
            _ctx.Check(found == L1Signs.GetLength(0), "l1: all " + L1Signs.GetLength(0) + " live world signs found (" + found + ")");
            yield return _ctx.Wait(0.3f);
        }

        static IEnumerator HintLive(string tag = "l1")
        {
            var hud = Object.FindAnyObjectByType<HudHint>(FindObjectsInactive.Include);
            HintZone zone = null;
            foreach (var h in Object.FindObjectsByType<HintZone>(FindObjectsInactive.Include)) if (h.RealHint != null && h.RealHint.IndexOf('{') >= 0) zone = h;
            if (zone == null) { _ctx.Log(tag + ": no tokenised hint zone (nothing to switch)"); yield break; }
            if (!_ctx.Check(hud != null, tag + ": HudHint")) yield break;
            yield return GamepadTestKit.UseKeyboard();
            GameEvents.RaiseHint(zone.RealHint);   // what HintSystem raises now: the template; the bubble formats it live
            yield return _ctx.Wait(0.8f);
            string kb = hud.Text;
            _ctx.Check(kb == InputPrompts.Format(zone.RealHint, false, false), tag + " kb hint bubble: '" + kb + "'");
            _ctx.Shot("gpi_" + tag + "_kb_hint");
            yield return GamepadTestKit.UseGamepad();
            yield return _ctx.Wait(0.5f);
            string pad = Strip(hud.Text);
            _ctx.Check(hud.Visible && pad == InputPrompts.Format(zone.RealHint, true, false) && pad != kb, tag + " pad hint bubble flipped live: '" + pad + "'");
            _ctx.Shot("gpi_" + tag + "_pad_hint");
            yield return GamepadTestKit.UseKeyboard();
            yield return _ctx.Wait(0.4f);
            _ctx.Check(hud.Text == kb, tag + " hint bubble back to keyboard live");
            hud.Clear();
        }

        static IEnumerator SkipChip()
        {
            var chud = CampaignHud.Instance;
            if (!_ctx.Check(chud != null && chud.Skip != null, "l1: campaign skip chip present")) yield break;
            yield return GamepadTestKit.UseKeyboard();
            chud.Skip.Set(true);
            yield return _ctx.Wait(0.8f);
            _ctx.Check(chud.Skip.KeyText == "BACKSPACE", "kb story skip chip: HOLD [" + chud.Skip.KeyText + "] TO SKIP");
            _ctx.Shot("gpi_l1_kb_skipchip");
            yield return GamepadTestKit.UseGamepad();
            yield return _ctx.Wait(0.5f);
            _ctx.Check(chud.Skip.KeyText == "BACK", "pad story skip chip (live): HOLD [" + chud.Skip.KeyText + "] TO SKIP");
            _ctx.Shot("gpi_l1_pad_skipchip");
            chud.Skip.Set(false);
            yield return GamepadTestKit.UseKeyboard();
            yield return _ctx.Wait(0.4f);
        }

        static IEnumerator Screens()
        {
            var ui = UIManager.Instance;
            if (!_ctx.Check(ui != null, "UIManager")) yield break;
            for (int dev = 0; dev < 2; dev++)
            {
                string d = dev == 0 ? "kb" : "pad";
                if (dev == 0) yield return GamepadTestKit.UseKeyboard(); else yield return GamepadTestKit.UseGamepad();
                ui.OpenPause();
                yield return _ctx.Wait(1.0f);
                if (dev == 1) { int a = 0, h = 0; AuditVisible("pause(pad)", ref h, ref a); _ctx.Check(h == 0, "pad pause screen: 0 raw key names (" + h + ")"); }
                _ctx.Shot("gpi_" + d + "_pause");
                ui.OpenShiftSchedule();
                yield return _ctx.Wait(1.0f);
                if (dev == 1) { int a = 0, h = 0; AuditVisible("schedule(pad)", ref h, ref a); _ctx.Check(h == 0, "pad schedule screen: 0 raw key names (" + h + ")"); }
                _ctx.Shot("gpi_" + d + "_schedule");
                ui.AskConfirm("CLOCK IN?", "Start this chapter? Your current progress in this shift is lost.", "CLOCK IN", "NOT TODAY", null, null, false);
                yield return _ctx.Wait(1.0f);
                if (dev == 1) { int a = 0, h = 0; AuditVisible("confirm(pad)", ref h, ref a); _ctx.Check(h == 0, "pad confirm screen: 0 raw key names (" + h + ")"); }
                _ctx.Shot("gpi_" + d + "_confirm");
                ui.Pop(); yield return _ctx.Wait(0.4f);
                ui.Pop(); yield return _ctx.Wait(0.4f);
                ui.ClosePause(); yield return _ctx.Wait(0.8f);
            }
            yield return GamepadTestKit.UseKeyboard();
            // fix pass: Settings > GAME sensitivity row names the device (slider scales mouse AND right-stick look)
            ui.OpenPause(); yield return _ctx.Wait(0.5f);
            ui.OpenSettings(); yield return _ctx.Wait(0.8f);
            TMPro.TMP_Text sens = null;
            var set = Object.FindAnyObjectByType<SettingsScreen>(FindObjectsInactive.Include);
            if (set != null) foreach (var r in set.Rows) if (r != null && r.Label == "MOUSE SENSITIVITY") { var lt = r.transform.parent != null ? r.transform.parent.Find("Label") : null; sens = lt != null ? lt.GetComponent<TMPro.TMP_Text>() : null; }
            if (_ctx.Check(sens != null, "settings: sensitivity row label found"))
            {
                _ctx.Check(sens.text == "MOUSE SENSITIVITY", "kb settings row: '" + sens.text + "'");
                _ctx.Shot("gpi_kb_settings");
                yield return GamepadTestKit.UseGamepad(); yield return _ctx.Wait(0.5f);
                _ctx.Check(sens.text == "LOOK SENSITIVITY", "pad settings row (live): '" + sens.text + "'");
                { int a = 0, h = 0; AuditVisible("settings(pad)", ref h, ref a); _ctx.Check(h == 0, "pad settings screen: 0 raw key names (" + h + ")"); }
                _ctx.Shot("gpi_pad_settings");
                yield return GamepadTestKit.UseKeyboard(); yield return _ctx.Wait(0.5f);
                _ctx.Check(sens.text == "MOUSE SENSITIVITY", "kb settings row back live: '" + sens.text + "'");
            }
            ui.Pop(); yield return _ctx.Wait(0.4f);
            ui.ClosePause(); yield return _ctx.Wait(0.8f);
        }

        // =========================================================== helpers
        static IEnumerator Load(string name)
        {
            if (SceneManager.GetActiveScene().name != name)
            {
                string path = "Assets/Shifters/Scenes/" + name + ".unity";
#if UNITY_EDITOR
                UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Single));
#else
                SceneManager.LoadScene(name);
#endif
                yield return null; yield return null;
            }
            yield return WaitReady();
        }

        static IEnumerator WaitReady()
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
            if (CharacterManager.Instance != null && CharacterManager.Instance.Team != null)
                foreach (var c in CharacterManager.Instance.Team) if (c != null) c.ExternalControl = true;
            yield return _ctx.Wait(0.5f);
        }

        static bool Ready(string scene) => _ctx.Check(SceneManager.GetActiveScene().name == scene && CharacterManager.Instance != null, scene + ": loaded with a team");

        static string Keys(HudTutorial t)
        {
            var l = new List<string>();
            for (int i = 0; i < t.KeyCount; i++) l.Add(t.KeyText(i));
            return string.Join(",", l);
        }

        static string Strip(string s) => System.Text.RegularExpressions.Regex.Replace(s ?? "", "<[^>]{0,39}>", "");

        static string Path(Transform t)
        {
            string p = t.name;
            for (var x = t.parent; x != null; x = x.parent) p = x.name + "/" + p;
            return p;
        }
    }
}
