// SHIFTERS - DevPlay: STORY_KIT tests.
//   devplay.sh <clone> storykit -devSetup Shifters.StoryKitTests.All -devSeconds 5          (gallery + rules + overlays)
//   devplay.sh <clone> storykit_gallery -devSetup Shifters.StoryKitTests.Gallery -devSeconds 5
// Gallery: every StoryKit / ScareKit factory on one floor next to a 1.2 m Max puppet and a real intern puppet (scale),
// per piece renderer / collider / material / text-facing checks, contact sheets (4 x 3 tiles) in $SP/shots/<name>/.
// Rules: ScareRules unit tests (time injected) + live ScareDirector tests (auto windows, stalker freeze, attempt memory,
// punchline <= 3 s, MILD blackout). Overlays: StoryKitOverlayTests.Run.
using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;

namespace Shifters
{
    public static class StoryKitTests
    {
        static readonly Vector3 Base = new Vector3(0f, 0f, 300f);

        public static IEnumerator All(DevPlayContext ctx)
        {
            yield return Gallery(ctx);
            yield return StoryKitOverlayTests.Run(ctx);
        }

        public static IEnumerator Gallery(DevPlayContext ctx)
        {
            RuleTests(ctx);
            var cam = Camera.main;
            ctx.Check(cam != null, "main camera");
            if (cam == null) yield break;
            cam.farClipPlane = Mathf.Max(cam.farClipPlane, 600f);
            yield return Pieces(ctx, cam);
            yield return DirectorTests(ctx, cam);
        }

        // ------------------------------------------------------------------ 1. pure rules (CAMPAIGN 10.1.5)
        static void RuleTests(DevPlayContext ctx)
        {
            var r = new ScareRules();
            ctx.Check(r.TryBegin("a", 0f) == ScareVerdict.Ok, "rules: first cue plays");
            ctx.Check(r.TryBegin("a", 100f) == ScareVerdict.AlreadySeen, "rules: a cue plays once per attempt");
            ctx.Check(r.Check("b", 29.9f) == ScareVerdict.Cooldown, "rules: <= 1 scripted scare per 30 s (29.9 s refused)");
            ctx.Check(r.TryBegin("b", 30.01f) == ScareVerdict.Ok, "rules: next cue after 30 s");
            var c = new ScareRules(); int ok = 0;
            for (int i = 0; i < 8; i++) if (c.TryBegin("c" + i, i * 31f) == ScareVerdict.Ok) ok++;
            ctx.Check(ok == ScareRules.MaxPerChapter && c.Check("c9", 999f) == ScareVerdict.ChapterCap, "rules: <= 6 per chapter (played " + ok + ")");
            var w = new ScareRules();
            w.SetWindow("timer", true);
            ctx.Check(w.Check("d", 1000f) == ScareVerdict.Blocked, "rules: never during a timed window");
            w.SetWindow("timer", false);
            ctx.Check(w.Check("d", 1000f) == ScareVerdict.Ok, "rules: window closed -> ok");
            w.BlockUntil("hazard", 50f);
            ctx.Check(w.Check("d", 40f) == ScareVerdict.Blocked && w.Check("d", 50.5f) == ScareVerdict.Ok, "rules: hazard block expires");
            w.BlockUntil("hazard", 80f); w.BlockUntil("hazard", 60f);
            ctx.Check(w.IsBlocked(70f, out var by) && by == "hazard", "rules: BlockUntil keeps the later end");
            w.TryBegin("e", 100f); w.SetWindow("capture", true); w.ClearWindows();
            ctx.Check(w.HasSeen("e") && w.Count == 1 && w.Check("f", 200f) == ScareVerdict.Ok, "rules: ClearWindows (scene reload) keeps seen ids + count");
            w.Enabled = false;
            ctx.Check(w.Check("g", 500f) == ScareVerdict.Disabled, "rules: disabled");
            ctx.Check(new ScareRules().Check("", 0f) == ScareVerdict.EmptyId, "rules: empty id refused");
            w.Reset(); w.Enabled = true;
            ctx.Check(!w.HasSeen("e") && w.Count == 0 && w.Check("e", 0f) == ScareVerdict.Ok, "rules: Reset = new attempt");
            ctx.Check(ScareRules.PunchlineMaxSeconds <= 3f && ScareRules.StalkerFreezeSeconds >= 2f, "rules: constants (punchline <= 3 s, freeze 2 s)");
        }

