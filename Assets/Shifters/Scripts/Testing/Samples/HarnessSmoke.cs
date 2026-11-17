using System.Collections;
using UnityEngine;

namespace Shifters
{
    /// <summary>Example DevPlay scenario: proves the harness works. Copy this pattern for module tests.</summary>
    public static class HarnessSmoke
    {
        public static IEnumerator Run(DevPlayContext ctx)
        {
            ctx.Check(GameSession.Instance != null, "GameSession bootstrapped");
            ctx.Check(Mathf.Approximately(Physics.gravity.y, -Tuning.Gravity), "gravity set");
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.localScale = new Vector3(20, 1, 20);
            ground.transform.position = new Vector3(0, -0.5f, 0);
            ground.GetComponent<Renderer>().sharedMaterial = MaterialLibrary.Get(MatKey.Floor);
            var mesh = ProcMesh.Box(new Vector3(1, 1, 1), 0.15f, Color.white);
            var box = new GameObject("BevelBox");
            ProcMesh.AddRenderer(box, mesh, MaterialLibrary.Colored(new Color(1f, 0.45f, 0.2f)));
            box.transform.position = new Vector3(0, 1.5f, 0);
            var sph = new GameObject("Sphere");
            ProcMesh.AddRenderer(sph, ProcMesh.Sphere(0.6f, 10, 7, false), MaterialLibrary.Colored(new Color(0.2f, 0.8f, 0.7f)));
            sph.transform.position = new Vector3(1.8f, 1f, 0);
            var cap = new GameObject("Capsule");
            ProcMesh.AddRenderer(cap, ProcMesh.Capsule(0.4f, 1.6f, 14, 4), MaterialLibrary.Colored(new Color(0.7f, 0.5f, 1f)));
            cap.transform.position = new Vector3(-1.8f, 0.8f, 0);
            var cam = Camera.main;
            cam.transform.position = new Vector3(0, 2.5f, -5f);
            cam.transform.LookAt(new Vector3(0, 1f, 0));
            var team = CharacterGenerator.GenerateTeam(42);
            ctx.Check(team.Members.Length == 3, "team has 3 members");
            yield return ctx.Wait(0.5f);
            ctx.Shot("smoke_primitives");
            ctx.Log("smoke done");
        }
    }
}
