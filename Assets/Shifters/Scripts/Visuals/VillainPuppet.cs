// =====================================================================================
//  SHIFTERS - VillainPuppet.cs   (VISUALS module, campaign wave)
//  MAX ME as a visual-only puppet (ME-TV studio, hologram taunts, cutscenes, the capture and
//  the courtroom). A CharacterPuppet (LookTarget, SetPose, SetMotion, HeadCenter... all work)
//  plus: villain faces (default Smug), his own gestures (Monologue, OneHandClap, Point,
//  Facepalm, Rage, CrownSlip), channel hats, hologram variant, the crown that slides over his
//  eyes when he is Angry (0.4 s tween), a swaying cape, knee cymbals and a kazoo.
// =====================================================================================
using System;
using UnityEngine;

namespace Shifters
{
    public class VillainPuppet : CharacterPuppet
    {
        public VillainRig VRig { get; private set; }
        public VillainGesture CurrentGesture => _g;
        public bool IsGesturing => _g != VillainGesture.None || (Animator != null && Animator.IsGesturing);
        public float GestureTime => _gT;
        public VillainHat Hat => VRig != null ? VRig.Hat : VillainHat.Crown;
        public bool IsHologram => VRig != null && VRig.Hologram;
        /// <summary>Mouth flaps like speech (broadcasts). Monologue talks on its own.</summary>
        public bool Talking;
        /// <summary>Cymbal / kazoo SFX at the puppet (off in the VillainStudio: the studio is 1200 m under the level).</summary>
        public bool PlaySounds = true;
        /// <summary>Crown slides over his eyes whenever he is Angry (running gag). Default true.</summary>
        public bool AutoCrownSlip = true;
        /// <summary>0 = crown on his head, 1 = over his eyes.</summary>
        public float CrownSlip01 => _slip;
        /// <summary>Raised when a villain gesture ends (not raised when interrupted by another Play).</summary>
        public event Action<VillainGesture> GestureFinished;

        VillainGesture _g; float _gT, _gW; VillainGesture _gLast;
        float _slip, _slipHoldUntil = -1f; bool _forceSlip;
        float _capeX, _capeXV, _capeZ, _capeZV; Vector3 _prevPos, _prevVel; bool _capeInit;
        float _cymbalT = 9f, _kazooT = 9f; bool _stompL, _stompR; int _clapDone;

        /// <summary>Build Max under parent: feet at localPosition, facing +Z of localRotation.</summary>
        public static VillainPuppet Create(Transform parent, Vector3 localPosition, Quaternion localRotation, bool hologram = false)
        {
            var go = new GameObject("Villain_MaxMe");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = localRotation;
            var p = go.AddComponent<VillainPuppet>();
            p.Init(hologram);
            return p;
        }

        void Init(bool hologram)
        {
            VRig = VillainModelBuilder.Build(transform, hologram, 1999);
            Rig = VRig; Profile = VRig.Profile; Head = VRig.Head;
            Animator = new CharacterAnimator(VRig, 1999) { PoseLayer = Layer };
            _base = Expression.Smug;
            Tick(0f);
        }

        // ------------------------------------------------------------------ API
        /// <summary>Neutral means his default Smug. duration > 0 = temporary.</summary>
        public override void SetExpression(Expression e, float duration = 0f)
        {
            if (e == Expression.Neutral) e = Expression.Smug;
            base.SetExpression(e, duration);
        }

        public void Play(VillainGesture g)
        {
            if (g == VillainGesture.None) { _g = VillainGesture.None; return; }
            Animator?.StopGesture();
            _g = g; _gT = 0f; _clapDone = 0; _stompL = _stompR = false;
            if (g == VillainGesture.Monologue || g == VillainGesture.OneHandClap) KazooToot();
        }

        /// <summary>Point / Facepalm use his own versions; other actions play on the shared animator.</summary>
        public override void Play(VisualAction action)
        {
            if (action == VisualAction.Point) { Play(VillainGesture.Point); return; }
            if (action == VisualAction.Facepalm) { Play(VillainGesture.Facepalm); return; }
            _g = VillainGesture.None;
            base.Play(action);
        }

