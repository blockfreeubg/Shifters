// =====================================================================================
//  SHIFTERS - EndToEndPhases.Abilities.cs   [INTEGRATION - phases 6-9: grab/throw (prop + teammate onto a
//  3.5 m ledge), stacking (head platform -> 2.5 m ledge), living bridge over 5.2 m, flop]
// =====================================================================================
using System.Collections;
using UnityEngine;

namespace Shifters
{
    public static partial class EndToEndPhases
    {
        // ------------------------------------------------------------------ 6. grab & throw
        public static IEnumerator GrabThrow(DevPlayContext ctx)
        {
            var brute = E2E.Brute;
            var crateGo = LevelMark.Find("spawn.crate");
            var crate = crateGo != null ? crateGo.GetComponentInChildren<Grabbable>() : null;
            ctx.Check(crate != null, "sandbox crate is Grabbable (PuzzleKit.Crate)");
            yield return E2E.SwitchTo(brute, true);
            if (crate != null)
            {
                Vector3 cpos = crate.transform.position;
                E2E.Place(brute, new Vector3(cpos.x - 1.25f, 0f, cpos.z), 90f);
                E2E.Frame(60f, 18f);
                yield return ctx.Wait(0.5f);
                bool got = brute.Grabber.TryGrab(crate);
                yield return ctx.Wait(0.4f);
                ctx.Check(got && brute.Grabber.Held == crate && crate.IsHeld, "Brute picked up the crate");
                ctx.Check(brute.TotalMass > brute.Mass + 1f, "carried mass counts (" + brute.TotalMass.ToString("0") + " kg)");
                ctx.Shot("08_carry_crate");
                Vector3 before = crate.transform.position;
                brute.Grabber.Throw((Vector3.right + Vector3.up * 0.45f).normalized);
                ctx.Check(!brute.Grabber.IsHolding, "throw released the crate");
                yield return ctx.Wait(1.6f);
                float flew = E2E.Planar(crate.transform.position, before);
                ctx.Check(flew > 4f, "crate thrown " + flew.ToString("0.0") + " m");
            }

            // teammate throw onto the 3.5 m ledge
            var flyer = E2E.Sprinter;
            var spot = LevelMark.Position("throw.spot");
            var ledge = LevelMark.Position("throw.ledge");
            E2E.Place(brute, spot, -90f);
            E2E.Place(flyer, spot + new Vector3(-1.1f, 0f, 0f), 90f);
            E2E.Frame(-60f, 15f);
            yield return ctx.Wait(0.6f);
            bool grabbed = brute.Grabber.TryGrab(flyer.Grabbable);
            yield return ctx.Wait(0.5f);
            ctx.Check(grabbed && flyer.Condition == CharacterCondition.Carried, "Brute lifted the Sprinter (Carried)");
            ctx.Check(flyer.Body.isKinematic && flyer.transform.parent == brute.CarryPoint, "carried teammate kinematic + parented to the CarryPoint");
            ctx.Shot("09_carry_teammate");
            bool solved = brute.Grabber.ThrowAt(ledge);
            ctx.Check(solved, "ThrowAt found an arc to the 3.5 m ledge");
            ctx.Check(flyer.Condition == CharacterCondition.Thrown, "teammate is Thrown");
            yield return ctx.Wait(0.35f);
            ctx.Shot("10_teammate_mid_throw");
            yield return E2E.Until(() => flyer.Condition == CharacterCondition.Normal && flyer.Motor.IsGrounded, 6f);
            if (flyer.Feet.y < L0_Sandbox.ThrowHeight - 0.3f)
            {
                // a tumbling body can clip the ledge lip (physics); the abilities suite checks ThrowAt precisely - retry once
                ctx.Log("first teammate throw landed at y " + flyer.Feet.y.ToString("0.00") + " (clipped the lip?) - retrying once");
                E2E.Place(brute, spot, -90f);
                E2E.Place(flyer, spot + new Vector3(-1.1f, 0f, 0f), 90f);
                yield return ctx.Wait(0.6f);
                brute.Grabber.TryGrab(flyer.Grabbable);
                yield return ctx.Wait(0.5f);
                brute.Grabber.ThrowAt(ledge);
                yield return ctx.Wait(0.35f);
                yield return E2E.Until(() => flyer.Condition == CharacterCondition.Normal && flyer.Motor.IsGrounded, 6f);
            }
            ctx.Check(flyer.Feet.y > L0_Sandbox.ThrowHeight - 0.3f, "thrown teammate landed on the 3.5 m ledge (feet y " + flyer.Feet.y.ToString("0.00") + ")");
            ctx.Check(flyer.Condition == CharacterCondition.Normal && !flyer.Body.isKinematic, "teammate got back up after the throw");
            ctx.Shot("11_teammate_on_ledge");
            E2E.Place(flyer, new Vector3(-2f, 0f, 14f), 0f);
        }

