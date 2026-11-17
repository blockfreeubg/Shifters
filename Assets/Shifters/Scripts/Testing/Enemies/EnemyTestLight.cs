using UnityEngine;

namespace Shifters
{
    /// <summary>Test light: a pool (ILightSource) or a flashlight cone (ILightCone) for EnemyTests.</summary>
    public class EnemyTestLight : MonoBehaviour, ILightSource, ILightCone
    {
        public bool IsCone;
        public bool On = true;
        public float Radius = 3f;
        public float ConeRange = 14f;
        public float ConeHalfAngle = 25f;

        public Vector3 LightPosition => transform.position;
        public float LightRadius => IsCone ? 0f : Radius;
        public bool LightOn => On;
        public Vector3 Origin => transform.position;
        public Vector3 Forward => transform.forward;
        public float Range => ConeRange;
        public float HalfAngle => ConeHalfAngle;

        public static EnemyTestLight Pool(Vector3 pos, float radius = 3f)
        {
            var go = Kit.Make("TestPool", null, pos);
            ProcMesh.AddRenderer(go, ProcMesh.Cylinder(radius, 0.02f, 32, true), Kit.Mat(MatKey.Glow), false);
            var l = go.AddComponent<EnemyTestLight>();
            l.Radius = radius;
            LightQuery.Register(l);
            return l;
        }

        public static EnemyTestLight Cone(Vector3 pos, Vector3 lookAt)
        {
            var go = Kit.Make("TestFlashlight", null, pos, Quaternion.LookRotation(lookAt - pos));
            var l = go.AddComponent<EnemyTestLight>();
            l.IsCone = true;
            LightQuery.RegisterCone(l);
            return l;
        }
    }
}