        public void SetHat(VillainHat hat) { if (VRig != null && VRig.Hat != hat) VillainModelBuilder.SetHat(VRig, hat); }
        public void SetHologram(bool on) { VillainModelBuilder.SetHologram(VRig, on); }
        public override void SetGold(bool on) { VillainModelBuilder.SetGoldLook(VRig, on); }

        /// <summary>Slide the crown over his eyes for holdSeconds (0.4 s tween each way).</summary>
        public void SlipCrown(float holdSeconds = 1.2f) { _slipHoldUntil = _time + Mathf.Max(0.1f, holdSeconds); }

        public void CymbalCrash()
        {
            _cymbalT = 0f;
            if (PlaySounds && Application.isPlaying) Sfx.At(SfxId.Cymbal, transform.position + Vector3.up * 0.3f, 0.8f, 1f);
        }

        public void KazooToot()
        {
            _kazooT = 0f;
            if (VRig == null || !Application.isPlaying) return;
            if (PlaySounds) Sfx.At(SfxId.Kazoo, VRig.KazooTip, 0.7f, 1f);
            if (!IsHologram) Fx.Puff(VRig.KazooTip, 0.25f, VillainLook.Gold);
        }

        /// <summary>Text hook for story scripts (StoryScriptBuilder.Puppet): "Rage", "Laugh", "hat:Judge", "Banker",
        /// "hologram:on", "crownslip", "clap", "kazoo", "talk:on", "gold:on", "Point" (VillainGesture / Expression /
        /// VillainHat / VisualAction names, case-insensitive). Returns false when nothing matched.</summary>
        public bool Do(string action)
        {
            if (string.IsNullOrEmpty(action)) return false;
            string a = action.Trim(), arg = null;
            int c = a.IndexOf(':');
            if (c >= 0) { arg = a.Substring(c + 1).Trim().ToLowerInvariant(); a = a.Substring(0, c).Trim(); }
            bool on = arg == null || arg == "on" || arg == "true" || arg == "1";
            switch (a.ToLowerInvariant())
            {
                case "hat": if (arg != null && Enum.TryParse(arg, true, out VillainHat hh)) { SetHat(hh); return true; } return false;
                case "hologram": case "holo": SetHologram(on); return true;
                case "gold": SetGold(on); return true;
                case "talk": case "talking": Talking = on; return true;
                case "crownslip": case "slip": SlipCrown(); return true;
                case "clap": case "cymbal": CymbalCrash(); return true;
                case "kazoo": case "fanfare": KazooToot(); return true;
                case "stop": Play(VillainGesture.None); return true;
            }
            if (Enum.TryParse(a, true, out VillainGesture g) && g != VillainGesture.None) { Play(g); return true; }
            if (Enum.TryParse(a, true, out Expression e)) { SetExpression(e, arg != null && float.TryParse(arg, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float dur) ? dur : 0f); return true; }
            if (Enum.TryParse(a, true, out VillainHat h)) { SetHat(h); return true; }
            if (Enum.TryParse(a, true, out VisualAction va)) { Play(va); return true; }
            return false;
        }

        // ------------------------------------------------------------------ tick
        protected override Expression Resolve()
        {
            if (_tempUntil >= 0f && _time < _tempUntil) return _temp;
            if (_g != VillainGesture.None && _base == Expression.Smug) return VillainLook.GestureFace(_g, _gT);
            if (Pose == PoseOverride.KO) return Expression.KO;
            if (Pose == PoseOverride.Captured && _base == Expression.Smug) return Expression.Shocked;
            return _base;
        }

        public override void Tick(float dt)
        {
            if (VRig == null || Animator == null) return;
            if (_g != VillainGesture.None)
            {
                _gT += dt;
                if (_gT >= VillainLook.GestureDuration(_g)) { _gLast = _g; _g = VillainGesture.None; GestureFinished?.Invoke(_gLast); }
            }
            float target = _g != VillainGesture.None ? 1f : 0f;
            _gW = Mathf.MoveTowards(_gW, target, dt / (target > _gW ? 0.14f : 0.22f));
            base.Tick(dt);
            if (VRig.Face != null) VRig.Face.Talking = Talking || (_g == VillainGesture.Monologue && _gT > 0.3f && _gT < 2.9f);
            Crown(dt);
            Cape(dt);
            Props(dt);
        }

