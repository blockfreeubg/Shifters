// =====================================================================================
//  SHIFTERS - BotDriver.cs   [BOTS module]
//  Persistent helper object for TeamBot:
//    * writes every bot-controlled character's motor Command in LateUpdate (idle characters get a
//      zero command every frame, so nothing keeps walking after an action ends),
//    * turns "press jump" into a short Jump pulse (stops as soon as the motor raises Jumped),
//    * counts frames -> deterministic simulated clock (DevPlay fixes frames to 1/60 s),
//    * keeps the DevPlay harness alive while a long solution runs (see HoldHarness).
// =====================================================================================
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Shifters
{
    public sealed class BotDriver : MonoBehaviour
    {
        public static BotDriver Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; }

        sealed class Channel
        {
            public ShifterCharacter C;
            public CharacterMotor.Command Cmd;
            public int JumpFrames;
            public bool JumpSeen;
            public Action OnJumped;
            public CharacterMotor HookedMotor;
        }

        readonly List<Channel> _channels = new List<Channel>();
        int _holdCount;
        float _heldOriginalSeconds;

        /// <summary>Frames since the driver was created.</summary>
        public int Frame { get; private set; }
        /// <summary>Bot clock in seconds: simulated (frames/60) under DevPlay, unscaled real time otherwise.</summary>
        public float Now => DevPlayHarness.Active ? Frame / 60f : Time.unscaledTime;

        public static BotDriver Ensure()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("[TeamBotDriver]");
            DontDestroyOnLoad(go);
            return go.AddComponent<BotDriver>();
        }

        void Awake() { Instance = this; }

        void OnDestroy()
        {
            for (int i = 0; i < _channels.Count; i++) Unhook(_channels[i]);
            _channels.Clear();
            if (Instance == this) Instance = null;
        }

        // ------------------------------------------------------------------ harness keep-alive
        /// <summary>DevPlay ends a scenario at -devSeconds + 120 s. While held, the deadline is kept ahead of the
        /// clock; on the last release the original -devSeconds is restored (so short solutions exit early).</summary>
        public void HoldHarness(DevPlayContext ctx)
        {
            if (ctx == null || ctx.Config == null) return;
            if (_holdCount == 0) _heldOriginalSeconds = ctx.Config.seconds;
            _holdCount++;
        }

        public void ReleaseHarness(DevPlayContext ctx)
        {
            if (_holdCount <= 0) return;
            _holdCount--;
            if (_holdCount == 0 && ctx != null && ctx.Config != null) ctx.Config.seconds = _heldOriginalSeconds;
        }

        // ------------------------------------------------------------------ commands
        Channel Get(ShifterCharacter c)
        {
            for (int i = 0; i < _channels.Count; i++) if (_channels[i].C == c) { Hook(_channels[i]); return _channels[i]; }
            var ch = new Channel { C = c };
            ch.OnJumped = () => ch.JumpSeen = true;
            Hook(ch);
            _channels.Add(ch);
            return ch;
        }

        static void Hook(Channel ch)
        {
            var m = ch.C != null ? ch.C.Motor : null;
            if (m == ch.HookedMotor) return;
            Unhook(ch);
            if (m != null) { m.Jumped += ch.OnJumped; ch.HookedMotor = m; }
        }

        static void Unhook(Channel ch)
        {
            if (ch.HookedMotor != null) ch.HookedMotor.Jumped -= ch.OnJumped;
            ch.HookedMotor = null;
        }

        /// <summary>Planar move (world space, magnitude 0..1) + sprint. Keeps the current facing/jump-hold.</summary>
        public void SetMove(ShifterCharacter c, Vector3 move, bool sprint)
        {
            if (c == null) return;
            var ch = Get(c);
            move.y = 0f;
            if (move.sqrMagnitude > 1f) move.Normalize();
            ch.Cmd.Move = move;
            ch.Cmd.Sprint = sprint;
        }

        public void SetFace(ShifterCharacter c, Vector3 face)
        {
            if (c == null) return;
            face.y = 0f;
            Get(c).Cmd.Face = face.sqrMagnitude > 1e-4f ? face.normalized : Vector3.zero;
        }

        public void SetJumpHeld(ShifterCharacter c, bool held) { if (c != null) Get(c).Cmd.JumpHeld = held; }

        /// <summary>Press jump: Jump=true for up to 3 frames (until the motor reports Jumped) + JumpHeld.</summary>
        public void PressJump(ShifterCharacter c)
        {
            if (c == null) return;
            var ch = Get(c);
            ch.JumpFrames = 3;
            ch.JumpSeen = false;
            ch.Cmd.JumpHeld = true;
        }

        /// <summary>True once the motor raised Jumped after the last PressJump.</summary>
        public bool JumpSeen(ShifterCharacter c) => c != null && Get(c).JumpSeen;

        /// <summary>Zero command (stand still).</summary>
        public void Stop(ShifterCharacter c)
        {
            if (c == null) return;
            var ch = Get(c);
            ch.Cmd = default;
            ch.JumpFrames = 0;
        }

        /// <summary>Stop writing commands for this character (AI / player brain take over if ExternalControl is off).</summary>
        public void Release(ShifterCharacter c)
        {
            for (int i = _channels.Count - 1; i >= 0; i--)
                if (_channels[i].C == c) { Unhook(_channels[i]); _channels.RemoveAt(i); }
            if (c != null && c.Motor != null) c.Motor.ClearCommand();
        }

        public void ReleaseAll()
        {
            for (int i = _channels.Count - 1; i >= 0; i--)
            {
                var ch = _channels[i];
                Unhook(ch);
                if (ch.C != null && ch.C.Motor != null) ch.C.Motor.ClearCommand();
            }
            _channels.Clear();
        }

        void Update()
        {
            Frame++;
            if (_holdCount > 0)
            {
                var ctx = DevPlayHarness.Context;
                if (ctx != null && ctx.Config != null && ctx.Config.seconds < Time.time + 30f) ctx.Config.seconds = Time.time + 30f;
            }
        }

        void LateUpdate()
        {
            TeamBot.Current?.RetakeControl(); // a story script that started before Attach hands the team back at its end
            for (int i = _channels.Count - 1; i >= 0; i--)
            {
                var ch = _channels[i];
                if (ch.C == null) { Unhook(ch); _channels.RemoveAt(i); continue; }
                if (!ch.C.ExternalControl || ch.C.Motor == null) continue;
                var cmd = ch.Cmd;
                if (ch.JumpFrames > 0)
                {
                    if (ch.JumpSeen) ch.JumpFrames = 0;
                    else { cmd.Jump = true; ch.JumpFrames--; }
                }
                ch.C.Motor.SetCommand(cmd);
            }
        }
    }
}
