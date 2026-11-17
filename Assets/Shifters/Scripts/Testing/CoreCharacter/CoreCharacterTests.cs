// =====================================================================================
//  SHIFTERS - CoreCharacterTests.cs   [CORE_CHARACTER DevPlay scenarios]
//  devplay.sh <clone> core_all -devSetup Shifters.CoreCharacterTests.RunAll
//  Builds test courses in code, spawns teams via CharacterManager, drives characters through
//  ExternalControl + Motor.SetCommand and asserts movement, states, switching, AI and camera.
// =====================================================================================
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Shifters
{
    public static class CoreCharacterTests
    {
        static DevPlayContext ctx;
        static readonly List<string> Speech = new List<string>();
        static readonly List<string> Banners = new List<string>();
        static int _wipes;

        public static IEnumerator RunAll(DevPlayContext c)
        {
            ctx = c;
            GameEvents.Speech += (ch, t) => Speech.Add(t);
            GameEvents.Banner += (t, s, st) => Banners.Add(t);
            GameEvents.TeamWiped += () => _wipes++;
            var cam = Camera.main;
            if (cam.GetComponent<ShiftersCamera>() == null) cam.gameObject.AddComponent<ShiftersCamera>();
            var mgrGo = new GameObject("[TestCharacterManager]");
            var mgr = mgrGo.AddComponent<CharacterManager>();
            mgrGo.AddComponent<CharacterSwitcher>();
            ctx.Check(ShiftersCamera.Instance != null && FindListenerCount() == 1, "camera has exactly one AudioListener");
            ctx.Check(cam.nearClipPlane <= 0.1001f && cam.farClipPlane >= 499f, "camera near/far configured");

            yield return Movement(mgr);
            yield return Team(mgr);
            yield return FollowAI(mgr);
            ctx.Log("core character tests finished");
        }

        static int FindListenerCount() { int n = 0; foreach (var l in Object.FindObjectsByType<AudioListener>()) if (l.enabled) n++; return n; }

        // ------------------------------------------------------------------ helpers
        static BoxCollider Box(string n, Vector3 center, Vector3 size, MatKey mat = MatKey.Floor) => Kit.SolidBox(null, n, center, size, mat);

        static ShifterCharacter Find(CharacterManager mgr, Archetype a)
        {
            foreach (var ch in mgr.Team) if (ch.Archetype == a) return ch;
            return null;
        }

        static void Drive(ShifterCharacter ch, Vector3 move, bool sprint = false, bool jump = false, bool hold = false)
            => ch.Motor.SetCommand(new CharacterMotor.Command { Move = move, Sprint = sprint, Jump = jump, JumpHeld = hold || jump });

        static void Stop(ShifterCharacter ch) => ch.Motor.SetCommand(default);

        static IEnumerator WaitUntil(System.Func<bool> cond, float maxSeconds)
        {
            int frames = Mathf.RoundToInt(maxSeconds * 60f);
            for (int i = 0; i < frames && !cond(); i++) yield return null;
        }

        static List<Pose> Poses(params Vector3[] p)
        {
            var l = new List<Pose>();
            foreach (var v in p) l.Add(new Pose(v, Quaternion.identity));
            return l;
        }

        static float Planar(Vector3 v) { v.y = 0f; return v.magnitude; }

        // ------------------------------------------------------------------ movement
        static IEnumerator Movement(CharacterManager mgr)
        {
            Box("SpeedFloor", new Vector3(0f, -0.5f, 30f), new Vector3(20f, 1f, 90f));
            mgr.SpawnTeam(CharacterGenerator.GenerateTeam(777), Poses(new Vector3(-1.6f, 0f, -10f), new Vector3(0f, 0f, -10f), new Vector3(1.6f, 0f, -10f)));
            foreach (var ch in mgr.Team) ch.ExternalControl = true;
            var brute = Find(mgr, Archetype.Brute); var sprinter = Find(mgr, Archetype.Sprinter); var brain = Find(mgr, Archetype.Brainiac);
            ctx.Check(mgr.Team.Count == 3 && brute != null && sprinter != null && brain != null, "team spawned with one of each archetype");
            ctx.Check(brute.Capsule.sharedMaterial == CharacterMotor.FrictionlessMaterial, "capsule has the frictionless material");
            ShiftersCamera.Instance.SetTarget(mgr.Active, true);
            yield return ctx.Wait(0.6f);
            ctx.Check(brute.Motor.IsGrounded && sprinter.Motor.IsGrounded && brain.Motor.IsGrounded, "all grounded after spawn");
            ctx.Shot("team_lineup");

            // spread into lanes for speed runs
            brute.Motor.Teleport(new Vector3(-6f, 0f, -12f), Quaternion.identity);
            sprinter.Motor.Teleport(new Vector3(0f, 0f, -12f), Quaternion.identity);
            brain.Motor.Teleport(new Vector3(6f, 0f, -12f), Quaternion.identity);
            yield return ctx.Wait(0.2f);
            foreach (var sprint in new[] { false, true })
            {
                foreach (var ch in mgr.Team) Drive(ch, Vector3.forward, sprint);
                yield return ctx.Wait(1.0f);
                var sums = new float[3];
                int samples = 0;
                for (int i = 0; i < 24; i++) { for (int k = 0; k < 3; k++) sums[k] += Planar(mgr.Team[k].Motor.Velocity); samples++; yield return null; }
                for (int k = 0; k < 3; k++)
                {
                    var ch = mgr.Team[k];
                    float want = sprint ? ch.Motor.SprintSpeed : ch.Motor.WalkSpeed;
                    float got = sums[k] / samples;
                    ctx.Check(Mathf.Abs(got - want) <= want * 0.1f, $"{ch.Archetype} {(sprint ? "sprint" : "walk")} speed {got:0.00} ~ {want:0.00}");
                }
            }
            foreach (var ch in mgr.Team) Stop(ch);
            yield return ctx.Wait(0.6f);

            // standing jump apex
            float y0 = brain.Feet.y, maxY = y0;
            Drive(brain, Vector3.zero, false, true, true);
            yield return null;
            Drive(brain, Vector3.zero, false, false, true);
            for (int i = 0; i < 70; i++) { maxY = Mathf.Max(maxY, brain.Feet.y); yield return null; }
            Stop(brain);
            ctx.Check(Mathf.Abs(maxY - y0 - Tuning.JumpApexHeight) <= 0.15f, $"standing jump apex {maxY - y0:0.00} ~ {Tuning.JumpApexHeight}");
            yield return ctx.Wait(0.3f);
            ctx.Check(brain.Motor.IsGrounded, "landed after jump");

            yield return Ledges(mgr, brute, sprinter, brain);
            yield return Gap(mgr, brute, sprinter);
            yield return Platform(brain);
            yield return Stack(brute, brain);
            yield return TumbleAndStates(mgr, brute, sprinter, brain);
        }

        static IEnumerator Ledges(CharacterManager mgr, ShifterCharacter brute, ShifterCharacter sprinter, ShifterCharacter brain)
        {
            Box("LedgeFloor", new Vector3(30f, -0.5f, 5f), new Vector3(16f, 1f, 30f));
            Box("Ledge1.2", new Vector3(26f, 0.6f, 10f), new Vector3(3f, 1.2f, 4f), MatKey.Concrete);
            Box("Ledge2.4", new Vector3(34f, 1.2f, 10f), new Vector3(3f, 2.4f, 4f), MatKey.Concrete);
            brain.Motor.Teleport(new Vector3(26f, 0f, 0f), Quaternion.identity);
            sprinter.Motor.Teleport(new Vector3(34f, 0f, 0f), Quaternion.identity);
            yield return ctx.Wait(0.3f);
            var runners = new[] { brain, sprinter };
            var jumped = new bool[2];
            for (int f = 0; f < 150; f++)
            {
                for (int k = 0; k < 2; k++)
                {
                    var ch = runners[k];
                    bool doJump = !jumped[k] && 8f - (ch.Feet.z + ch.Radius) <= 1.2f;
                    if (doJump) jumped[k] = true;
                    if (ch.Feet.z > 9.2f) Stop(ch); else Drive(ch, Vector3.forward, true, doJump, true);
                }
                yield return null;
            }
            Stop(brain); Stop(sprinter);
            yield return ctx.Wait(0.4f);
            ctx.Check(Mathf.Abs(brain.Feet.y - 1.2f) < 0.15f && brain.Feet.z > 8f, $"1.2 m ledge climbable with run+jump (feet y={brain.Feet.y:0.00} z={brain.Feet.z:0.0})");
            ctx.Check(sprinter.Feet.y < 0.3f, $"2.4 m ledge NOT climbable (feet y={sprinter.Feet.y:0.00})");
        }

        static IEnumerator Gap(CharacterManager mgr, ShifterCharacter brute, ShifterCharacter sprinter)
        {
            Box("GapA", new Vector3(52f, -0.5f, 0f), new Vector3(12f, 1f, 20f));        // z -10..10
            Box("GapB", new Vector3(52f, -0.5f, 23f), new Vector3(12f, 1f, 15f));       // z 15.5..30.5
            Box("GapPit", new Vector3(52f, -6.5f, 12f), new Vector3(12f, 1f, 12f), MatKey.MetalDark);
            sprinter.Motor.Teleport(new Vector3(49f, 0f, -8f), Quaternion.identity);
            brute.Motor.Teleport(new Vector3(55f, 0f, -8f), Quaternion.identity);
            yield return ctx.Wait(0.3f);
            var runners = new[] { sprinter, brute };
            var jumped = new bool[2];
            for (int f = 0; f < 260; f++)
            {
                for (int k = 0; k < 2; k++)
                {
                    var ch = runners[k];
                    bool doJump = !jumped[k] && ch.Feet.z + ch.Radius >= 9.85f;
                    if (doJump) jumped[k] = true;
                    if (jumped[k] && (ch.Feet.z > 17f || ch.Feet.y < -1f)) Stop(ch);
                    else Drive(ch, Vector3.forward, true, doJump, true);
                }
                yield return null;
            }
            Stop(sprinter); Stop(brute);
            yield return ctx.Wait(0.3f);
            ctx.Check(sprinter.Feet.y > -0.3f && sprinter.Feet.z > 15.5f, $"turbo Sprinter crosses a 5.5 m gap (y={sprinter.Feet.y:0.0} z={sprinter.Feet.z:0.0}, jump {sprinter.Motor.MaxJumpDistance:0.0} m)");
            ctx.Check(brute.Feet.y < -3f, $"Brute cannot cross a 5.5 m gap (y={brute.Feet.y:0.0}, jump {brute.Motor.MaxJumpDistance:0.0} m)");
            ctx.Check(brute.IsAlive, "pit fall leaves the Brute alive (" + brute.Health.ToString("0") + " hp)");
        }

        static IEnumerator Platform(ShifterCharacter rider)
        {
            var go = new GameObject("MovingPlatform");
            go.transform.position = new Vector3(70f, 0.5f, 0f);
            var rb = go.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.interpolation = RigidbodyInterpolation.Interpolate;
            go.AddComponent<BoxCollider>().size = new Vector3(3.5f, 0.4f, 3.5f);
            ProcMesh.AddRenderer(go, ProcMesh.CachedBox(new Vector3(3.5f, 0.4f, 3.5f), 0.05f), Kit.Mat(MatKey.PuzzleBody));
            var mover = go.AddComponent<TestPlatformMover>();
            mover.Amplitude = new Vector3(3f, 1f, 0f);
            mover.Period = 4f;
            rider.Motor.Teleport(new Vector3(70f, 0.75f, 0f), Quaternion.identity);
            yield return ctx.Wait(0.6f);
            Vector3 off0 = rider.Feet - rb.position;
            float maxDrift = 0f;
            for (int i = 0; i < 180; i++) { maxDrift = Mathf.Max(maxDrift, Planar(rider.Feet - rb.position - off0)); yield return null; }
            float dy = rider.Feet.y - (rb.position.y + 0.2f);
            ctx.Check(maxDrift < 0.3f && Mathf.Abs(dy) < 0.12f && rider.Motor.GroundBody == rb, $"rides a moving platform (drift {maxDrift:0.00} m, dy {dy:0.00})");
            Object.Destroy(go);
            rider.Motor.Teleport(new Vector3(80f, 0f, 0f), Quaternion.identity);
        }

        static IEnumerator Stack(ShifterCharacter bottom, ShifterCharacter top)
        {
            Box("StackFloor", new Vector3(80f, -0.5f, 8f), new Vector3(10f, 1f, 30f));
            bottom.Motor.Teleport(new Vector3(80f, 0f, 0f), Quaternion.identity);
            top.Motor.Teleport(new Vector3(80f, bottom.Height + 0.05f, 0f), Quaternion.identity);
            ShiftersCamera.Instance.SetTarget(bottom, true);
            yield return ctx.Wait(0.5f);
            Vector3 off0 = top.Feet - bottom.Feet;
            float drift = 0f;
            for (int i = 0; i < 180; i++) { drift = Mathf.Max(drift, Planar(top.Feet - bottom.Feet - off0)); yield return null; }
            ctx.Check(top.Motor.StandingOn == bottom && drift < 0.1f && Mathf.Abs(off0.y - bottom.Height) < 0.1f, $"stacked on a teammate's head for 3 s (drift {drift:0.000}, dy {off0.y:0.00})");
            Drive(bottom, Vector3.forward * 0.7f);
            float maxRel = 0f;
            for (int i = 0; i < 150; i++) { maxRel = Mathf.Max(maxRel, Planar(top.Feet - bottom.Feet)); yield return null; }
            ctx.Shot("stacking");
            Stop(bottom);
            yield return ctx.Wait(0.5f);
            ctx.Check(top.Motor.StandingOn == bottom && maxRel < 0.35f && bottom.Feet.z > 3f, $"totem: top rides along while bottom walks (max offset {maxRel:0.00}, bottom z {bottom.Feet.z:0.0})");
        }

        static IEnumerator TumbleAndStates(CharacterManager mgr, ShifterCharacter brute, ShifterCharacter sprinter, ShifterCharacter brain)
        {
            Box("StateFloor", new Vector3(100f, -0.5f, 5f), new Vector3(16f, 1f, 30f));
            sprinter.Motor.Teleport(new Vector3(98f, 0f, 0f), Quaternion.identity);
            brain.Motor.Teleport(new Vector3(103f, 0f, 0f), Quaternion.identity);
            ShiftersCamera.Instance.SetTarget(sprinter, true);
            yield return ctx.Wait(0.4f);
            bool gotUp = false;
            sprinter.Motor.GotUp += () => gotUp = true;
            sprinter.Motor.AddImpulse(new Vector3(0f, 7f, 7f), true, 0.5f);
            yield return ctx.Wait(0.05f);
            ctx.Check(sprinter.Motor.IsTumbling && sprinter.Condition == CharacterCondition.Stunned, "AddImpulse(tumble) -> tumbling + Stunned");
            yield return ctx.Wait(0.3f);
            ctx.Shot("tumble");
            yield return WaitUntil(() => gotUp && !sprinter.Motor.IsGettingUp, 7f);
            yield return ctx.Wait(0.1f);
            float tilt = Vector3.Angle(sprinter.transform.up, Vector3.up);
            ctx.Check(gotUp && !sprinter.Motor.IsTumbling && sprinter.Condition == CharacterCondition.Normal && tilt < 2f, $"got back up after tumble (Condition {sprinter.Condition}, tilt {tilt:0.0})");

            // non-tumble stun
            sprinter.Motor.AddImpulse(new Vector3(3f, 0f, 0f), false, 0.5f);
            yield return ctx.Wait(0.1f);
            ctx.Check(sprinter.Condition == CharacterCondition.Stunned && !sprinter.Motor.IsTumbling, "small impulse with stun -> Stunned without tumble");
            yield return ctx.Wait(0.6f);
            ctx.Check(sprinter.Condition == CharacterCondition.Normal, "stun wears off -> Normal");

            // KO -> limp, revive -> stands
            brain.KnockOut("a test anvil");
            yield return ctx.Wait(0.1f);
            ctx.Check(brain.State == CharacterState.Downed && brain.Motor.IsTumbling, "KnockOut -> Downed + limp");
            yield return ctx.Wait(3f);
            ctx.Check(brain.State == CharacterState.Downed && brain.Motor.IsTumbling, "Downed stays limp (no auto get-up)");
            brain.Revive();
            yield return ctx.Wait(0.3f);
            float bt = Vector3.Angle(brain.transform.up, Vector3.up);
            ctx.Check(brain.IsAlive && !brain.Motor.IsTumbling && bt < 2f && Mathf.Approximately(brain.Health, brain.MaxHealth) && brain.CanAct, $"Revive -> stands, full health (tilt {bt:0.0})");

            // fall damage: drop from 8 m
            brain.Motor.Teleport(new Vector3(103f, 8f, 4f), Quaternion.identity);
            float hp0 = brain.Health;
            yield return ctx.Wait(1.2f);
            ctx.Check(brain.Health < hp0 && brain.IsAlive, $"fall damage from 8 m ({hp0 - brain.Health:0} hp)");

            // Lost -> hidden after ~1.5 s
            brain.Lose("a test void");
            yield return ctx.Wait(0.5f);
            ctx.Check(!brain.IsHidden, "Lost: still visible during the 1.5 s beat");
            yield return ctx.Wait(1.3f);
            bool rendersOff = true; foreach (var r in brain.GetComponentsInChildren<Renderer>()) if (r.enabled) rendersOff = false;
            ctx.Check(brain.IsHidden && brain.Body.isKinematic && !brain.Capsule.enabled && rendersOff, "Lost -> hidden, colliders off, kinematic");
            brain.Revive();
            ctx.Check(brain.State == CharacterState.Lost, "Lost is permanent (Revive refused)");
        }

        // ------------------------------------------------------------------ switching + camera
        static IEnumerator Team(CharacterManager mgr)
        {
            Box("TeamFloor", new Vector3(-40f, -0.5f, 15f), new Vector3(20f, 1f, 50f));
            mgr.SpawnTeam(CharacterGenerator.GenerateTeam(4242), Poses(new Vector3(-40f, 0f, 0f), new Vector3(-36.5f, 0f, 0f), new Vector3(-40f, 0f, 30f)), 0);
            yield return null; // old team destroyed
            foreach (var ch in mgr.Team) ch.ExternalControl = true;
            var s0 = mgr.Get(0); var s1 = mgr.Get(1); var s2 = mgr.Get(2);
            yield return ctx.Wait(0.4f);
            ctx.Check(mgr.Active == s0 && s0.IsActive && s0.Brain.enabled && !s0.AI.enabled && !s1.Brain.enabled && s1.AI.enabled, "spawn activates slot 0 (brain on, AI off; others AI)");
            Vector3 p0 = s0.Feet;

            // short glide
            ctx.Check(mgr.SwitchTo(1), "SwitchTo(1) accepted");
            ctx.Check(mgr.IsTransitioning && !s1.IsActive, "control is withheld during the camera transition");
            yield return WaitUntil(() => !mgr.IsTransitioning, 2f);
            ctx.Check(mgr.Active == s1 && s1.IsActive && s1.Brain.enabled && !s1.AI.enabled && s0.AI.enabled && !s0.Brain.enabled, "SwitchTo changes Active and brain/AI");
            ctx.Check(!ShiftersCamera.Instance.LastTransitionWasSky, "short switch uses the glide");
            ctx.Check((s0.Feet - p0).magnitude < 0.05f, "previous character stays where it was");
            ctx.Check(Speech.Count > 0, "switched-in character says a quip: " + (Speech.Count > 0 ? Speech[Speech.Count - 1] : "-"));

            // long sky transition
            float took = 0f;
            ctx.Check(mgr.SwitchTo(2), "SwitchTo(2) accepted (30 m away)");
            bool shot = false;
            while (mgr.IsTransitioning && took < 3f)
            {
                if (!shot && took >= 0.55f) { shot = true; ctx.Shot("midswitch_sky"); }
                yield return null;
                took += ShiftersCamera.UnscaledDeltaTime;
            }
            ctx.Check(mgr.Active == s2 && s2.IsActive && ShiftersCamera.Instance.LastTransitionWasSky && took <= 1.3f, $"long-distance sky transition completes in {took:0.00} s (<= 1.3)");
            yield return ctx.Wait(0.3f);

            // camera collision: wall between camera and target
            var camT = ShiftersCamera.Instance.transform;
            Vector3 pivot = ShiftersCamera.PivotOf(s2);
            Vector3 back = -ShiftersCamera.Instance.FlatForward;
            var wall = Box("CamWall", pivot + back * 2.2f, new Vector3(8f, 8f, 0.4f), MatKey.Wall);
            wall.transform.rotation = Quaternion.LookRotation(back);
            yield return ctx.Wait(0.4f);
            bool blocked = Physics.Linecast(camT.position, ShiftersCamera.PivotOf(s2), out var lh, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) && lh.collider == wall;
            ctx.Check(!blocked && ShiftersCamera.Instance.CurrentDistance < 2.2f, $"camera does not clip through a wall (dist {ShiftersCamera.Instance.CurrentDistance:0.00})");
            ctx.Shot("camera_wall");
            Object.Destroy(wall.gameObject);
            yield return ctx.Wait(0.5f);
            ctx.Check(ShiftersCamera.Instance.CurrentDistance > 4.5f, $"camera eases back out after the wall is gone ({ShiftersCamera.Instance.CurrentDistance:0.00})");

            // refuse Downed
            Sfx.Play(SfxId.None);
            s0.KnockOut("test");
            yield return ctx.Wait(0.1f);
            ctx.Check(!mgr.SwitchTo(0) && mgr.Active == s2, "switching to a Downed character is refused");
            ctx.Check(Banners.Exists(b => b.Contains("KNOCKED OUT")), "banner for an inactive KO");
            s0.Revive();

            // switcher API (logic; the stub input cannot press keys)
            var sw = CharacterSwitcher.Instance;
            sw.Open();
            ctx.Check(sw.IsOpen && sw.Highlighted == mgr.ActiveIndex && ShiftersInput.Context == InputContext.Switcher && TimeController.IsSlowMo, "switcher opens: slow-mo, Switcher context, highlight = active");
            sw.Highlight(1);
            sw.Close(true);
            ctx.Check(!sw.IsOpen && ShiftersInput.Context == InputContext.Gameplay && !TimeController.IsSlowMo, "switcher closes and restores time/context");
            yield return WaitUntil(() => !mgr.IsTransitioning, 2f);
            ctx.Check(mgr.Active == s1, "confirming a highlighted slot switches");

            // auto-switch after the active is KO'd
            s1.KnockOut("a test laser");
            float k0 = Time.time;
            yield return ctx.Wait(1.0f);
            ctx.Check(mgr.Active == s1, "1.2 s dramatic beat before auto-switch");
            yield return WaitUntil(() => mgr.Active != s1 && !mgr.IsTransitioning, 3f);
            ctx.Check(mgr.Active != s1 && mgr.Active.IsSelectable && mgr.Active.IsActive, $"auto-switch after the active is KO'd ({Time.time - k0:0.0} s)");

            // team wipe
            foreach (var ch in mgr.Team) if (ch.IsAlive) ch.KnockOut("test wipe");
            yield return ctx.Wait(1.6f);
            ctx.Check(_wipes == 1 && mgr.IsTeamWiped, "TeamWiped raised once when nobody is available");
            foreach (var ch in mgr.Team) ch.Revive();
            yield return ctx.Wait(0.5f);
            ctx.Check(mgr.Active != null && mgr.Active.IsSelectable && mgr.Active.IsActive && !mgr.IsTeamWiped, "revive after wipe gives control back");
        }

        // ------------------------------------------------------------------ follow AI
        static IEnumerator FollowAI(CharacterManager mgr)
        {
            float x = -80f;
            Box("FollowA", new Vector3(x, -0.5f, 1.5f), new Vector3(8f, 1f, 13f));           // z -5..8, top 0
            Box("FollowB", new Vector3(x, -0.35f, 11.5f), new Vector3(8f, 1.3f, 7f));         // z 8..15, top 0.3 (step)
            Box("FollowC", new Vector3(x, -0.35f, 28f), new Vector3(8f, 1.3f, 14f));          // z 21..35 (6 m gap)
            Box("FollowPit", new Vector3(x, -8.5f, 18f), new Vector3(8f, 1f, 8f), MatKey.MetalDark);
            var team = CharacterGenerator.GenerateTeam(99);
            int leadSlot = 0;
            for (int i = 0; i < 3; i++) if (team.Members[i].Archetype == Archetype.Sprinter) leadSlot = i;
            var poses = new Vector3[3];
            int f = 0;
            for (int i = 0; i < 3; i++) poses[i] = i == leadSlot ? new Vector3(x, 0f, 0f) : new Vector3(x - 1.5f + 3f * f++, 0f, -3.5f);
            mgr.SpawnTeam(team, Poses(poses), leadSlot);
            yield return null;
            var leader = mgr.Active;
            leader.ExternalControl = true;
            mgr.SetAIMode(TeamAIMode.Follow);
            ShiftersCamera.Instance.SetTarget(leader, true);
            Speech.Clear();
            yield return ctx.Wait(0.5f);
            bool jumped = false;
            for (int i = 0; i < 360; i++)
            {
                bool doJump = !jumped && leader.Feet.z + leader.Radius >= 14.85f;
                if (doJump) jumped = true;
                bool go = leader.Feet.z < 25f;
                Drive(leader, go ? Vector3.forward : Vector3.zero, go && leader.Feet.z > 9f, doJump, true);
                if (i == 120) ctx.Shot("follow_ai");
                yield return null;
            }
            Stop(leader);
            yield return ctx.Wait(3f);
            ctx.Check(leader.Feet.z > 21f && leader.Feet.y > 0f, $"leader (turbo) crossed the 6 m gap (z {leader.Feet.z:0.0})");
            foreach (var ch in mgr.Team)
            {
                if (ch == leader) continue;
                ctx.Check(ch.Feet.z > 10f && ch.Feet.z < 15.1f && Mathf.Abs(ch.Feet.y - 0.3f) < 0.15f,
                    $"{ch.Archetype} followed 15 m incl. the step and stopped at the 6 m gap edge (z {ch.Feet.z:0.0}, y {ch.Feet.y:0.00}, wait '{ch.AI.WaitReason}')");
            }
            ctx.Check(Speech.Contains(CharacterQuips.CantJump), "follower says \"I can't jump that far!\"");
            ctx.Shot("follow_gap_edge");
        }
    }

    /// <summary>Kinematic test platform moved with MovePosition in FixedUpdate (sine motion).</summary>
    public class TestPlatformMover : MonoBehaviour
    {
        public Vector3 Amplitude = new Vector3(3f, 0f, 0f);
        public float Period = 4f;
        Rigidbody _rb;
        Vector3 _origin;
        float _t;
        void Awake() { _rb = GetComponent<Rigidbody>(); _origin = transform.position; }
        void FixedUpdate()
        {
            _t += Time.fixedDeltaTime;
            float s = Mathf.Sin(_t * Mathf.PI * 2f / Period);
            _rb.MovePosition(_origin + Amplitude * s);
        }
    }
}