        void Crown(float dt)
        {
            var cp = VRig.CrownPivot; if (cp == null) return;
            bool slip = (AutoCrownSlip && CurrentExpression == Expression.Angry) || _time < _slipHoldUntil
                        || (_g == VillainGesture.CrownSlip && _gT > 0.15f && _gT < 1.35f)
                        || (_g == VillainGesture.Rage && _gT > 0.5f);
            _slip = Mathf.MoveTowards(_slip, slip ? 1f : 0f, dt / 0.4f);
            float s = AnimMath.Smooth01(_slip);
            if (s > 0.98f && slip && dt > 0f) s += 0.015f * Mathf.Sin(_time * 30f); // rattles when it lands
            var R = VRig.HeadR;
            cp.localPosition = VRig.CrownRestPos + new Vector3(0f, -R.y * 0.6f * s, 0.03f * s);
            cp.localRotation = Quaternion.Slerp(VRig.CrownRestRot, Quaternion.Euler(12f, 0f, -2f), Mathf.Clamp01(s));
            if (_defW > 0.01f && s < 0.5f) { cp.localRotation *= Quaternion.Euler(0f, 0f, -22f * _defW); cp.localPosition += new Vector3(0.04f * _defW, -0.03f * _defW, 0f); }
        }

        void Cape(float dt)
        {
            var cape = VRig.Cape; if (cape == null || dt <= 0f) return;
            var pos = cape.position;
            if (!_capeInit) { _prevPos = pos; _prevVel = Vector3.zero; _capeInit = true; }
            var vel = (pos - _prevPos) / dt; var acc = (vel - _prevVel) / dt;
            _prevPos = pos; _prevVel = vel;
            if (acc.sqrMagnitude > 400f) acc = acc.normalized * 20f;
            var la = transform.InverseTransformDirection(acc);
            var lv = transform.InverseTransformDirection(vel);
            float tx = Mathf.Clamp(-la.z * 1.2f - lv.z * 6f, -30f, 40f) + 3f * Mathf.Sin(_time * 1.3f);
            float tz = Mathf.Clamp(la.x * 1.2f, -20f, 20f) + 2f * Mathf.Sin(_time * 0.9f + 1f);
            AnimMath.Spring(ref _capeX, ref _capeXV, tx, 60f, 5f, dt);
            AnimMath.Spring(ref _capeZ, ref _capeZV, tz, 60f, 5f, dt);
            cape.localRotation = Quaternion.Euler(-Mathf.Max(-8f, _capeX), 0f, _capeZ);
        }

        void Props(float dt)
        {
            _cymbalT += dt; _kazooT += dt;
            float cv = _cymbalT < 0.45f ? Mathf.Sin(_cymbalT * 70f) * 14f * (1f - _cymbalT / 0.45f) : 0f;
            if (VRig.CymbalL != null) VRig.CymbalL.localRotation = Quaternion.Euler(cv, 180f, cv * 0.5f);
            if (VRig.CymbalR != null) VRig.CymbalR.localRotation = Quaternion.Euler(-cv, 0f, cv * 0.5f);
            if (VRig.Kazoo != null)
            {
                float k = _kazooT < 0.5f ? 1f + 0.35f * Mathf.Sin(_kazooT * Mathf.PI * 2f / 0.5f * 3f) * (1f - _kazooT / 0.5f) : 1f;
                VRig.Kazoo.localScale = new Vector3(k, k, k);
            }
            if (VRig.Drum != null)
            {
                float db = _cymbalT < 0.3f ? Mathf.Sin(_cymbalT * 50f) * 0.012f * (1f - _cymbalT / 0.3f) : 0f;
                VRig.Drum.localScale = new Vector3(1f + db, 1f + db, 1f);
            }
        }

        // ------------------------------------------------------------------ gesture poses (animator layer)
        float _defW, _laughW, _poutW, _shockW;

