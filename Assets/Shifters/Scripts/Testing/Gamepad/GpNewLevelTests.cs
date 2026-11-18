// =====================================================================================
//  SHIFTERS - GpNewLevelTests.cs   (gp_new, DevPlay)
//  Campaign levels L6-L14 gamepad prompts. Loads every campaign scene L6..L14 in one run and checks:
//   N1 every TutorialTrigger Title/Body, HintZone real/vague hint and Revealable label is audit-clean
//      (InputPrompts.AuditRawKeys) and every tutorial Keys entry maps to an action with a pad binding;
//   N2 the converted cards / hints / labels render the OLD keyboard text on keyboard and the pad buttons on a pad,
//      switching LIVE on real virtual-device input. Shots <level>_kb_* / <level>_pad_*.
//   N3 GamepadTestKit.AuditScene per level (informational: kit-owned texts are other agents' scope).
//  Run: devplay.sh <clone> gp_new_levels -devSetup Shifters.GpNewLevelTests.RunAll
//         -devScene Assets/Shifters/Scenes/L6_PerformanceReview.unity -devSize 1920x1080 -devSeconds 1
// =====================================================================================
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shifters
{
    public static class GpNewLevelTests
    {
        static DevPlayContext _ctx;

        static readonly string[] Scenes =
        {
            "L6_PerformanceReview", "L7_ActNatural", "L8_ColorReserve", "L9_TheGetaway", "L10_LightsOut",
            "L11_MeMorialPark", "L12_Unmasked", "L13_HostileTakeover", "L14_AllInFavor",
        };

        public static IEnumerator RunAll(DevPlayContext ctx)
        {
            _ctx = ctx;
            StoryPlayer.StoryModeOverride = 2;   // story OFF: no intro scenes over the HUD
            ShiftersInput.SetUsingGamepad(false);
            for (int i = 0; i < Scenes.Length; i++)
            {
                string name = Scenes[i];
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
                yield return Level(name);
            }
            GamepadTestKit.Cleanup();
            StoryPlayer.StoryModeOverride = -1;
        }

        static IEnumerator Level(string scene)
        {
            float t0 = Time.realtimeSinceStartup;
            while ((CharacterManager.Instance == null || CharacterManager.Instance.Team == null || CharacterManager.Instance.Team.Count < 3
                    || UIManager.Instance == null || !UIManager.Instance.HudVisible) && Time.realtimeSinceStartup - t0 < 40f)
                yield return null;
            yield return _ctx.Wait(1.5f);
            if (!_ctx.Check(SceneManager.GetActiveScene().name == scene && CharacterManager.Instance != null, scene + ": loaded with a team")) yield break;
            ScreenFader.Set(0f);
            GameEvents.RaiseTutorialHide();
            var chud = CampaignHud.Instance;
            if (chud != null && chud.Card.Showing) chud.Card.Close(true);
            foreach (var c in CharacterManager.Instance.Team) if (c != null) c.ExternalControl = true;
            yield return GamepadTestKit.UseKeyboard();
            yield return _ctx.Wait(0.4f);

            // ---------------- N1 data audit
            int bad = 0, tuts = 0, hints = 0, labels = 0;
            foreach (var t in Object.FindObjectsByType<TutorialTrigger>(FindObjectsInactive.Include))
            {
                tuts++;
                var m = t.Message;
                if (m == null) continue;
                bad += Clean(scene + " tutorial " + m.Id + " title", m.Title);
                bad += Clean(scene + " tutorial " + m.Id + " body", m.Body);
                if (m.Keys != null)
                    foreach (var k in m.Keys)
                    {
                        string a = KeyLabels.ActionForKey(k);
                        if (a == null || string.IsNullOrEmpty(KeyLabels.Gamepad(a)) || KeyLabels.PadSwitcherOnly(a))
                        { bad++; _ctx.Log("N1 " + scene + " tutorial " + m.Id + ": key '" + k + "' has no gameplay pad binding (action " + a + ")"); }
                    }
            }
            foreach (var h in Object.FindObjectsByType<HintZone>(FindObjectsInactive.Include))
            {
                hints++;
                bad += Clean(scene + " hint real", h.RealHint);
                bad += Clean(scene + " hint vague", h.VagueHint);
            }
            foreach (var r in Object.FindObjectsByType<Revealable>(FindObjectsInactive.Include))
            {
                labels++;
                bad += Clean(scene + " revealable " + r.name, r.Label);
            }
            _ctx.Check(bad == 0, "N1 " + scene + ": " + tuts + " tutorials, " + hints + " hints, " + labels + " revealables audit-clean (" + bad + " raw key names)");

            // ---------------- N2 converted texts, both devices, live
            string tag = scene.Substring(0, scene.IndexOf('_'));
            switch (scene)
            {
                case "L7_ActNatural":
                    yield return Card(tag, "ch04_actnatural", null, null, "TAB,Q,E", "D-DOWN,RB,X");
                    break;
                case "L8_ColorReserve":
                    yield return Card(tag, "ch07_keys", null, null, "G,E,RSHIFT", "D-LEFT,X,D-DOWN");
                    yield return Hint(tag, "then {kb:press {Interact}", "Park everyone on a key pad, then press E, SHIFT, E, SHIFT, E.",
                        "Park everyone on a key pad, then press X on each pad in turn, SHIFTing between them with D-DOWN.");
                    break;
                case "L13_HostileTakeover":
                    yield return Card(tag, "ch13_fist", "Only the Brute can punch it (E).", "Only the Brute can punch it (X).", "E", "X");
                    yield return Card(tag, "ch13_capture", "TAUNT him (T) or TURBO-tag him. Then the Brute grabs the pod (F).",
                        "TAUNT him (D-RIGHT) or TURBO-tag him. Then the Brute grabs the pod (Y).", "T,F", "D-RIGHT,Y");
                    yield return Hint(tag, "punches it ({Interact})", "the Brute punches it (E). Three times.", "the Brute punches it (X). Three times.");
                    Revealable clue = null;
                    foreach (var r in Object.FindObjectsByType<Revealable>(FindObjectsInactive.Include)) if (r.Label != null && r.Label.Contains("ARGUMENT")) clue = r;
                    _ctx.Check(clue != null && InputPrompts.Format(clue.Label, false, false) == "HE CAN'T RESIST AN ARGUMENT. (T)"
                               && InputPrompts.Format(clue.Label, true, false) == "HE CAN'T RESIST AN ARGUMENT. (D-RIGHT)",
                        "N2 L13 argue clue label kb/pad (" + (clue != null ? clue.Label : "missing") + ")");
                    break;
                case "L14_AllInFavor":
                    yield return RuntimeCard(tag, "ch14_bench", "BENCH FOR THREE", "BENCH FOR THREE: everybody sit down ({Interact}). Rulers sit together.",
                        "BENCH FOR THREE: everybody sit down (E). Rulers sit together.", "BENCH FOR THREE: everybody sit down (X). Rulers sit together.");
                    yield return Hint(tag, "({AIMode} = stay)", "One intern per voter plate (G = stay), then AYE, SHIFT, AYE, SHIFT, AYE within 5 s.",
                        "One intern per voter plate (D-LEFT = stay), then AYE, SHIFT, AYE, SHIFT, AYE within 5 s.");
                    yield return Hint(tag + "b", "bench ({Interact})", "Everybody sits on the bench (E). All three.", "Everybody sits on the bench (X). All three.");
                    break;
            }

            // ---------------- N3 informational scene audit
            var audit = GamepadTestKit.AuditScene();
            _ctx.Log("N3 " + scene + " scene audit (informational): " + audit.Count + " texts with raw keyboard names");
            for (int i = 0; i < audit.Count && i < 30; i++) _ctx.Log("  audit " + tag + ": " + audit[i]);
            GameEvents.RaiseTutorialHide();
            yield return GamepadTestKit.UseKeyboard();
        }

        static int Clean(string what, string text)
        {
            var hits = InputPrompts.AuditRawKeys(text);
            // known false positive: "HOLD 2 S" (hold for 2 seconds, L8 latch label) is a duration, not a key
            hits.RemoveAll(h => h.Length >= 6 && char.IsDigit(h[h.Length - 1]) && h.StartsWith("HOLD ", System.StringComparison.OrdinalIgnoreCase) && text.Contains(h + " S"));
            if (hits.Count == 0) return 0;
            _ctx.Log("N1 " + what + ": " + string.Join(", ", hits) + "  ::  " + text);
            return hits.Count;
        }

        static IEnumerator Card(string tag, string id, string kbPart, string padPart, string kbKeys, string padKeys)
        {
            TutorialTrigger trig = null;
            foreach (var t in Object.FindObjectsByType<TutorialTrigger>(FindObjectsInactive.Include)) if (t.Message != null && t.Message.Id == id) trig = t;
            if (!_ctx.Check(trig != null, "N2 " + tag + " tutorial " + id + " present")) yield break;
            var m = trig.Message;
            yield return Show(tag, id, new TutorialMessage { Id = m.Id + "_gptest", Title = m.Title, Body = m.Body, Keys = m.Keys, Important = true, Duration = 0f },
                kbPart, padPart, kbKeys, padKeys);
        }

        static IEnumerator RuntimeCard(string tag, string id, string title, string body, string kbBody, string padBody)
        {
            // the real L14Hearing.ShowTutorial path, then compare against the template used here (guards source drift)
            var mi = typeof(L14Hearing).GetMethod("ShowTutorial", BindingFlags.NonPublic | BindingFlags.Static);
            var tut = Object.FindAnyObjectByType<HudTutorial>(FindObjectsInactive.Include);
            if (!_ctx.Check(mi != null && tut != null, "N2 " + tag + " L14Hearing.ShowTutorial + HudTutorial found")) yield break;
            mi.Invoke(null, new object[] { id, title, body, false });
            yield return _ctx.Wait(1.2f);
            _ctx.Check(tut.Visible && tut.BodyText == kbBody, "N2 " + tag + " kb " + id + ": '" + tut.BodyText + "'");
            _ctx.Shot(tag + "_kb_" + id);
            yield return GamepadTestKit.UseGamepad();
            yield return _ctx.Wait(0.6f);
            string plain = StripTags(tut.BodyText);
            _ctx.Check(tut.Visible && plain == padBody, "N2 " + tag + " pad " + id + " (live): '" + plain + "'");
            _ctx.Shot(tag + "_pad_" + id);
            yield return GamepadTestKit.UseKeyboard();
            yield return _ctx.Wait(0.4f);
            _ctx.Check(tut.BodyText == kbBody, "N2 " + tag + " back to kb " + id);
            GameEvents.RaiseTutorialHide();
            yield return _ctx.Wait(0.4f);
        }

        static IEnumerator Show(string tag, string id, TutorialMessage m, string kbPart, string padPart, string kbKeys, string padKeys)
        {
            var tut = Object.FindAnyObjectByType<HudTutorial>(FindObjectsInactive.Include);
            if (!_ctx.Check(tut != null, "N2 " + tag + " HudTutorial")) yield break;
            GameEvents.RaiseTutorialShow(m);
            yield return _ctx.Wait(1.2f);
            string kbBody = InputPrompts.Format(m.Body, false, false);
            _ctx.Check(tut.Visible && tut.BodyText == kbBody && (kbPart == null || kbBody.Contains(kbPart)) && Keys(tut) == kbKeys,
                "N2 " + tag + " kb " + id + ": keys " + Keys(tut) + " body '" + tut.BodyText + "'");
            _ctx.Shot(tag + "_kb_" + id);
            yield return GamepadTestKit.UseGamepad();
            yield return _ctx.Wait(0.6f);
            string plain = StripTags(tut.BodyText);
            _ctx.Check(tut.Visible && (padPart == null || plain.Contains(padPart)) && Keys(tut) == padKeys,
                "N2 " + tag + " pad " + id + " (live): keys " + Keys(tut) + " body '" + plain + "'");
            _ctx.Shot(tag + "_pad_" + id);
            yield return GamepadTestKit.UseKeyboard();
            yield return _ctx.Wait(0.4f);
            _ctx.Check(tut.BodyText == kbBody && Keys(tut) == kbKeys, "N2 " + tag + " back to kb " + id + ": keys " + Keys(tut));
            GameEvents.RaiseTutorialHide();
            yield return _ctx.Wait(0.4f);
        }

        /// <summary>Hint zone text: checks the template formats to the old keyboard text / the pad text, and shows it in the
        /// hint bubble the way HintSystem raises it (formatted for the current device) for a screenshot on each device.</summary>
        static IEnumerator Hint(string tag, string marker, string kbPart, string padPart)
        {
            HintZone zone = null;
            foreach (var h in Object.FindObjectsByType<HintZone>(FindObjectsInactive.Include)) if (h.RealHint != null && h.RealHint.Contains(marker)) zone = h;
            if (!_ctx.Check(zone != null, "N2 " + tag + " hint with '" + marker + "' present")) yield break;
            string kb = InputPrompts.Format(zone.RealHint, false, false), pad = InputPrompts.Format(zone.RealHint, true, false);
            _ctx.Check(kb.Contains(kbPart) && pad.Contains(padPart), "N2 " + tag + " hint kb '" + kb + "' / pad '" + pad + "'");
            GameEvents.RaiseHint(InputPrompts.FormatRich(zone.RealHint));
            yield return _ctx.Wait(0.8f);
            _ctx.Shot(tag + "_kb_hint");
            yield return GamepadTestKit.UseGamepad();
            GameEvents.RaiseHint(InputPrompts.FormatRich(zone.RealHint));
            yield return _ctx.Wait(0.8f);
            _ctx.Shot(tag + "_pad_hint");
            yield return GamepadTestKit.UseKeyboard();
            yield return _ctx.Wait(0.3f);
        }

        static string StripTags(string s) => System.Text.RegularExpressions.Regex.Replace(s ?? "", "<[^>]{0,39}>", "");

        static string Keys(HudTutorial t)
        {
            var l = new List<string>();
            for (int i = 0; i < t.KeyCount; i++) l.Add(t.KeyText(i));
            return string.Join(",", l);
        }
    }
}
