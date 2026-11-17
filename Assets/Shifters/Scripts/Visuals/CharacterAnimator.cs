// =====================================================================================
//  SHIFTERS - CharacterAnimator.cs   (VISUALS module)
//  Shared procedural animation core (used by CharacterVisual AND CharacterPuppet).
//  Layers: locomotion (idle / walk / run / turbo / air / landing / tumble) -> pose override
//  (carry, carried, bridge, hacking, aiming, seated, captured, flop, KO) -> gesture / fidget ->
//  secondary motion (squash & stretch spring, accel/turn lean, tumble wobble springs,
//  propeller, antenna bobble) -> 0.22 s crossfade on pose changes. No per-frame allocations.
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public class CharacterAnimator
    {
        readonly CharacterRig _r;
        readonly RigDims _d;
        readonly System.Random _rng;
        float _t;

        // locomotion state
        float _phase, _spd, _air, _tumble, _vy, _prevSpd;
        float _sq, _sqV, _landCrouch, _airStretch;
        float _accLean, _accLeanV, _turnLean;
        Vector2 _wA, _wAV, _wL, _wLV;
        // idle
        float _lookYaw, _lookTarget, _lookTimer = 2f;
        float _fidgetTimer, _fidgetT = -1f; int _fidget;
        // gestures
        bool _gOn; VisualAction _g; float _gT, _gW;
        // poses
        PoseOverride _pose = PoseOverride.None; float _poseT;
        BodyPose _from = BodyPose.Rest, _last = BodyPose.Rest; float _xfade = 1f;
        float _bridgeLen = Tuning.BridgeLength, _bridgeSag, _bridgeSagS;
        float _tremble;
        // accessories
        float _propAngle, _propSpeed; Vector2 _ant, _antV; Vector3 _prevAntPos, _prevAntVel; bool _antInit;

        /// <summary>0 = calm idle, 1 = bouncy/excited, 2 = nervous.</summary>
        public int IdleStyle;
        public bool IsGesturing => _gOn;
        public VisualAction CurrentGesture => _g;
        public PoseOverride ActivePose => _pose;
        public float GestureWeight => _gW;
        public float Squash => _sq;
        public float BridgeLength => _bridgeLen;
        /// <summary>(campaign) Extra pose layer applied after the gesture (villain gestures, dummy poses). dt = this frame.</summary>
        public delegate void PoseLayerFn(ref BodyPose p, float dt);
        public PoseLayerFn PoseLayer;
        /// <summary>(campaign) Extra trembling in degrees (SCARED interns: ~2.5). Merged with gesture/pose tremble.</summary>
        public float ExtraTremble;
        /// <summary>Animator clock (seconds).</summary>
        public float Clock => _t;

        public CharacterAnimator(CharacterRig rig, int seed)
        {
            _r = rig; _d = rig.D;
            _rng = new System.Random(seed);
            _fidgetTimer = Rand(4f, 9f);
            _t = Rand(0f, 10f);
        }

        float Rand(float a, float b) => a + (float)_rng.NextDouble() * (b - a);

        // ------------------------------------------------------------------ events
        public void Play(VisualAction a) { _gOn = true; _g = a; _gT = 0f; _fidgetT = -1f; }
        public void StopGesture() { _gOn = false; }
        public void OnJump() { _sqV += 3.4f; _landCrouch = 0f; }
        public void OnLand(float impact)
        {
            float amt = Mathf.Clamp(impact * 0.02f, 0.07f, 0.34f);
            _sq = -amt; _sqV = 0f;
            _landCrouch = Mathf.Clamp(impact / 14f, 0.25f, 1f);
        }
        /// <summary>Squash/stretch kick (hit, get-up, switch-in hop). Positive = stretch.</summary>
        public void Kick(float amount) { _sqV += amount; }
        public void SetBridge(float length, float sag01) { _bridgeLen = Mathf.Max(1f, length); _bridgeSag = Mathf.Clamp01(sag01); }
        /// <summary>Skip any running crossfade (portraits / first frame).</summary>
        public void SnapPose() { _xfade = 1f; }

        // ------------------------------------------------------------------ tick
        public void Tick(float dt, in AnimInput inp)
        {
            if (_r.Root == null) return;
            dt = Mathf.Clamp(dt, 0f, 0.1f);
            _t += dt;
            float legLen = Mathf.Max(0.2f, _d.LegLen + _d.AnkleY);

            _spd = Mathf.Lerp(_spd, inp.Speed, AnimMath.Damp(10f, dt));
            _air = Mathf.MoveTowards(_air, inp.Grounded ? 0f : 1f, dt * (inp.Grounded ? 9f : 5f));
            _tumble = Mathf.MoveTowards(_tumble, inp.Tumbling ? 1f : 0f, dt * 5f);
            _vy = inp.VerticalSpeed;
            _bridgeSagS = Mathf.Lerp(_bridgeSagS, _bridgeSag, AnimMath.Damp(6f, dt));

            // springs
            if (dt > 0f)
            {
                AnimMath.Spring(ref _sq, ref _sqV, 0f, 260f, 11f, dt); _sq = Mathf.Clamp(_sq, -0.4f, 0.45f);
                float acc = (_spd - _prevSpd) / Mathf.Max(dt, 1f / 120f); _prevSpd = _spd;
                float leanT = Mathf.Clamp(acc * 1.1f, -14f, 14f) * (inp.Grounded ? 1f : 0.3f);
                AnimMath.Spring(ref _accLean, ref _accLeanV, leanT, 70f, 9f, dt);
                _turnLean = Mathf.Lerp(_turnLean, Mathf.Clamp(-inp.TurnRate * _spd * 0.006f, -14f, 14f), AnimMath.Damp(6f, dt));
                var av = inp.AngularVelocity;
                var tA = new Vector2(Mathf.Clamp(-av.x * 9f, -60f, 60f), Mathf.Clamp(av.z * 9f, -60f, 60f));
                AnimMath.Spring(ref _wA, ref _wAV, tA, 85f, 6f, dt);
                var tL = new Vector2(Mathf.Clamp(av.x * 6f, -40f, 40f), Mathf.Clamp(-av.z * 6f, -40f, 40f));
                AnimMath.Spring(ref _wL, ref _wLV, tL, 70f, 6f, dt);
            }
            _landCrouch = Mathf.MoveTowards(_landCrouch, 0f, dt * 3.5f);
            _airStretch = Mathf.Lerp(_airStretch, _air * Mathf.Clamp01(_vy / 6f) * 0.08f, AnimMath.Damp(8f, dt));

            // pose change -> crossfade from the last output
            if (inp.Pose != _pose) { _from = _last; _xfade = 0f; _pose = inp.Pose; _poseT = 0f; }
            _poseT += dt;
            _tremble = 0f;

            var p = Locomotion(dt, inp, legLen);
            ApplyPose(ref p, inp, legLen);
            ApplyGesture(ref p, dt, inp, legLen);
            PoseLayer?.Invoke(ref p, dt);
            if (ExtraTremble > _tremble) _tremble = ExtraTremble;
            if (_tremble > 0f)
            {
                p.Spine.z += _tremble * Mathf.Sin(_t * 53f); p.Head.z += _tremble * 1.3f * Mathf.Sin(_t * 61f);
                p.Head.x += _tremble * Mathf.Sin(_t * 47f); p.HipsPos.x += _tremble * 0.0025f * Mathf.Sin(_t * 71f);
            }
            if (_xfade < 1f) { _xfade = Mathf.Min(1f, _xfade + dt / 0.22f); p = BodyPose.Lerp(_from, p, AnimMath.Smooth01(_xfade)); }
            _last = p;
            Write(p);
            Accessories(dt, inp);
        }

        // ------------------------------------------------------------------ locomotion
        BodyPose Locomotion(float dt, in AnimInput inp, float legLen)
        {
            var d = _d;
            var p = BodyPose.Rest;
            float walk = Mathf.Clamp01(_spd / 1.2f);
            float run = AnimMath.Smooth01((_spd - 3f) / 4.5f);
            float turboW = inp.Turbo ? Mathf.Clamp01((_spd - 7.5f) / 3f) : 0f;
            float stepLen = legLen * Mathf.Lerp(0.85f, 1.45f, run) * Mathf.Lerp(1f, 0.62f, turboW);
            float freq = Mathf.Min(_spd / Mathf.Max(0.1f, 2f * stepLen), 7f);
            _phase += freq * dt * (inp.Forward < 0f ? -1f : 1f);
            float ph = _phase * Mathf.PI * 2f, s = Mathf.Sin(ph), c = Mathf.Cos(ph);
            float archArm = _r.Profile != null && _r.Profile.Archetype == Archetype.Brute ? 1.2f : _r.Profile != null && _r.Profile.Archetype == Archetype.Brainiac ? 0.8f : 1f;

            float legA = Mathf.Lerp(24f, 44f, run) * walk + 26f * turboW;
            float kneeA = Mathf.Lerp(34f, 82f, run) * walk + 45f * turboW;
            float armA = Mathf.Lerp(18f, 52f, run) * walk * archArm;
            float elb = Mathf.Lerp(12f, 82f, run) * walk;
            float bob = Mathf.Lerp(0.025f, 0.06f, run) * walk * legLen;
            float lean = Mathf.Lerp(4f, 16f, run) * walk + 9f * turboW + _accLean;
            float twist = 9f * walk, sway = (archArm > 1.1f ? 6f : 3f) * walk * (1f - run * 0.5f);

            p.LgL = new Vector3(-legA * s, 0f, -1.5f); p.LgR = new Vector3(legA * s, 0f, 1.5f);
            p.KnL = new Vector3(6f * walk + kneeA * Mathf.Max(0f, c), 0f, 0f);
            p.KnR = new Vector3(6f * walk + kneeA * Mathf.Max(0f, -c), 0f, 0f);
            p.FtL = new Vector3(-(p.LgL.x + p.KnL.x) * 0.8f, 0f, 0f);
            p.FtR = new Vector3(-(p.LgR.x + p.KnR.x) * 0.8f, 0f, 0f);
            p.HipsPos = new Vector3(0f, bob * Mathf.Cos(2f * ph) - (0.5f * bob + run * 0.03f * legLen), 0f);
            p.HipsRot = new Vector3(0f, twist * s, sway * c);
            p.Spine = new Vector3(lean, -twist * 1.3f * s, -sway * c * 0.5f + _turnLean);
            p.ShL = new Vector3(armA * s, 0f, -d.ArmSplay - 4f * run);
            p.ShR = new Vector3(-armA * s, 0f, d.ArmSplay + 4f * run);
            p.ElL = new Vector3(-elb - 12f * walk * Mathf.Max(0f, -s), 0f, 0f);
            p.ElR = new Vector3(-elb - 12f * walk * Mathf.Max(0f, s), 0f, 0f);
            p.Head = new Vector3(-lean * 0.6f + 3f * walk * Mathf.Cos(2f * ph), 0f, 0f);

            // idle layer
            float idle = 1f - walk;
            if (idle > 0.001f)
            {
                float br = Mathf.Sin(_t * Mathf.PI * 2f * 0.28f), ws = Mathf.Sin(_t * Mathf.PI * 2f / 3.6f);
                p.Spine.x += idle * (2f + 1.2f * br);
                p.HipsPos.x += idle * 0.012f * ws; p.HipsRot.z += idle * 1.6f * ws;
                p.ShL.x += idle * 1.5f * br; p.ShR.x += idle * 1.5f * br;
                p.ElL.x -= idle * 6f; p.ElR.x -= idle * 6f;
                _lookTimer -= dt;
                if (_lookTimer <= 0f)
                {
                    _lookTarget = _rng.NextDouble() < 0.4 ? 0f : Rand(-38f, 38f);
                    _lookTimer = IdleStyle == 2 ? Rand(0.5f, 1.4f) : Rand(2.5f, 5.5f);
                }
                _lookYaw = Mathf.Lerp(_lookYaw, _lookTarget, AnimMath.Damp(IdleStyle == 2 ? 9f : 4f, dt));
                p.Head.y += _lookYaw * idle; p.Spine.y += _lookYaw * 0.2f * idle;
                if (IdleStyle == 1)
                {
                    float bb = Mathf.Abs(Mathf.Sin(_t * Mathf.PI * 2f * 1.7f));
                    p.HipsPos.y += idle * (0.045f * bb - 0.03f) * legLen;
                    p.KnL.x += idle * 16f * (1f - bb); p.KnR.x += idle * 16f * (1f - bb);
                    p.LgL.x -= idle * 8f * (1f - bb); p.LgR.x -= idle * 8f * (1f - bb);
                    p.FtL.x -= idle * 8f * (1f - bb); p.FtR.x -= idle * 8f * (1f - bb);
                    float sw = Mathf.Sin(_t * Mathf.PI * 2f * 1.7f);
                    p.ShL.x += idle * 14f * sw; p.ShR.x -= idle * 14f * sw; p.Head.x += idle * 5f * sw;
                }
                else if (IdleStyle == 2)
                {
                    p.Spine.z += idle * 1.4f * Mathf.Sin(_t * 47f); p.Head.z += idle * 2f * Mathf.Sin(_t * 53f);
                    p.ShLPos.y += 0.025f * idle; p.ShRPos.y += 0.025f * idle;
                    p.ShL = Vector3.Lerp(p.ShL, new Vector3(-28f, 0f, 14f), idle); p.ShR = Vector3.Lerp(p.ShR, new Vector3(-28f, 0f, -14f), idle);
                    p.ElL.x = Mathf.Lerp(p.ElL.x, -78f, idle); p.ElR.x = Mathf.Lerp(p.ElR.x, -78f, idle);
                    p.Spine.x += idle * 6f;
                }
            }

            // air layer
            if (_air > 0.001f)
            {
                var a = p;
                float rising = AnimMath.Smooth01((_vy + 2f) / 5f);
                float fl = Mathf.Clamp01((inp.AirTime - 0.25f) / 0.3f);
                var flailL = new Vector3(-95f + 35f * Mathf.Sin(_t * 15f), 0f, -75f + 25f * Mathf.Sin(_t * 11f));
                var flailR = new Vector3(-95f + 35f * Mathf.Sin(_t * 15f + 2f), 0f, 75f - 25f * Mathf.Sin(_t * 11f + 1f));
                var fallL = Vector3.Lerp(new Vector3(-60f, 0f, -55f), flailL, fl);
                var fallR = Vector3.Lerp(new Vector3(-60f, 0f, 55f), flailR, fl);
                a.ShL = Vector3.Lerp(fallL, new Vector3(-150f, 0f, -28f), rising);
                a.ShR = Vector3.Lerp(fallR, new Vector3(-150f, 0f, 28f), rising);
                a.ElL = new Vector3(Mathf.Lerp(-40f + 25f * Mathf.Sin(_t * 17f) * fl, -20f, rising), 0f, 0f);
                a.ElR = new Vector3(Mathf.Lerp(-40f + 25f * Mathf.Sin(_t * 17f + 1f) * fl, -20f, rising), 0f, 0f);
                float bk = _t * Mathf.PI * 2f * 2.6f;
                var bikeL = new Vector3(-30f + 32f * Mathf.Sin(bk) * fl, 0f, -3f); var bikeR = new Vector3(-30f - 32f * Mathf.Sin(bk) * fl, 0f, 3f);
                a.LgL = Vector3.Lerp(bikeL, new Vector3(-38f, 0f, -3f), rising); a.LgR = Vector3.Lerp(bikeR, new Vector3(-12f, 0f, 3f), rising);
                a.KnL = new Vector3(Mathf.Lerp(50f + 35f * Mathf.Sin(bk + 1.2f) * fl, 70f, rising), 0f, 0f);
                a.KnR = new Vector3(Mathf.Lerp(50f - 35f * Mathf.Sin(bk + 1.2f) * fl, 35f, rising), 0f, 0f);
                a.FtL = new Vector3(-20f, 0f, 0f); a.FtR = a.FtL;
                a.Spine = new Vector3(Mathf.Lerp(8f, -4f, rising), 0f, 0f); a.Head = new Vector3(Mathf.Lerp(10f, -10f, rising), 0f, 0f);
                a.HipsPos = Vector3.zero; a.HipsRot = Vector3.zero;
                p = BodyPose.Lerp(p, a, _air);
            }

            // landing crouch
            if (_landCrouch > 0.001f)
            {
                float lc = _landCrouch * _landCrouch;
                p.KnL.x += 50f * lc; p.KnR.x += 50f * lc; p.LgL.x -= 25f * lc; p.LgR.x -= 25f * lc;
                p.FtL.x -= 25f * lc; p.FtR.x -= 25f * lc; p.HipsPos.y -= 0.12f * lc * legLen; p.Spine.x += 14f * lc;
                p.ShL.z -= 20f * lc; p.ShR.z += 20f * lc;
            }

            // tumble layer (limbs loose, spring wobble driven by angular velocity)
            if (_tumble > 0.001f)
            {
                var t = p;
                t.ShL = new Vector3(-40f + _wA.x, 0f, -105f + _wA.y); t.ShR = new Vector3(-40f - _wA.x, 0f, 105f + _wA.y);
                t.ElL = new Vector3(-25f + _wA.y * 0.3f, 0f, 0f); t.ElR = new Vector3(-25f - _wA.y * 0.3f, 0f, 0f);
                t.LgL = new Vector3(-25f + _wL.x, 0f, -22f + _wL.y); t.LgR = new Vector3(-25f - _wL.x, 0f, 22f + _wL.y);
                t.KnL = new Vector3(35f, 0f, 0f); t.KnR = t.KnL; t.FtL = Vector3.zero; t.FtR = Vector3.zero;
                t.Head = new Vector3(_wA.x * 0.4f, 0f, _wA.y * 0.3f); t.Spine = new Vector3(10f, 0f, 0f);
                t.HipsPos = Vector3.zero; t.HipsRot = Vector3.zero;
                p = BodyPose.Lerp(p, t, _tumble);
            }

            // idle fidgets
            if (IdleStyle == 0 && inp.Pose == PoseOverride.None && !_gOn && idle > 0.9f && _air < 0.05f && _tumble < 0.05f)
            {
                if (_fidgetT < 0f)
                {
                    _fidgetTimer -= dt;
                    if (_fidgetTimer <= 0f) { _fidgetT = 0f; _fidget = _rng.Next(4); _fidgetTimer = Rand(6f, 11f); }
                }
            }
            else if (_fidgetT >= 0f && (walk > 0.2f || _gOn || inp.Pose != PoseOverride.None)) _fidgetT = -1f;
            if (_fidgetT >= 0f)
            {
                _fidgetT += dt;
                const float fd = 1.8f;
                if (_fidgetT >= fd) _fidgetT = -1f;
                else CharacterGestures.Fidget(_fidget, _fidgetT, ref p, AnimMath.Bump(_fidgetT / fd, 0.2f, 0.75f));
            }
            return p;
        }

        // ------------------------------------------------------------------ pose overrides
        void ApplyPose(ref BodyPose p, in AnimInput inp, float legLen)
        {
            var d = _d;
            switch (_pose)
            {
                case PoseOverride.Carrying:
                    {
                        var g = p;
                        g.ShL = new Vector3(-168f, 0f, -14f); g.ShR = new Vector3(-168f, 0f, 14f);
                        g.ElL = new Vector3(-28f, 0f, 0f); g.ElR = g.ElL; g.HaL = Vector3.zero; g.HaR = Vector3.zero;
                        g.Spine = new Vector3(p.Spine.x * 0.4f - 6f, p.Spine.y * 0.5f, p.Spine.z); g.Head = new Vector3(-10f, p.Head.y * 0.5f, 0f);
                        p.BlendUpper(g, 1f);
                        if (inp.Strain01 > 0.6f) _tremble = Mathf.Max(_tremble, (inp.Strain01 - 0.6f) * 5f);
                        break;
                    }
                case PoseOverride.Aiming:
                    {
                        var g = p; float aim = Mathf.Clamp(inp.AimPitch, -60f, 70f);
                        g.ShR = new Vector3(-90f - aim, 0f, 0f); g.ElR = new Vector3(-4f, 0f, 0f); g.HaR = Vector3.zero;
                        g.ShL = new Vector3(-80f - aim, 0f, 32f); g.ElL = new Vector3(-50f, 0f, 0f); g.HaL = Vector3.zero;
                        g.Spine = new Vector3(p.Spine.x * 0.5f + 2f, -8f, 0f); g.Head = new Vector3(-aim * 0.6f, 8f, 0f);
                        p.BlendUpper(g, 1f);
                        break;
                    }
                case PoseOverride.Hacking:
                    {
                        var g = p;
                        g.ShL = new Vector3(-52f, 0f, 8f); g.ShR = new Vector3(-52f, 0f, -8f);
                        g.ElL = new Vector3(-62f + 6f * Mathf.Sin(_t * 24f), 0f, 0f); g.ElR = new Vector3(-62f + 6f * Mathf.Sin(_t * 24f + Mathf.PI), 0f, 0f);
                        g.HaL = new Vector3(14f * Mathf.Sin(_t * 26f), 0f, 0f); g.HaR = new Vector3(14f * Mathf.Sin(_t * 26f + 2f), 0f, 0f);
                        g.Spine = new Vector3(10f, 0f, 0f); g.Head = new Vector3(16f, 6f * Mathf.Sin(_t * 1.3f), 0f);
                        p.BlendUpper(g, 1f);
                        break;
                    }
                case PoseOverride.Seated:
                    {
                        var g = BodyPose.Rest;
                        g.RootPos = new Vector3(0f, -d.ThighLen, 0f);
                        g.LgL = new Vector3(-88f, 0f, -5f); g.LgR = new Vector3(-88f, 0f, 5f); g.KnL = new Vector3(88f, 0f, 0f); g.KnR = g.KnL;
                        g.Spine = new Vector3(4f + p.Spine.x * 0.3f, p.Spine.y, 0f); g.Head = p.Head;
                        g.ShL = new Vector3(-38f, 0f, -6f); g.ShR = new Vector3(-38f, 0f, 6f); g.ElL = new Vector3(-48f, 0f, 0f); g.ElR = g.ElL;
                        p = g;
                        break;
                    }
                case PoseOverride.Captured:
                    {
                        var g = p;
                        g.ShL = new Vector3(-155f + 28f * Mathf.Sin(_t * 15f), 0f, -30f + 20f * Mathf.Sin(_t * 11f));
                        g.ShR = new Vector3(-155f + 28f * Mathf.Sin(_t * 15f + 1.7f), 0f, 30f - 20f * Mathf.Sin(_t * 11f + 0.6f));
                        g.ElL = new Vector3(-35f + 25f * Mathf.Sin(_t * 13f), 0f, 0f); g.ElR = new Vector3(-35f + 25f * Mathf.Sin(_t * 13f + 2f), 0f, 0f);
                        g.LgL = new Vector3(-28f * Mathf.Sin(_t * 12f), 0f, -6f); g.LgR = new Vector3(28f * Mathf.Sin(_t * 12f), 0f, 6f);
                        g.KnL = new Vector3(30f + 25f * Mathf.Sin(_t * 12f + 1f), 0f, 0f); g.KnR = new Vector3(30f + 25f * Mathf.Sin(_t * 12f + 2.5f), 0f, 0f);
                        g.FtL = Vector3.zero; g.FtR = Vector3.zero;
                        g.Spine = new Vector3(5f, 0f, 9f * Mathf.Sin(_t * 8f)); g.Head = new Vector3(-10f, 22f * Mathf.Sin(_t * 5f), 0f);
                        g.HipsPos = new Vector3(0f, 0.02f * Mathf.Abs(Mathf.Sin(_t * 12f)), 0f); g.HipsRot = Vector3.zero;
                        p = g; _tremble = Mathf.Max(_tremble, 1.5f);
                        break;
                    }
                case PoseOverride.Carried:
                    {
                        var g = BodyPose.Rest;
                        var face = new Vector3(0f, 0.6f, -0.8f).normalized;
                        g.RootRot = Quaternion.LookRotation(face, Vector3.right);
                        g.RootPos = -(g.RootRot * new Vector3(0f, d.Height * 0.5f, 0f)) + new Vector3(0f, 0.03f, 0f);
                        g.ShL = new Vector3(-90f + 50f * Mathf.Sin(_t * 9f), 0f, -40f - 30f * Mathf.Sin(_t * 7f + 1f));
                        g.ShR = new Vector3(-90f + 50f * Mathf.Sin(_t * 9f + 2f), 0f, 40f + 30f * Mathf.Sin(_t * 7f));
                        g.ElL = new Vector3(-30f + 20f * Mathf.Sin(_t * 11f), 0f, 0f); g.ElR = new Vector3(-30f + 20f * Mathf.Sin(_t * 11f + 1f), 0f, 0f);
                        g.LgL = new Vector3(-20f + 35f * Mathf.Sin(_t * 10f), 0f, -6f); g.LgR = new Vector3(-20f - 35f * Mathf.Sin(_t * 10f), 0f, 6f);
                        g.KnL = new Vector3(40f + 30f * Mathf.Sin(_t * 10f + 1f), 0f, 0f); g.KnR = new Vector3(40f - 30f * Mathf.Sin(_t * 10f + 1f), 0f, 0f);
                        g.Head = new Vector3(-10f, 25f * Mathf.Sin(_t * 3f), 0f);
                        p = g;
                        break;
                    }
                case PoseOverride.Bridge:
                    p = BridgePose();
                    break;
                case PoseOverride.Flop:
                case PoseOverride.KO:
                    {
                        bool ko = _pose == PoseOverride.KO;
                        var g = BodyPose.Rest;
                        float lay = AnimMath.Smooth01((inp.Upright - 0.35f) / 0.5f);
                        g.RootRot = Quaternion.Slerp(Quaternion.identity, Quaternion.Euler(-90f, 0f, 0f), lay);
                        g.RootPos = new Vector3(0f, (d.TorsoD + 0.02f) * lay, d.Height * 0.5f * lay);
                        float spread = ko ? 78f : 100f, lsp = ko ? 12f : 22f;
                        g.ShL = new Vector3(-15f + _wA.x, 0f, -spread + _wA.y); g.ShR = new Vector3(-15f - _wA.x, 0f, spread + _wA.y);
                        g.ElL = new Vector3(-22f, 0f, 0f); g.ElR = g.ElL;
                        g.LgL = new Vector3(-8f + _wL.x, 0f, -lsp); g.LgR = new Vector3(-8f - _wL.x, 0f, lsp);
                        float twitch = ko ? Mathf.Pow(Mathf.Max(0f, Mathf.Sin(_t * 1.3f)), 12f) * 30f : 0f;
                        g.KnL = new Vector3(14f, 0f, 0f); g.KnR = new Vector3(14f + twitch, 0f, 0f);
                        g.Head = ko ? new Vector3(-5f, 18f, 22f) : new Vector3(-10f, 10f * Mathf.Sin(_t * 0.7f), 0f);
                        g.Spine = new Vector3(0f, 0f, 4f);
                        p = g;
                        break;
                    }
            }
        }

        BodyPose BridgePose()
        {
            var d = _d;
            var p = BodyPose.Rest;
            float L = Mathf.Max(1.5f, _bridgeLen);
            float k = L / (d.Height + d.ArmReach * 0.55f);
            float te = Mathf.Clamp01(_poseT / 0.42f), fall = te * te;
            float stretch = AnimMath.EaseOutBack(Mathf.Clamp01((_poseT - 0.25f) / 0.32f));
            float kk = Mathf.Lerp(1f, k, stretch);
            float sag = _bridgeSagS * 0.25f * Mathf.Clamp01(stretch);
            p.RootRot = Quaternion.Euler(90f * fall, 0f, 0f) * Quaternion.Euler(0f, 180f * AnimMath.Smooth01(te), 0f);
            p.RootPos = new Vector3(0f, -d.TorsoD * fall, 0f);
            float hipAlong = d.HipY * kk;
            p.HipsPos = new Vector3(0f, hipAlong - d.HipY, -0.9f * sag);
            float la = Mathf.Atan2(0.9f * sag, hipAlong) * Mathf.Rad2Deg;
            p.LgL = new Vector3(-la, 0f, 0f); p.LgR = p.LgL;
            p.LegScale = Mathf.Sqrt(hipAlong * hipAlong + 0.81f * sag * sag) / Mathf.Max(0.1f, d.HipY);
            float sa = Mathf.Atan2(0.9f * sag, Mathf.Max(0.5f, L - hipAlong)) * Mathf.Rad2Deg;
            p.Spine = new Vector3(sa, 0f, 0f);
            p.SpineScale = kk;
            float shoulderAlong = hipAlong + (d.WaistY - d.HipY) + d.ShoulderY * kk;
            float e = (L - shoulderAlong) / Mathf.Max(0.1f, d.ArmReach * kk);
            p.ArmScale = Mathf.Lerp(1f, Mathf.Clamp(e, 0.6f, 4f), Mathf.Clamp01(stretch));
            p.ShL = new Vector3(-180f, 0f, 4f); p.ShR = new Vector3(-180f, 0f, -4f);
            p.HaL = new Vector3(-15f, 0f, 0f); p.HaR = p.HaL;
            p.Head = new Vector3(-6f, 0f, 0f);
            _tremble = Mathf.Max(_tremble, 0.5f + 1.8f * _bridgeSagS);
            return p;
        }

        // ------------------------------------------------------------------ gestures
        void ApplyGesture(ref BodyPose p, float dt, in AnimInput inp, float legLen)
        {
            bool blocked = _pose == PoseOverride.Bridge || _pose == PoseOverride.Carried || _pose == PoseOverride.KO ||
                           _pose == PoseOverride.Flop || _pose == PoseOverride.Captured || _pose == PoseOverride.Seated;
            if (_gOn)
            {
                _gT += dt;
                if (_gT >= CharacterGestures.Duration(_g) || blocked) _gOn = false;
            }
            float target = _gOn ? 1f : 0f;
            _gW = Mathf.MoveTowards(_gW, target, dt / (target > _gW ? 0.12f : 0.18f));
            if (_gW <= 0.001f || blocked) return;
            var g = p;
            CharacterGestures.Pose(_g, Mathf.Min(_gT, CharacterGestures.Duration(_g)), legLen, ref g, out float tr);
            if (CharacterGestures.IsFullBody(_g) && inp.Grounded && _spd < 2.5f) p = BodyPose.Lerp(p, g, _gW);
            else p.BlendUpper(g, _gW);
            _tremble = Mathf.Max(_tremble, tr * _gW);
        }

        // ------------------------------------------------------------------ output
        static Quaternion Q(Vector3 e) => Quaternion.Euler(e);

        void Write(in BodyPose p)
        {
            var r = _r;
            r.Root.localPosition = p.RootPos;
            r.Root.localRotation = p.RootRot;
            float sy = Mathf.Clamp(1f + _sq + _airStretch, 0.62f, 1.4f), sxz = 1f / Mathf.Sqrt(sy);
            r.Root.localScale = new Vector3(sxz, sy, sxz);
            r.Hips.localPosition = r.HipsRest + p.HipsPos; r.Hips.localRotation = Q(p.HipsRot);
            float ss = Mathf.Max(0.05f, p.SpineScale), asc = Mathf.Max(0.05f, p.ArmScale), ls = Mathf.Max(0.05f, p.LegScale);
            r.Spine.localRotation = Q(p.Spine); r.Spine.localScale = new Vector3(1f, ss, 1f);
            r.Head.localRotation = Q(p.Head); r.Head.localScale = new Vector3(1f, 1f / ss, 1f);
            var handScale = new Vector3(1f, 1f / (asc * ss), 1f);
            r.ShoulderL.localPosition = r.ShoulderLRest + p.ShLPos; r.ShoulderL.localRotation = Q(p.ShL); r.ShoulderL.localScale = new Vector3(1f, asc, 1f);
            r.ShoulderR.localPosition = r.ShoulderRRest + p.ShRPos; r.ShoulderR.localRotation = Q(p.ShR); r.ShoulderR.localScale = new Vector3(1f, asc, 1f);
            r.ElbowL.localRotation = Q(p.ElL); r.ElbowR.localRotation = Q(p.ElR);
            r.HandL.localRotation = Q(p.HaL); r.HandL.localScale = handScale;
            r.HandR.localRotation = Q(p.HaR); r.HandR.localScale = handScale;
            var legScale = new Vector3(1f, ls, 1f); var footScale = new Vector3(1f, 1f / ls, 1f);
            r.LegL.localRotation = Q(p.LgL); r.LegL.localScale = legScale; r.KneeL.localRotation = Q(p.KnL); r.FootL.localRotation = Q(p.FtL); r.FootL.localScale = footScale;
            r.LegR.localRotation = Q(p.LgR); r.LegR.localScale = legScale; r.KneeR.localRotation = Q(p.KnR); r.FootR.localRotation = Q(p.FtR); r.FootR.localScale = footScale;
            if (r.TorsoMesh != null)
            {
                float br = Mathf.Sin(_t * Mathf.PI * 2f * 0.28f) * (1f - Mathf.Clamp01(_spd / 1.2f));
                r.TorsoMesh.localScale = new Vector3(1f + 0.012f * br, 1f + 0.016f * br, 1f + 0.018f * br);
            }
        }

        void Accessories(float dt, in AnimInput inp)
        {
            var r = _r;
            if (r.Propeller != null)
            {
                float target = _spd * 260f + (_air > 0.5f ? 900f : 0f) + (_tumble > 0.5f ? 600f : 0f);
                _propSpeed = Mathf.Lerp(_propSpeed, target, AnimMath.Damp(2.5f, dt));
                _propAngle = Mathf.Repeat(_propAngle + _propSpeed * dt, 360f);
                r.Propeller.localRotation = Quaternion.Euler(0f, _propAngle, 0f);
            }
            if (r.AntennaPivot != null && dt > 0f)
            {
                var pos = r.AntennaPivot.position;
                if (!_antInit) { _prevAntPos = pos; _prevAntVel = Vector3.zero; _antInit = true; }
                var vel = (pos - _prevAntPos) / dt; var acc = (vel - _prevAntVel) / dt;
                _prevAntPos = pos; _prevAntVel = vel;
                if (acc.sqrMagnitude > 3600f) acc = acc.normalized * 60f;
                var la = r.Head.InverseTransformDirection(acc);
                var target = new Vector2(Mathf.Clamp(-la.z * 0.9f, -35f, 35f), Mathf.Clamp(la.x * 0.9f, -35f, 35f));
                AnimMath.Spring(ref _ant, ref _antV, target, 90f, 5f, dt);
                r.AntennaPivot.localRotation = Quaternion.Euler(_ant.x, 0f, _ant.y);
            }
        }
    }
}