        // ------------------------------------------------------------------ 2. gallery
        struct Piece
        {
            public string Name; public int Colliders, MaxRenderers, Backs; public bool Front, Logic;
            public System.Func<Transform, Vector3, GameObject> Build;
        }

        static Piece P(string n, int colliders, System.Func<Transform, Vector3, GameObject> b, bool front = true, int maxR = 40, bool logic = false, int backs = 0)
            => new Piece { Name = n, Colliders = colliders, Build = b, Front = front, MaxRenderers = maxR, Logic = logic, Backs = backs };

        static GameObject Popper(Transform t, Vector3 p, ScareKind k, bool mild)
        {
            var g = Kit.Make("Popper_" + k + (mild ? "_mild" : ""), t, p, Quaternion.Euler(0f, 180f, 0f));
            ScareKit.BuildPopper(g.transform, k, mild);
            return g;
        }

        static List<Piece> PieceList()
        {
            var l = new List<Piece>
            {
                P("MeTvScreen", 0, (t, p) => StoryKit.MeTvScreen(t, p + new Vector3(0f, 2.4f, 0f), null, 4f)),
                P("HoloPoster", 0, (t, p) => StoryKit.HoloPoster(t, p, null, StoryKitLines.Poster(0), 1)),
                P("HoloPosterAct3", 0, (t, p) => StoryKit.HoloPoster(t, p, null, StoryKitLines.Poster(4), 3)),
                P("VillainStatue", 0, (t, p) => StoryKit.VillainStatue(t, p, null, 3f, 0)),
                P("VillainStatueVandal2", 0, (t, p) => StoryKit.VillainStatue(t, p, null, 3f, 2), true, 40, false, 1),
                P("VillainStatueShades", 0, (t, p) => { var s = StoryKit.VillainStatue(t, p, null, 3f, 1); StoryKit.StatueSunglasses(s); return s; }, true, 40, false, 1),
                P("PercyMachine", 0, (t, p) => StoryKit.PercyMachine(t, p, null)),
                P("Tombstone", 0, (t, p) => StoryKit.Tombstone(t, p, null, TombstoneRowView.Format(new LostEntry { Name = "Gary", Cause = "Fell into the void", ChapterId = "L3", CloneName = "Gary II" }))),
                P("TombstoneRow", 0, (t, p) =>
                {
                    var g = StoryKit.TombstoneRow(t, p, null, 4);
                    var v = g.GetComponent<TombstoneRowView>();
                    if (v != null)
                    {
                        v.AutoFillFromLostLog = false;
                        v.FillFrom(new List<LostEntry> { new LostEntry { Name = "Gary", Cause = "Crushed by a press", ChapterId = "L2", CloneName = "Gary 2" },
                                                         new LostEntry { Name = "Bea", Cause = "Laser", ChapterId = "L5", CloneName = "Bea B" } });
                    }
                    return g;
                }),
                P("GhostTrio", 0, (t, p) => StoryKit.GhostTrio(t, p, null, new[] { "MARKETING", "SALES", "IT" }), false),
                P("HologramTauntSpot", 0, (t, p) => StoryKit.HologramTauntSpot(t, p, null, "L1.S1.D1"), false),
                P("ExhibitA", 0, (t, p) => StoryKit.ExhibitProp(t, p, "A")),
                P("ExhibitB", 0, (t, p) => StoryKit.ExhibitProp(t, p, "B")),
                P("ExhibitC", 0, (t, p) => StoryKit.ExhibitProp(t, p, "C")),
                P("TallyBoard", 0, (t, p) => StoryKit.TallyBoard(t, p, null)),
                P("LetterBlock", 1, (t, p) => StoryKit.LetterBlock(t, p + Vector3.up, null, "ME", new Vector3(2.4f, 2f, 1f)), true, 40, false, 1),
                P("FoundersStatue", 0, (t, p) => StoryKit.FoundersStatue(t, p, null), false, 200),
                P("CitizenCrowd", 0, (t, p) => StoryKit.CitizenCrowd(t, new Bounds(p + new Vector3(0f, 1f, 2f), new Vector3(6f, 2f, 4f)), 24, 1), false, 1),
                P("ScriptTrigger", 0, (t, p) => StoryKit.ScriptTrigger(t, p + Vector3.up, new Vector3(3f, 2f, 3f), "test.beat").gameObject, false, 0, true),
                P("LineSpot", 0, (t, p) => StoryKit.LineSpot(t, p, 3f, "percy.wink").gameObject, false, 0, true),
                P("JumpScare", 0, (t, p) => ScareKit.JumpScare(t, p + new Vector3(0f, 1f, -3f), new Vector3(3f, 2f, 2f), p, ScareKind.CardboardMax, "gallery.static"), false),
                P("PhoneCall", 1, (t, p) => ScareKit.PhoneCall(t, p, "gallery.phone")),
            };
            foreach (ScareKind k in System.Enum.GetValues(typeof(ScareKind)))
            {
                var kk = k;
                l.Add(P("Popper_" + k, 0, (t, p) => Popper(t, p, kk, false), false));
                l.Add(P("Popper_" + k + "_MILD", 0, (t, p) => Popper(t, p, kk, true), false));
            }
            return l;
        }

