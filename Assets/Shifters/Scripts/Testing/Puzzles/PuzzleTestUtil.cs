using System;
using System.Collections;
using UnityEngine;

namespace Shifters
{
    /// <summary>Helpers for the PUZZLES DevPlay scenarios (arena floors, test characters, waits).</summary>
    public static class PuzzleTestUtil
    {
        static int _slot;

        public static void Floor(Vector3 center, Vector2 size)
        {
            Kit.SolidBox(null, "TestFloor", center + Vector3.down * 0.5f, new Vector3(size.x, 1f, size.y), MatKey.Floor, 0.02f);
        }

        public static ShifterCharacter Spawn(string name, Archetype a, int spd, int str, int iq, float mass, Vector3 pos, Color? color = null)
        {
            var p = new CharacterProfile { Name = name, Archetype = a, Mass = mass };
            p.Stats = new CharacterStats { Speed = spd, Strength = str, IQ = iq };
            p.Body.Height = Tuning.BaseHeight(a);
            p.Body.Radius = Tuning.CapsuleRadius(a);
            p.PrimaryColor = color ?? (a == Archetype.Brute ? new Color(1f, 0.45f, 0.2f) : a == Archetype.Sprinter ? new Color(0.2f, 0.8f, 1f) : new Color(0.7f, 0.5f, 1f));
            var c = CharacterFactory.Create(p, (_slot++) % 3, pos, Quaternion.identity);
            c.ExternalControl = true;
            return c;
        }

        public static ShifterCharacter Brute(Vector3 pos, int str = 8) => Spawn("Big Steve", Archetype.Brute, 3, str, 2, 110f, pos);
        public static ShifterCharacter Sprinter(Vector3 pos) => Spawn("Zippy", Archetype.Sprinter, 8, 3, 3, 60f, pos);
        public static ShifterCharacter Brainiac(Vector3 pos, int iq = 8) => Spawn("Dr. Toast", Archetype.Brainiac, 3, 2, iq, 70f, pos);

        /// <summary>Wait until cond() or timeout (simulated seconds).</summary>
        public static IEnumerator WaitFor(Func<bool> cond, float timeout)
        {
            float t = 0f;
            while (t < timeout && !cond()) { t += Time.deltaTime; yield return null; }
        }

        public static void Look(Vector3 from, Vector3 at, float fov = 55f)
        {
            var cam = Camera.main;
            if (cam == null) return;
            cam.transform.position = from;
            cam.transform.LookAt(at);
            cam.fieldOfView = fov;
            cam.farClipPlane = 500f;
        }

        public static void Move(Rigidbody rb, Vector3 pos)
        {
            rb.position = pos;
            rb.transform.position = pos;
            if (!rb.isKinematic) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
        }

        public static void Shoot(IShootable target, Vector3 point)
        {
            var shot = new ShotInfo { Point = point, Normal = Vector3.back, Direction = Vector3.forward, Damage = 20f, Knockback = 5f };
            target.OnShot(shot);
        }
    }
}
