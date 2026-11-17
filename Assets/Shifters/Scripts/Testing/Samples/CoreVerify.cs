using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Shifters
{
    /// <summary>Architect's independent check of the early core: spawn, walk, jump, switch, stack.</summary>
    public static class CoreVerify
    {
        public static IEnumerator Run(DevPlayContext ctx)
        {
            Kit.SolidBox(null, "Ground", new Vector3(0, -0.5f, 10), new Vector3(30, 1, 40), MatKey.Floor);
            var mgrGo = new GameObject("CharacterManager");
            var mgr = mgrGo.AddComponent<CharacterManager>();
            mgrGo.AddComponent<CharacterSwitcher>();
            var cam = Camera.main;
            if (cam.GetComponent<ShiftersCamera>() == null) cam.gameObject.AddComponent<ShiftersCamera>();
            var team = CharacterGenerator.GenerateTeam(7);
            var poses = new List<Pose> { new Pose(new Vector3(-3, 0, 0), Quaternion.identity), new Pose(new Vector3(0, 0, 0), Quaternion.identity), new Pose(new Vector3(3, 0, 0), Quaternion.identity) };
            mgr.SpawnTeam(team, poses, 0);
            yield return ctx.Wait(0.5f);
            ctx.Check(mgr.Team.Count == 3, "3 characters spawned");
            ShifterCharacter sprinter = null, brute = null;
            foreach (var c in mgr.Team) { c.ExternalControl = true; if (c.Archetype == Archetype.Sprinter) sprinter = c; if (c.Archetype == Archetype.Brute) brute = c; }
            ctx.Check(sprinter != null && brute != null, "archetypes present");
            var start = sprinter.transform.position;
            float t = 0;
            while (t < 1.5f) { sprinter.Motor.SetCommand(new CharacterMotor.Command { Move = Vector3.forward, Sprint = true }); t += Time.deltaTime; yield return null; }
            float dist = Vector3.Distance(start, sprinter.transform.position);
            ctx.Check(dist > 8f, "turbo sprinter covered " + dist.ToString("0.0") + " m in 1.5 s");
            sprinter.Motor.SetCommand(new CharacterMotor.Command { Jump = true, JumpHeld = true });
            float maxY = 0; for (int i = 0; i < 60; i++) { maxY = Mathf.Max(maxY, sprinter.transform.position.y); sprinter.Motor.SetCommand(new CharacterMotor.Command { JumpHeld = true }); yield return null; }
            sprinter.Motor.ClearCommand();
            ctx.Check(maxY > 1.4f && maxY < 2.2f, "jump apex " + maxY.ToString("0.00"));
            bool switched = mgr.SwitchTo(brute.Slot, true);
            yield return ctx.Wait(0.3f);
            ctx.Check(switched && mgr.Active == brute, "switched to brute");
            ctx.Shot("core_verify");
        }
    }
}