        static IEnumerator Pieces(DevPlayContext ctx, Camera cam)
        {
            Kit.Theme = LevelTheme.Training;
            LevelLighting.Apply(LevelTheme.Training);
            var root = new GameObject("StoryKitGallery").transform;
            root.position = Base;
            EnvKit.Floor(root, new Vector3(60f, 0f, 2f), new Vector3(150f, 1f, 24f));
            TeamData team = null;
            try { team = CharacterGenerator.GenerateTeam(1); } catch (System.Exception e) { ctx.Log("team: " + e.Message); }
            var list = PieceList();
            var tiles = new List<GameObject>();
            var built = new List<GameObject>();
            float cursor = 0f;
            int ii = 0;
            foreach (var pc in list)
            {
                var slot = new GameObject("Slot_" + pc.Name).transform;
                slot.SetParent(root, false);
                GameObject go = null;
                try { go = pc.Build(slot, Vector3.zero); } catch (System.Exception e) { ctx.Log("BUILD " + pc.Name + ": " + e); }
                ctx.Check(go != null, "gallery " + pc.Name + ": built");
                if (go == null) continue;
                var b = Bounds(go, out int rcount);
                float w = rcount > 0 ? b.size.x : 2f;
                float minX = rcount > 0 ? b.min.x - slot.position.x : -1f;
                // refs left of the piece: Max (1.2 m) and an intern, facing the camera
                slot.localPosition = new Vector3(cursor + 1.8f - minX, 0f, 0f);
                try { VillainPuppet.Create(root, new Vector3(cursor + 0.4f, 0f, -0.4f), Quaternion.Euler(0f, 180f, 0f)); } catch (System.Exception e) { if (ii == 0) ctx.Log("villain ref: " + e.Message); }
                if (team != null) try { CharacterPuppet.Create(team.Members[ii % 3], root, new Vector3(cursor + 1.15f, 0f, -0.4f), Quaternion.Euler(0f, 180f, 0f)); } catch (System.Exception e) { if (ii == 0) ctx.Log("intern ref: " + e.Message); }
                cursor += 1.8f + w + 1.2f;
                ii++;

                var rs = go.GetComponentsInChildren<Renderer>(true);
                int verts = 0; foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true)) if (mf.sharedMesh != null) verts += mf.sharedMesh.vertexCount;
                int bad = 0; foreach (var r in rs) foreach (var m in r.sharedMaterials) if (m == null || m.shader == null || m.shader.name.Contains("InternalError") || m.name.StartsWith("Default-")) bad++;
                var cols = go.GetComponentsInChildren<Collider>(true);
                int solid = 0; foreach (var cc in cols) if (!cc.isTrigger) solid++;
                int lights = go.GetComponentsInChildren<Light>(true).Length;
                int texts = go.GetComponentsInChildren<TextMeshPro>(true).Length;
                ctx.Log("piece " + pc.Name + ": renderers=" + rs.Length + " texts=" + texts + " colliders=" + cols.Length + " (solid " + solid + ") verts=" + verts + " lights=" + lights + " size=" + b.size.ToString("F2"));
                if (pc.Logic) ctx.Check(rs.Length == 0, "gallery " + pc.Name + ": logic-only (no renderers)");
                else ctx.Check(rs.Length > 0 && rs.Length <= pc.MaxRenderers, "gallery " + pc.Name + ": renderer count " + rs.Length + " <= " + pc.MaxRenderers);
                ctx.Check(bad == 0, "gallery " + pc.Name + ": no missing / default materials");
                ctx.Check(cols.Length == pc.Colliders, "gallery " + pc.Name + ": documented colliders (" + cols.Length + " == " + pc.Colliders + ")");
                ctx.Check(lights == 0, "gallery " + pc.Name + ": light-free");
                ctx.Check(verts < 65000, "gallery " + pc.Name + ": vertex count sane (" + verts + ")");
                if (pc.Name == "LetterBlock") ctx.Check(cols.Length == 1 && solid == 1, "LetterBlock: one static solid box collider");
                if (pc.Name == "PhoneCall") ctx.Check(cols.Length == 1 && solid == 0, "PhoneCall: one interaction trigger");
                if (pc.Front)
                {
                    int wrong = 0, backs = 0;
                    foreach (var tmp in go.GetComponentsInChildren<TextMeshPro>(true))
                    {
                        if (!tmp.gameObject.activeInHierarchy) continue;
                        float d = Vector3.Dot(tmp.transform.forward, Vector3.forward);
                        if (d < -0.9f) backs++;   // documented back-side text (graffiti / double-sided block), readable from behind
                        else if (d < 0.9f) { wrong++; ctx.Log("  text '" + SceneAudit.CleanText(tmp.text, 30) + "' fwd " + tmp.transform.forward.ToString("F2")); }
                    }
                    ctx.Check(wrong == 0 && backs == pc.Backs, "gallery " + pc.Name + ": texts readable from the viewer (rot = identity -> -Z side; " + backs + " documented back texts)");
                }
                if (!pc.Logic) { tiles.Add(slot.gameObject); built.Add(go); }
            }
            yield return ctx.Wait(1.2f);   // Start(): Founders puppets, tombstone fill, MeTv ticker
            // founders statue: gold puppets of the current team (needs GameSession.Team)
            var fs = root.GetComponentInChildren<FoundersStatueView>(true);
            if (fs != null) ctx.Log("FoundersStatue renderers after Start: " + fs.GetComponentsInChildren<Renderer>(true).Length + " (team " + (GameSession.Instance != null && GameSession.Instance.Team != null) + ")");
            var tr = root.GetComponentInChildren<TombstoneRowView>(true);
            if (tr != null && tr.Texts != null && tr.Texts.Length >= 3)
                ctx.Check(tr.Texts[0].text.Contains("BEA") && tr.Texts[1].text.Contains("GARY") && tr.Texts[2].text.Contains("RESERVED"), "TombstoneRow: filled newest first, rest RESERVED");