        void Layer(ref BodyPose p, float dt)
        {
            // pompous idle: chest out, chin up
            p.Spine.x -= 4f; p.Head.x -= 4f;
            // expression posture (readable at PiP size): Defeated slumps, Laugh throws the head back, Pout turns away (hmph), Shocked recoils
            var ce = CurrentExpression;
            float k = dt > 0f ? Mathf.Clamp01(dt * 7f) : 1f;
            _defW = Mathf.Lerp(_defW, ce == Expression.Defeated || ce == Expression.Sad ? 1f : 0f, k);
            _laughW = Mathf.Lerp(_laughW, ce == Expression.Laugh ? 1f : 0f, k);
            _poutW = Mathf.Lerp(_poutW, ce == Expression.Pout ? 1f : 0f, k);
            _shockW = Mathf.Lerp(_shockW, ce == Expression.Shocked || ce == Expression.Surprised ? 1f : 0f, k);
            if (_defW > 0.001f) { p.Spine.x += 16f * _defW; p.Head.x += 20f * _defW; p.Head.z += 7f * _defW; p.HipsPos.y -= 0.025f * _defW; }
            if (_laughW > 0.001f) { float sh = Mathf.Sin(_time * 22f); p.Head.x -= (14f + 4f * sh) * _laughW; p.Spine.x -= (6f + 2f * sh) * _laughW; p.HipsPos.y += 0.008f * Mathf.Abs(sh) * _laughW; }
            if (_poutW > 0.001f) { p.Head.y += 28f * _poutW; p.Head.x -= 8f * _poutW; p.Head.z -= 6f * _poutW; p.Spine.y += 8f * _poutW; }
            if (_shockW > 0.001f) { p.Head.x -= 8f * _shockW; p.Spine.x -= 5f * _shockW; p.HipsPos.y += 0.015f * _shockW; }
            if (_gW <= 0.001f) return;
            var g = p;
            float t = _gT, w = _gW;
            const float TAU = Mathf.PI * 2f;
            bool lower = false; float trem = 0f;
            switch (_g == VillainGesture.None ? _gLast : _g)
            {
                case VillainGesture.Monologue:
                    g.ShR = new Vector3(-105f + 25f * Mathf.Sin(t * TAU * 0.9f), 0f, 22f + 18f * Mathf.Sin(t * TAU * 0.6f));
                    g.ElR = new Vector3(-35f - 25f * Mathf.Sin(t * TAU * 0.9f + 1f), 0f, 0f); g.HaR = new Vector3(0f, 0f, -20f);
                    g.ShL = new Vector3(-30f, 0f, -38f); g.ElL = new Vector3(-100f, 0f, 0f); g.HaL = Vector3.zero;
                    g.Spine = new Vector3(-9f, 6f * Mathf.Sin(t * TAU * 0.45f), 0f);
                    g.Head = new Vector3(-10f + 4f * Mathf.Sin(t * TAU * 0.9f), 12f * Mathf.Sin(t * TAU * 0.45f), 4f * Mathf.Sin(t * TAU * 0.3f));
                    break;
                case VillainGesture.OneHandClap:
                    {
                        float flap = Mathf.Sin(t * 15f);
                        g.ShR = new Vector3(-150f, 0f, 16f); g.ElR = new Vector3(-25f + 20f * flap, 0f, 0f); g.HaR = new Vector3(0f, 0f, 35f * flap);
                        g.ShL = new Vector3(-20f, 0f, -30f); g.ElL = new Vector3(-90f, 0f, 0f);
                        g.Head = new Vector3(-12f, 0f, 6f); g.Spine = new Vector3(-8f, 0f, -3f);
                        float knock = AnimMath.Bump(Mathf.InverseLerp(0.8f, 1.35f, t), 0.3f, 0.6f);
                        if (knock > 0.01f)
                        {
                            lower = true;
                            g.LgL = new Vector3(-6f, 0f, 11f * knock); g.LgR = new Vector3(-6f, 0f, -11f * knock);
                            g.KnL = new Vector3(10f * knock, 0f, -18f * knock); g.KnR = new Vector3(10f * knock, 0f, 18f * knock);
                            g.HipsPos = new Vector3(0f, -0.02f * knock, 0f);
                        }
                        if (_clapDone == 0 && t > 1.0f) { _clapDone = 1; CymbalCrash(); }
                        break;
                    }
                case VillainGesture.Point:
                    {
                        float jab = t > 0.35f ? Mathf.Sin((t - 0.35f) * TAU * 3f) * Mathf.Max(0f, 1f - (t - 0.35f) * 2f) : 0f;
                        float up = AnimMath.Smooth01(t / 0.25f);
                        g.ShR = new Vector3(Mathf.Lerp(10f, -88f, up) - 6f * jab, 0f, Mathf.Lerp(20f, 4f, up)); g.ElR = new Vector3(-6f, 0f, 0f); g.HaR = new Vector3(-8f * jab, 0f, 0f);
                        g.ShL = new Vector3(12f, 0f, -34f); g.ElL = new Vector3(-95f, 0f, 0f);
                        g.Spine = new Vector3(9f * up + 3f * jab, 10f * up, 0f); g.Head = new Vector3(4f, -8f * up, 0f);
                        break;
                    }
                case VillainGesture.Facepalm:
                    {
                        float r = AnimMath.Smooth01(t / 0.35f);
                        float shake = t > 0.6f ? Mathf.Sin(t * 11f) * 7f : 0f;
                        g.ShR = new Vector3(-118f * r, 0f, Mathf.Lerp(20f, -34f, r)); g.ElR = new Vector3(-122f * r, 0f, 0f); g.HaR = new Vector3(-15f * r, 0f, -10f * r);
                        g.ShL = new Vector3(-10f, 0f, -24f); g.ElL = new Vector3(-40f, 0f, 0f);
                        g.Head = new Vector3(18f * r, shake, 0f); g.Spine = new Vector3(11f * r, 0f, 0f);
                        break;
                    }
                case VillainGesture.Rage:
                    {
                        float shake = Mathf.Sin(t * 19f) * 18f;
                        g.ShL = new Vector3(-158f, 0f, -32f); g.ShR = new Vector3(-158f, 0f, 32f);
                        g.ElL = new Vector3(-55f + shake, 0f, 0f); g.ElR = new Vector3(-55f - shake, 0f, 0f);
                        g.Head = new Vector3(-6f, 0f, 5f * Mathf.Sin(t * 13f)); g.Spine = new Vector3(-4f, 0f, 3f * Mathf.Sin(t * 13f));
                        float ph = t * TAU * 2.5f, sl = Mathf.Max(0f, Mathf.Sin(ph)), sr = Mathf.Max(0f, Mathf.Sin(ph + Mathf.PI));
                        lower = true;
                        g.LgL = new Vector3(-42f * sl, 0f, 0f); g.KnL = new Vector3(70f * sl, 0f, 0f);
                        g.LgR = new Vector3(-42f * sr, 0f, 0f); g.KnR = new Vector3(70f * sr, 0f, 0f);
                        g.HipsPos = new Vector3(0f, 0.025f * (sl + sr), 0f);
                        bool dl = sl < 0.08f && Mathf.Cos(ph) < 0f, dr = sr < 0.08f && Mathf.Cos(ph + Mathf.PI) < 0f;
                        if (dl && !_stompL) StompFx(VRig.FootL); _stompL = dl;
                        if (dr && !_stompR) StompFx(VRig.FootR); _stompR = dr;
                        trem = 2.2f;
                        break;
                    }
                case VillainGesture.CrownSlip:
                    {
                        if (t < 1.1f)
                        {
                            float fl = Mathf.Sin(t * 9f) * 15f;
                            g.ShL = new Vector3(-45f + fl, 0f, -72f); g.ShR = new Vector3(-45f - fl, 0f, 72f);
                            g.ElL = new Vector3(-20f, 0f, 0f); g.ElR = new Vector3(-20f, 0f, 0f);
                            g.Head = new Vector3(-6f, 14f * Mathf.Sin(t * 5f), 0f);
                        }
                        else
                        {
                            float r = AnimMath.Bump(Mathf.InverseLerp(1.1f, 2.1f, t), 0.25f, 0.7f);
                            g.ShL = new Vector3(-168f * r, 0f, -16f); g.ShR = new Vector3(-168f * r, 0f, 16f);
                            g.ElL = new Vector3(-62f * r, 0f, 0f); g.ElR = new Vector3(-62f * r, 0f, 0f);
                            g.Head = new Vector3(-8f * r, 0f, 0f);
                        }
                        break;
                    }
            }
            if (lower) p = BodyPose.Lerp(p, g, w); else p.BlendUpper(g, w);
            if (trem > 0f) { p.Spine.z += trem * w * Mathf.Sin(_time * 53f); p.Head.z += trem * w * Mathf.Sin(_time * 61f); }
        }

        void StompFx(Transform foot)
        {
            CymbalCrash();
            if (foot != null && Application.isPlaying && !IsHologram) Fx.Puff(foot.position, 0.35f);
        }
    }
}
