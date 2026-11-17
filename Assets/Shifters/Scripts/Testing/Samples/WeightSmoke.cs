using System.Collections;
using UnityEngine;

namespace Shifters
{
    /// <summary>Verifies the weight support graph: crates directly on a plate and stacked on each other both count.</summary>
    public static class WeightSmoke
    {
        static Rigidbody Crate(Vector3 pos, float mass)
        {
            var go = new GameObject("Crate" + mass);
            ProcMesh.AddRenderer(go, ProcMesh.CachedBox(Vector3.one * 0.9f, 0.05f), MaterialLibrary.Get(MatKey.Wood));
            go.AddComponent<BoxCollider>().size = Vector3.one * 0.9f;
            var rb = go.AddComponent<Rigidbody>(); rb.mass = mass;
            go.AddComponent<WeightBody>();
            go.transform.position = pos;
            return rb;
        }

        public static IEnumerator Run(DevPlayContext ctx)
        {
            var ground = Kit.SolidBox(null, "Ground", new Vector3(0, -0.5f, 0), new Vector3(20, 1, 20), MatKey.Floor);
            var plateGo = Kit.Make("Plate", null, new Vector3(0, 0.05f, 0));
            var plateCol = Kit.SolidBox(plateGo.transform, "Top", Vector3.zero, new Vector3(2, 0.1f, 2), MatKey.PuzzleBody);
            var sensor = plateGo.AddComponent<WeightSensor>();
            sensor.Surfaces = new Collider[] { plateCol };
            Camera.main.transform.position = new Vector3(0, 3, -6); Camera.main.transform.LookAt(Vector3.zero);
            yield return ctx.Wait(0.3f);
            ctx.Check(sensor.CurrentMass < 0.01f, "empty plate weighs 0 (" + sensor.CurrentMass + ")");
            Crate(new Vector3(0, 0.6f, 0), 25f);
            yield return ctx.Wait(1f);
            ctx.Check(Mathf.Abs(sensor.CurrentMass - 25f) < 0.5f, "one crate = 25 (" + sensor.CurrentMass + ")");
            Crate(new Vector3(0, 1.6f, 0), 60f);
            yield return ctx.Wait(1.2f);
            ctx.Check(Mathf.Abs(sensor.CurrentMass - 85f) < 0.5f, "stacked crate counts = 85 (" + sensor.CurrentMass + ")");
            Crate(new Vector3(4, 0.6f, 0), 160f);
            yield return ctx.Wait(0.8f);
            ctx.Check(Mathf.Abs(sensor.CurrentMass - 85f) < 0.5f, "crate on the ground beside does not count (" + sensor.CurrentMass + ")");
            ctx.Shot("weight_stack");
        }
    }
}