            // overview + contact sheets (4 x 3) from the front, slightly above and to the side
            var dir = new Vector3(0.22f, 0.18f, -1f);
            Pose(cam, Base + new Vector3(cursor * 0.5f, 6f, -cursor * 0.55f), Base + new Vector3(cursor * 0.5f, 1f, 0f), 50f);
            ctx.Shot("storykit_overview", cam);
            for (int s = 0; s * 12 < tiles.Count; s++)
                Sheet(ctx, cam, "storykit_sheet" + (s + 1), tiles.GetRange(s * 12, Mathf.Min(12, tiles.Count - s * 12)), dir, 4, 3);
            // night look for the park / scare pieces
            LevelLighting.Apply(LevelTheme.Night);
            yield return ctx.Wait(0.3f);
            var night = new List<GameObject>();
            foreach (var t in tiles) if (t.name.Contains("Tomb") || t.name.Contains("Ghost") || t.name.Contains("Popper") || t.name.Contains("Phone") || t.name.Contains("Statue")) night.Add(t);
            for (int s = 0; s * 12 < night.Count; s++)
                Sheet(ctx, cam, "storykit_night" + (s + 1), night.GetRange(s * 12, Mathf.Min(12, night.Count - s * 12)), dir, 4, 3);
            LevelLighting.Apply(LevelTheme.Training);
            Object.Destroy(root.gameObject);
            yield return null;
        }

        // ------------------------------------------------------------------ 3. live director tests
        static IEnumerator DirectorTests(DevPlayContext ctx, Camera cam)
        {
            var root = new GameObject("StoryKitScareLive").transform;
            root.position = Base + new Vector3(0f, 0f, 60f);
            EnvKit.Floor(root, Vector3.zero, new Vector3(30f, 1f, 30f));
            ScareDirector.MildOverride = false;
            ScareDirector.Ensure();
            ScareDirector.ResetAttempt();
            yield return null;

            // automatic windows (boss / cutscene / timer / hazard)
            GameEvents.RaiseBossChanged(new BossStatus { Visible = true, State = BossState.Fighting });
            ctx.Check(!ScareDirector.CanPlay("w1") && ScareDirector.LastVerdict == ScareVerdict.Blocked, "director: blocked during a boss fight");
            GameEvents.RaiseBossChanged(new BossStatus { Visible = true, State = BossState.Defeated });
            ctx.Check(ScareDirector.CanPlay("w1"), "director: boss defeated -> ok");
            GameEvents.RaiseCutsceneChanged(true);
            ctx.Check(!ScareDirector.CanPlay("w1"), "director: blocked during a blocking cutscene");
            GameEvents.RaiseCutsceneChanged(false);
            GameEvents.RaiseTimer(8f);
            ctx.Check(!ScareDirector.CanPlay("w1"), "director: blocked during a timed window");
            GameEvents.RaiseTimer(0f);
            GameEvents.RaiseCharacterDamaged(null, 10f);
            ctx.Check(!ScareDirector.CanPlay("w1") && ScareDirector.IsBlocked(out var by) && by == "hazard", "director: blocked 3 s after a hazard hit");
            ScareDirector.ResetAttempt();
            ctx.Check(ScareDirector.CanPlay("w1"), "director: ResetAttempt clears windows");

            // jump scare + stalker freeze + punchline timing
            StalkerBrain stalker = null;
            try { stalker = EnemyKit.Stalker(root, new Vector3(6f, 0f, 6f), Quaternion.identity, "storykit.test", root.position, 8f, StalkerSkin.TeamBuildingDummy); } catch (System.Exception e) { ctx.Log("stalker: " + e.Message); }
            yield return ctx.Wait(0.3f);
            var js = ScareKit.JumpScare(root, new Vector3(0f, 1f, -3f), new Vector3(3f, 2f, 2f), new Vector3(0f, 0f, 1.5f), ScareKind.CardboardMax, "storykit.jump");
            var jv = js.GetComponent<JumpScareView>();
            yield return null;
            bool started = jv != null && jv.Trigger();
            float t0 = Time.time;
            ctx.Check(started, "jump scare: Trigger() plays");
            ctx.Check(stalker == null || stalker.IsFrozen, "director: stalkers frozen after a scripted scare (" + ScareDirector.StalkersFrozenLast + ")");
            float punch = -1f;
            while (Time.time - t0 < 4f) { if (punch < 0f && jv.PunchText != null && jv.PunchText.gameObject.activeInHierarchy) punch = Time.time - t0; if (punch >= 0f) break; yield return null; }
            ctx.Check(punch >= 0f && punch <= ScareRules.PunchlineMaxSeconds, "jump scare: punchline at " + punch.ToString("F2") + " s (<= 3 s)");
            yield return ctx.Wait(0.4f);
            Pose(cam, root.position + new Vector3(0.8f, 1.6f, -3.4f), root.position + new Vector3(0f, 1.2f, 1.5f), 60f);
            ctx.Shot("storykit_jump_punchline", cam);
            ctx.Check(!jv.Trigger() && ScareDirector.HasSeen("storykit.jump"), "jump scare: once per attempt");
            ctx.Check(!ScareDirector.CanPlay("other") && ScareDirector.LastVerdict == ScareVerdict.Cooldown, "director: second scare inside 30 s refused");
            if (stalker != null) { yield return ctx.Wait(2.3f); ctx.Check(!stalker.IsFrozen, "director: stalker freeze ends after ~2 s"); }

            // attempt memory: seen ids survive a checkpoint restart, a new level resets them
            LevelAttempt.Close();
            LevelAttempt.Begin(77, false, out _, out _);
            ScareDirector.ResetAttempt();
            ctx.Check(ScareDirector.TryPlay("storykit.attempt"), "attempt: cue plays in attempt 1");
            LevelAttempt.Begin(77, true, out _, out _);   // checkpoint restart of the same attempt
            ctx.Check(ScareDirector.HasSeen("storykit.attempt"), "attempt: seen id survives a checkpoint restart");
            LevelAttempt.Close();
            LevelAttempt.Begin(78, false, out _, out _);
            ctx.Check(!ScareDirector.HasSeen("storykit.attempt"), "attempt: another level = fresh memory");
            LevelAttempt.Close();

            // flicker reveal
            ScareDirector.ResetAttempt();
            var fr = ScareKit.FlickerReveal(root, new[] { new Vector3(-3f, 0f, 9f), new Vector3(-2f, 0f, 6f), new Vector3(-1f, 0f, 3.5f), new Vector3(0f, 0f, 2f) }, 0.5f, "storykit.flicker");
            var fv = fr.GetComponent<FlickerRevealView>();
            yield return null;
            ctx.Check(fv != null && fv.Play(), "flicker reveal: Play()");
            float ft0 = Time.time;
            while (fv != null && fv.State < 2 && Time.time - ft0 < 5f) yield return null;
            ctx.Check(fv != null && fv.PunchlineAt > 0f && fv.PunchlineAt - fv.StartedAt <= ScareRules.PunchlineMaxSeconds, "flicker reveal: punchline within 3 s (" + (fv != null ? (fv.PunchlineAt - fv.StartedAt).ToString("F2") : "-") + " s, steps " + (fv != null ? fv.StepsPlayed : 0) + ")");
            yield return ctx.Wait(0.3f);
            Pose(cam, root.position + new Vector3(0f, 1.7f, -2.5f), root.position + new Vector3(0f, 1.2f, 3f), 60f);
            ctx.Shot("storykit_flicker_punchline", cam);

            // phone call
            ScareDirector.ResetAttempt();
            var ph = ScareKit.PhoneCall(root, new Vector3(4f, 0f, 1f), "storykit.phone");
            var pv = ph.GetComponent<PhoneCallView>();
            yield return null;
            if (pv != null) pv.Ring();
            yield return ctx.Wait(0.5f);
            ctx.Check(pv != null && pv.State == 1 && pv.CanInteract(null, out _), "phone call: rings and is interactable");
            Pose(cam, root.position + new Vector3(4f, 1.6f, -1.6f), root.position + new Vector3(4f, 0.9f, 1f), 55f);
            ctx.Shot("storykit_phone_ringing", cam);

            // blackout normal vs MILD (CAMPAIGN 10.1.7)
            Pose(cam, root.position + new Vector3(0f, 2f, -6f), root.position + new Vector3(0f, 1f, 2f), 60f);
            ScareKit.Blackout(1.2f);
            yield return ctx.Wait(0.3f);
            float veilNormal = ScareBlackoutDriver.VeilAlpha;
            ctx.Check(ScareBlackoutDriver.Active, "blackout: active (override via rendering: " + ScareKit.LastBlackoutUsedOverride + ")");
            ctx.Shot("storykit_blackout", cam);
            yield return ctx.Wait(1.5f);
            ctx.Check(!ScareBlackoutDriver.Active, "blackout: restores itself");
            ScareDirector.MildOverride = true;
            ScareKit.Blackout(1.2f);
            yield return ctx.Wait(0.3f);
            float veilMild = ScareBlackoutDriver.VeilAlpha;
            ctx.Check(veilMild < veilNormal, "blackout MILD is brighter (veil " + veilMild.ToString("F2") + " < " + veilNormal.ToString("F2") + ")");
            ctx.Shot("storykit_blackout_mild", cam);
            yield return ctx.Wait(1.5f);
            ScareDirector.MildOverride = null;
            ScareDirector.ResetAttempt();
            Object.Destroy(root.gameObject);
            yield return null;
        }

        // ------------------------------------------------------------------ shot helpers (shared with the overlay tests)
        public static Bounds Bounds(GameObject go, out int count)
        {
            count = 0;
            var b = new Bounds(go.transform.position, Vector3.zero);
            foreach (var r in go.GetComponentsInChildren<Renderer>(false))
            {
                if (!r.enabled) continue;
                if (count == 0) b = r.bounds; else b.Encapsulate(r.bounds);
                count++;
            }
            return b;
        }

        public static void Pose(Camera cam, Vector3 pos, Vector3 look, float fov)
        {
            cam.transform.position = pos;
            cam.transform.rotation = Quaternion.LookRotation((look - pos).normalized, Vector3.up);
            cam.fieldOfView = fov;
        }

        /// <summary>Frames 'go' (renderer bounds) from direction 'dir' (from the piece toward the camera).</summary>
        public static void Frame(Camera cam, GameObject go, Vector3 dir, float minDist = 1.6f, float maxDist = 120f)
        {
            var b = Bounds(go, out int n);
            if (n == 0) b = new Bounds(go.transform.position + Vector3.up, Vector3.one * 2f);
            float rad = Mathf.Max(0.4f, b.extents.magnitude);
            float fov = 45f;
            float d = Mathf.Clamp(rad / Mathf.Sin(fov * 0.5f * Mathf.Deg2Rad) * 0.82f, minDist, maxDist);
            Pose(cam, b.center + dir.normalized * d, b.center, fov);
        }

        /// <summary>Contact sheet: each object framed in its own tile (row-major, top-left first), one PNG.</summary>
        public static string Sheet(DevPlayContext ctx, Camera cam, string name, List<GameObject> gos, Vector3 dir, int cols, int rows, List<Vector3> dirs = null)
        {
            if (cam == null || string.IsNullOrEmpty(ctx.Config.shotsDir) || gos.Count == 0) return null;
            var prevPos = cam.transform.position; var prevRot = cam.transform.rotation; var prevRect = cam.rect; float prevFov = cam.fieldOfView;
            var prevTarget = cam.targetTexture;
            const int W = 1920, H = 1080;
            var rt = RenderTexture.GetTemporary(W, H, 24, RenderTextureFormat.ARGB32);
            string order = "";
            try
            {
                var prevActive = RenderTexture.active;
                RenderTexture.active = rt; GL.Clear(true, true, Color.black); RenderTexture.active = prevActive;
                cam.targetTexture = rt;
                for (int i = 0; i < gos.Count && i < cols * rows; i++)
                {
                    if (gos[i] == null) continue;
                    int c = i % cols, r = i / cols;
                    cam.rect = new Rect((float)c / cols, 1f - (float)(r + 1) / rows, 1f / cols, 1f / rows);
                    Frame(cam, gos[i], dirs != null && i < dirs.Count ? dirs[i] : dir);
                    cam.Render();
                    order += (order.Length > 0 ? " | " : "") + gos[i].name;
                }
                cam.rect = new Rect(0f, 0f, 1f, 1f);
                var active = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
                tex.Apply();
                RenderTexture.active = active;
                Directory.CreateDirectory(ctx.Config.shotsDir);
                string path = Path.Combine(ctx.Config.shotsDir, name + ".png");
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.Destroy(tex);
                ctx.Result.shots.Add(path);
                ctx.Log("sheet " + name + ": " + order);
                return path;
            }
            catch (System.Exception e) { ctx.Log("sheet " + name + " failed: " + e.Message); return null; }
            finally
            {
                cam.targetTexture = prevTarget; cam.rect = prevRect;
                cam.transform.SetPositionAndRotation(prevPos, prevRot); cam.fieldOfView = prevFov;
                RenderTexture.ReleaseTemporary(rt);
            }
        }
    }
}
