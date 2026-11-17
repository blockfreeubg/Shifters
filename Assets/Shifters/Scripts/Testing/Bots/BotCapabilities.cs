// =====================================================================================
//  SHIFTERS - BotCapabilities.cs   [BOTS module]
//  Which gameplay modules are real (vs. contract stubs) in this build. Lets validation runs skip
//  sections whose module is not published yet instead of reporting bogus failures.
// =====================================================================================
using System.Reflection;
using System.Text;
using UnityEngine;

namespace Shifters
{
    public sealed class BotCapabilities
    {
        public bool Motor, Camera, Grab, Bridge, Flop, Sensor, Weapon;
        public bool Push, KitTerminal, KitShootTarget, KitPlate, KitExit, LevelManager, LevelMark;

        public static BotCapabilities Probe()
        {
            return new BotCapabilities
            {
                Motor = BotReflect.Declares(typeof(CharacterMotor), "FixedUpdate"),
                Camera = BotReflect.Declares(typeof(ShiftersCamera), "LateUpdate") || BotReflect.Declares(typeof(ShiftersCamera), "Update"),
                Grab = !BotReflect.IsStub(typeof(CharacterGrabber), "TryGrab", typeof(Grabbable)) && !BotReflect.IsStub(typeof(CharacterGrabber), "Throw"),
                Bridge = !BotReflect.IsStub(typeof(CharacterBridge), "TryForm"),
                Flop = !BotReflect.IsStub(typeof(CharacterFlop), "SetFlopped"),
                Sensor = !BotReflect.IsStub(typeof(InteractionSensor), "TryInteract"),
                Weapon = !BotReflect.IsStub(typeof(WeaponHolder), "FireAt"),
                Push = BotReflect.HasType("Pushable") && HasKit("PuzzleKit", "HeavyBlock") && HasKit("PuzzleKit", "PressurePlate"),
                KitTerminal = HasKit("PuzzleKit", "Terminal"),
                KitShootTarget = HasKit("PuzzleKit", "ShootTarget"),
                KitPlate = HasKit("PuzzleKit", "PressurePlate"),
                KitExit = HasKit("LevelKit", "Exit"),
                LevelManager = BotReflect.HasType("LevelManager"),
                LevelMark = BotReflect.HasType("LevelMark"),
            };
        }

        public static bool HasKit(string kit, string method)
        {
            var t = BotReflect.FindType(kit);
            if (t == null) return false;
            foreach (var m in t.GetMethods(BindingFlags.Static | BindingFlags.Public)) if (m.Name == method) return true;
            return false;
        }

        public string Describe()
        {
            var sb = new StringBuilder("[BOT] CAPABILITIES (real module = Y, stub/missing = n):");
            void A(string n, bool v) => sb.Append(' ').Append(n).Append('=').Append(v ? "Y" : "n");
            A("motor", Motor); A("camera", Camera); A("grab+throw", Grab); A("bridge", Bridge); A("flop", Flop);
            A("sensor", Sensor); A("weapon", Weapon); A("pushable+kit", Push); A("kitTerminal", KitTerminal);
            A("kitShootTarget", KitShootTarget); A("kitPlate", KitPlate); A("kitExit", KitExit);
            A("LevelManager", LevelManager); A("LevelMark", LevelMark);
            return sb.ToString();
        }
    }
}