        // ------------------------------------------------------------------ 7. stacking
        public static IEnumerator Stacking(DevPlayContext ctx)
        {
            var brute = E2E.Brute;
            var climber = E2E.Brainiac;
            var basePos = LevelMark.Position("stack.base");
            E2E.Place(brute, basePos, 90f);
            yield return ctx.Wait(0.4f);
            E2E.Place(climber, brute.HeadTop + Vector3.up * 0.15f, 90f);
            yield return E2E.SwitchTo(climber, true);
            E2E.Frame(20f, 18f);
            yield return ctx.Wait(1.2f);
            ctx.Check(climber.Motor.StandingOn == brute, "Brainiac stands on the Brute's head (StandingOn)");
            ctx.Check(Mathf.Abs(climber.Feet.y - brute.HeadTop.y) < 0.35f && E2E.Planar(climber.Feet, brute.Feet) < 0.6f,
                "stack is stable (offset " + E2E.Planar(climber.Feet, brute.Feet).ToString("0.00") + " m)");
            ctx.Shot("12_stack");
            // jump from the head onto the 2.5 m ledge
            for (int i = 0; i < 70; i++) { E2E.Drive(climber, Vector3.right, false, i == 0, i < 30); yield return null; }
            E2E.Stop(climber);
            yield return E2E.Until(() => climber.Motor.IsGrounded, 2f);
            ctx.Check(climber.Feet.y > L0_Sandbox.StackHeight - 0.25f, "stack jump reached the 2.5 m ledge (feet y " + climber.Feet.y.ToString("0.00") + ")");
            ctx.Shot("13_stack_ledge");
            E2E.Place(climber, new Vector3(0f, 0f, 16f), 0f);
        }

        // ------------------------------------------------------------------ 8. living bridge
        public static IEnumerator Bridge(DevPlayContext ctx)
        {
            var brute = E2E.Brute;
            var walker = E2E.Sprinter;
            var near = LevelMark.Position("bridge.near");
            var far = LevelMark.Position("bridge.far");
            E2E.Place(brute, near, 0f);
            E2E.Place(walker, near + new Vector3(0f, 0f, -2.2f), 0f);
            yield return ctx.Wait(0.5f);
            bool formed = brute.Bridge.TryForm(Vector3.forward);
            ctx.Check(formed, "Brute starts forming a bridge");
            yield return E2E.Until(() => brute.Bridge.IsSolid, 3f);
            ctx.Check(brute.Bridge.IsSolid && brute.Condition == CharacterCondition.Bridging, "bridge is solid (Condition Bridging)");
            ctx.Check(brute.Body.isKinematic, "bridger body kinematic");
            yield return E2E.SwitchTo(walker, true);
            E2E.Frame(30f, 22f);
            // walk across: shoot halfway
            bool shot = false;
            float maxLoad = 0f;
            Vector3 dest = far + Vector3.forward * 0.6f;
            for (int i = 0; i < 60 * 8; i++)
            {
                var d = dest - walker.Feet; d.y = 0f;
                if (d.magnitude < 0.4f) break;
                E2E.Drive(walker, d.normalized * 0.8f);
                maxLoad = Mathf.Max(maxLoad, brute.Bridge.CurrentLoad);
                if (!shot && walker.Feet.z > (L0_Sandbox.BridgeStart + L0_Sandbox.BridgeEnd) * 0.5f) { shot = true; ctx.Shot("14_bridge_walk"); }
                yield return null;
            }
            E2E.Stop(walker);
            ctx.Check(walker.Feet.z > L0_Sandbox.BridgeEnd && walker.Feet.y > -0.3f, "Sprinter crossed the 5.2 m gap on the bridge (feet " + walker.Feet.ToString("0.0") + ")");
            ctx.Check(maxLoad > walker.Mass * 0.6f, "bridge plank measured the walker's weight (max " + maxLoad.ToString("0") + " kg)");
            ctx.Check(brute.Bridge.IsSolid, "bridge held a single walker (no collapse)");
            yield return E2E.WalkTo(walker, far + new Vector3(1.5f, 0f, 2.5f), false, 0.4f, 4f); // clear the far end for the recover
            brute.Bridge.Recover();
            yield return E2E.Until(() => !brute.Bridge.IsRecovering && brute.Condition == CharacterCondition.Normal, 3f);
            ctx.Check(brute.Condition == CharacterCondition.Normal && !brute.Body.isKinematic, "bridger recovered (Normal, dynamic)");
            ctx.Check(brute.Feet.z > L0_Sandbox.BridgeEnd - 0.2f, "recover snapped to the far end (z " + brute.Feet.z.ToString("0.0") + ")");
            yield return ctx.Wait(0.3f);
            ctx.Shot("15_bridge_recovered");
        }

        // ------------------------------------------------------------------ 9. flop
        public static IEnumerator Flop(DevPlayContext ctx)
        {
            var c = CharacterManager.Instance.Active;
            c.Flop.SetFlopped(true);
            yield return ctx.Wait(0.8f);
            ctx.Check(c.Condition == CharacterCondition.Flopped, E2E.Who(c) + " flopped");
            ctx.Shot("16_flop");
            c.Flop.SetFlopped(false);
            yield return E2E.Until(() => c.Condition == CharacterCondition.Normal, 2f);
            ctx.Check(c.Condition == CharacterCondition.Normal, "got up after the flop");
        }
    }
}
