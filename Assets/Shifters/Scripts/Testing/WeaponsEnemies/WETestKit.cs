using System;
using System.Collections;
using UnityEngine;

namespace Shifters
{
    /// <summary>Dummy IShootable used by the weapons tests (counts hits).</summary>
    public class WETestTarget : MonoBehaviour, IShootable
    {
        public int Hits;
        public ShotInfo Last;
        public void OnShot(in ShotInfo shot) { Hits++; Last = shot; }
    }

    /// <summary>Scene helpers for the WeaponsEnemies DevPlay scenarios.</summary>
    public static class WETestKit
    {
        static readonly Color[] Colors = { UIColors.Hex(0xFF6B35), UIColors.Hex(0x3DE0FF), UIColors.Hex(0xB07CFF), UIColors.Hex(0x7BD389), UIColors.Hex(0xFFD23C), UIColors.Hex(0xFF4D9A) };
        static readonly string[] Names = { "Gary", "Brenda", "Dr. Toast", "Zippy", "Mildred", "Beans" };

        public static void Ground(float size = 70f)
        {
            Kit.SolidBox(null, "Ground", new Vector3(0f, -0.5f, 0f), new Vector3(size, 1f, size), MatKey.Floor, 0f);
        }

        public static ShifterCharacter Character(Archetype a, Vector3 pos, float yaw, int slot, WeaponData weapon = null)
        {
            var p = new CharacterProfile
            {
                Name = Names[slot % Names.Length], Archetype = a, PrimaryColor = Colors[slot % Colors.Length],
                Stats = new CharacterStats { Speed = a == Archetype.Sprinter ? 8 : 3, Strength = a == Archetype.Brute ? 8 : 3, IQ = a == Archetype.Brainiac ? 8 : 3 },
                Weapon = weapon,
            };
            var mr = Tuning.MassRange(a);
            p.Mass = (mr.x + mr.y) * 0.5f;
            p.Body.Height = Tuning.BaseHeight(a);
            p.Body.Radius = Tuning.CapsuleRadius(a);
            var c = CharacterFactory.Create(p, slot, pos, Quaternion.Euler(0f, yaw, 0f));
            c.ExternalControl = true;
            return c;
        }

        public static Rigidbody Prop(string name, Vector3 pos, Vector3 size, float mass, MatKey mat)
        {
            var go = Kit.Make(name, null, pos);
            ProcMesh.AddRenderer(go, ProcMesh.CachedBox(size, 0.05f), Kit.Mat(mat));
            go.AddComponent<BoxCollider>().size = size;
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = mass;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            return rb;
        }

        public static WETestTarget Target(Vector3 pos)
        {
            var go = Kit.Make("ShootDummy", null, pos);
            Kit.SolidBox(go.transform, "Board", Vector3.zero, new Vector3(1f, 1f, 0.2f), MatKey.PuzzleBody);
            Kit.BoxVisual(go.transform, "Bullseye", new Vector3(0f, 0f, -0.11f), new Vector3(0.45f, 0.45f, 0.04f), MatKey.LightRed);
            return go.AddComponent<WETestTarget>();
        }

        public static void Cam(Vector3 pos, Vector3 look)
        {
            var cam = Camera.main;
            if (cam == null) return;
            cam.transform.position = pos;
            cam.transform.LookAt(look);
            cam.fieldOfView = 55f;
        }

        public static IEnumerator WaitUntil(Func<bool> cond, float timeout)
        {
            int frames = Mathf.CeilToInt(timeout * 60f);
            for (int i = 0; i < frames; i++) { if (cond()) yield break; yield return null; }
        }

        public static float Planar(Vector3 a, Vector3 b) { a.y = 0f; b.y = 0f; return Vector3.Distance(a, b); }
    }
}
