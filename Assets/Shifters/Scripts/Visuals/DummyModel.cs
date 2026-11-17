// =====================================================================================
//  SHIFTERS - DummyModel.cs   (VISUALS module, campaign wave)
//  TEAM-BUILDING DUMMY (CAMPAIGN 2.4 / 10): a grey wooden mannequin, 1.75 m, ball joints, a
//  painted smile (SPOOKY: MILD = a smiley sticker) and a party hat "MANDATORY FUN". Creepy-cute.
//  Poses (Stand, Reach, Hug, Peek, Slump, Wave, TPose) blend over blendSeconds; a stiff
//  stop-motion shuffle while moving; freezes instantly when lit. Visual only: the stalker
//  brain (enemies) owns movement and colliders. Feet at the origin, facing +Z.
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public class DummyModel : MonoBehaviour, IStalkerVisual
    {
        public const float Height = 1.75f;
        public DummyPose Pose { get; private set; } = DummyPose.Stand;
        public bool Moving { get; private set; }
        public bool Lit { get; private set; }
        public bool Mild { get; private set; }
        /// <summary>World point the head turns toward (creepy follow). null = straight.</summary>
        public Vector3? LookTarget;
        public bool UseUnscaledTime;
        public Transform Root => transform;
        public Transform HeadPivot => _head;

        Transform _model, _spine, _head, _shL, _shR, _elL, _elR, _lgL, _lgR, _knL, _knR;
        GameObject _smile, _sticker;
        struct P { public Vector3 Spine, Head, ShL, ShR, ElL, ElR, LgL, LgR, KnL, KnR; public float Bob; }
        P _from, _cur; float _blend = 1f, _blendDur = 0.25f;
        float _t, _walkPh, _speed, _twitchT = 9f, _headYaw;

        public static DummyModel Create(Transform parent, Vector3 localPosition, Quaternion? localRotation = null, bool mild = false)
        {
            var go = new GameObject("TeamBuildingDummy");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = localRotation ?? Quaternion.identity;
            var d = go.AddComponent<DummyModel>();
            d.Build();
            d.SetMild(mild);
            d._cur = d._from = d.Target(DummyPose.Stand);
            d.Apply(d._cur, 0f);
            return d;
        }

        void Build()
        {
            var wood = MaterialLibrary.Colored(new Color(0.6f, 0.58f, 0.54f), 0.3f);
            var joint = MaterialLibrary.Colored(new Color(0.45f, 0.43f, 0.4f), 0.4f);
            var paint = MaterialLibrary.Colored(new Color(0.12f, 0.1f, 0.1f), 0.5f);
            var hatA = MaterialLibrary.Colored(new Color(0.86f, 0.55f, 0.66f), 0.35f);
            var hatB = MaterialLibrary.Colored(new Color(0.95f, 0.86f, 0.5f), 0.35f);
            _model = CharacterModelBuilder.Pivot("Model", transform, Vector3.zero);
            var hips = CharacterModelBuilder.Pivot("Hips", _model, new Vector3(0f, 0.9f, 0f));
            ProcMesh.Part("Pelvis", hips, VisualMeshes.Ellipsoid(new Vector3(0.17f, 0.1f, 0.12f), 12, 8), wood, Vector3.zero);
            _spine = CharacterModelBuilder.Pivot("Spine", hips, new Vector3(0f, 0.06f, 0f));
            ProcMesh.Part("Torso", _spine, VisualMeshes.Lathe("dummy_torso", new[]
            {
                new Vector4(0f, 0f, 0f, 0f), new Vector4(0f, 0.13f, 0.1f, 0f), new Vector4(0.16f, 0.12f, 0.09f, 0f), new Vector4(0.36f, 0.19f, 0.12f, 0f),
                new Vector4(0.48f, 0.17f, 0.11f, 0f), new Vector4(0.52f, 0.06f, 0.05f, 0f), new Vector4(0.52f, 0f, 0f, 0f)
            }, 2.2f, 14), wood, Vector3.zero);
            _head = CharacterModelBuilder.Pivot("Head", _spine, new Vector3(0f, 0.55f, 0.01f));
            ProcMesh.Part("Neck", _head, VisualMeshes.Limb(0.035f, 0.08f), joint, new Vector3(0f, 0.06f, 0f));
            ProcMesh.Part("HeadMesh", _head, VisualMeshes.Ellipsoid(new Vector3(0.11f, 0.145f, 0.12f), 14, 10), wood, new Vector3(0f, 0.17f, 0f));
            // painted face: two dot eyes + a wide smile (one mesh); mild sticker = round yellow smiley
            _smile = ProcMesh.Part("PaintedSmile", _head, ProcMesh.Cached("dummy_smile", () =>
            {
                var b = new ProcMesh.Builder();
                b.AddSphere(new Vector3(-0.042f, 0.2f, 0.108f), 1f, Quaternion.identity, Color.white, 8, 6, true, new Vector3(0.016f, 0.02f, 0.008f));
                b.AddSphere(new Vector3(0.042f, 0.2f, 0.108f), 1f, Quaternion.identity, Color.white, 8, 6, true, new Vector3(0.016f, 0.02f, 0.008f));
                VisualMeshes.AddStroke(b, VisualMeshes.Arc(0.075f, 0.17f, 205f, 335f, 12), 0.011f, 0.008f, 0.12f, new Vector3(0f, 0.0f, 0.112f));
                b.AddBox(new Vector3(-0.077f, 0.142f, 0.098f), new Vector3(0.006f, 0.022f, 0.006f), Quaternion.Euler(0f, 0f, 25f), Color.white);
                b.AddBox(new Vector3(0.077f, 0.142f, 0.098f), new Vector3(0.006f, 0.022f, 0.006f), Quaternion.Euler(0f, 0f, -25f), Color.white);
                return b.ToMesh("DummySmile");
            }), paint, Vector3.zero, null, null, false);
            _sticker = ProcMesh.Part("SmileySticker", _head, ProcMesh.Cached("dummy_sticker", () =>
            {
                var b = new ProcMesh.Builder();
                b.AddCylinder(new Vector3(0f, 0.17f, 0.118f), 0.07f, 0.006f, Quaternion.Euler(90f, 0f, 0f), Color.white, 18, true);
                return b.ToMesh("Sticker");
            }), MaterialLibrary.Colored(new Color(1f, 0.85f, 0.25f), 0.3f), Vector3.zero, null, null, false);
            ProcMesh.Part("StickerFace", _sticker.transform, ProcMesh.Cached("dummy_stickerface", () =>
            {
                var b = new ProcMesh.Builder();
                b.AddSphere(new Vector3(-0.022f, 0.19f, 0.123f), 0.009f, Quaternion.identity, Color.white, 6, 4);
                b.AddSphere(new Vector3(0.022f, 0.19f, 0.123f), 0.009f, Quaternion.identity, Color.white, 6, 4);
                VisualMeshes.AddStroke(b, VisualMeshes.Arc(0.035f, 0.17f, 215f, 325f, 8), 0.008f, 0.004f, 0f, new Vector3(0f, 0f, 0.123f));
                return b.ToMesh("StickerFace");
            }), paint, Vector3.zero, null, null, false);
            // party hat "MANDATORY FUN"
            var hat = CharacterModelBuilder.Pivot("PartyHat", _head, new Vector3(0.015f, 0.29f, -0.005f));
            hat.localRotation = Quaternion.Euler(0f, 0f, -12f);
            ProcMesh.Part("HatCone", hat, ProcMesh.Cached("dummy_hat", () =>
            {
                var b = new ProcMesh.Builder();
                b.AddCone(Vector3.zero, 0.075f, 0.2f, Quaternion.identity, Color.white, 14);
                return b.ToMesh("HatCone");
            }), hatA, Vector3.zero);
            ProcMesh.Part("HatStripes", hat, ProcMesh.Cached("dummy_hatstripes", () =>
            {
                var b = new ProcMesh.Builder();
                b.AddCylinder(new Vector3(0f, 0.05f, 0f), 0.058f, 0.02f, Quaternion.identity, Color.white, 14, true, false);
                b.AddCylinder(new Vector3(0f, 0.12f, 0f), 0.033f, 0.018f, Quaternion.identity, Color.white, 14, true, false);
                b.AddSphere(new Vector3(0f, 0.205f, 0f), 0.022f, Quaternion.identity, Color.white, 8, 6);
                return b.ToMesh("HatStripes");
            }), hatB, Vector3.zero, null, null, false);
            var txt = EnvKit.AddText(hat, "MANDATORY\nFUN", new Vector3(0f, 0.035f, 0.07f), new Vector2(0.12f, 0.05f), new Color(0.2f, 0.15f, 0.15f), ShiftersFonts.Label, 0.03f);
            txt.transform.localRotation = Quaternion.Euler(-20f, 180f, 0f);
            // arms + legs with ball joints
            for (int s = 0; s < 2; s++)
            {
                bool left = s == 0; float sx = left ? -1f : 1f;
                var sh = CharacterModelBuilder.Pivot(left ? "ShoulderL" : "ShoulderR", _spine, new Vector3(sx * 0.2f, 0.45f, 0f));
                ProcMesh.Part("ShoulderBall", sh, VisualMeshes.Sphere(0.045f, 10, 7), joint, Vector3.zero);
                ProcMesh.Part("Upper", sh, VisualMeshes.Limb(0.035f, 0.28f), wood, Vector3.zero);
                var el = CharacterModelBuilder.Pivot(left ? "ElbowL" : "ElbowR", sh, new Vector3(0f, -0.29f, 0f));
                ProcMesh.Part("ElbowBall", el, VisualMeshes.Sphere(0.035f, 10, 7), joint, Vector3.zero);
                ProcMesh.Part("Fore", el, VisualMeshes.TaperLimb(0.032f, 0.026f, 0.26f), wood, Vector3.zero);
                ProcMesh.Part("Hand", el, VisualMeshes.Ellipsoid(new Vector3(0.035f, 0.06f, 0.02f), 8, 6), wood, new Vector3(0f, -0.31f, 0f));
                var lg = CharacterModelBuilder.Pivot(left ? "LegL" : "LegR", hips, new Vector3(sx * 0.09f, -0.04f, 0f));
                ProcMesh.Part("Thigh", lg, VisualMeshes.Limb(0.05f, 0.4f), wood, Vector3.zero);
                var kn = CharacterModelBuilder.Pivot(left ? "KneeL" : "KneeR", lg, new Vector3(0f, -0.41f, 0f));
                ProcMesh.Part("KneeBall", kn, VisualMeshes.Sphere(0.045f, 10, 7), joint, Vector3.zero);
                ProcMesh.Part("Shin", kn, VisualMeshes.TaperLimb(0.042f, 0.032f, 0.4f), wood, Vector3.zero);
                ProcMesh.Part("Foot", kn, VisualMeshes.Ellipsoid(new Vector3(0.045f, 0.03f, 0.09f), 8, 6), wood, new Vector3(0f, -0.43f, 0.04f));
                if (left) { _shL = sh; _elL = el; _lgL = lg; _knL = kn; } else { _shR = sh; _elR = el; _lgR = lg; _knR = kn; }
            }
            // stand plate (mannequin base) - tiny, so it does not read as a pedestal
        }

        // ------------------------------------------------------------------ API
        public void SetPose(DummyPose pose, float blendSeconds = 0.25f)
        {
            if (pose == Pose && _blend >= 1f) return;
            _from = _cur; Pose = pose; _blend = blendSeconds <= 0f ? 1f : 0f; _blendDur = Mathf.Max(0.01f, blendSeconds);
            if (_blend >= 1f) { _cur = Target(pose); Apply(_cur, 0f); }
        }

        /// <summary>SPOOKY: MILD swaps the painted smile for a smiley sticker.</summary>
        public void SetMild(bool mild)
        {
            Mild = mild;
            if (_smile != null) _smile.SetActive(!mild);
            if (_sticker != null) _sticker.SetActive(mild);
        }

        public void SetMoving(bool moving, float speed = 0f) { Moving = moving && !Lit; _speed = speed > 0f ? speed : Tuning.StalkerSpeed; }
        /// <summary>Lit = frozen on the spot (mid-step), head snaps toward LookTarget.</summary>
        public void SetLit(bool lit) { if (lit && !Lit) _twitchT = 0f; Lit = lit; if (lit) Moving = false; }
        public void SetFrozen(bool frozen) { SetLit(frozen); }
        /// <summary>Small creepy head jerk (scare beat).</summary>
        public void Twitch() { _twitchT = 0f; }

        // ------------------------------------------------------------------ tick
        void LateUpdate()
        {
            float dt = UseUnscaledTime ? TimeController.RealDeltaTime : Time.deltaTime;
            dt = Mathf.Clamp(dt, 0f, 0.1f);
            _t += dt; _twitchT += dt;
            if (_blend < 1f) _blend = Mathf.Min(1f, _blend + dt / _blendDur);
            var tgt = Target(Pose);
            var p = _blend < 1f ? Lerp(_from, tgt, AnimMath.Smooth01(_blend)) : tgt;
            if (Moving)
            {
                // stiff stop-motion shuffle: the phase advances in 8 discrete steps per stride
                _walkPh += dt * Mathf.Clamp(_speed / 1.4f, 0.4f, 3.5f);
                float q = Mathf.Floor(_walkPh * 8f) / 8f * Mathf.PI * 2f;
                float s = Mathf.Sin(q);
                p.LgL.x += -26f * s; p.LgR.x += 26f * s; p.KnL.x += 18f * Mathf.Max(0f, s); p.KnR.x += 18f * Mathf.Max(0f, -s);
                p.ShL.x += 14f * s; p.ShR.x -= 14f * s; p.Bob += 0.02f * Mathf.Abs(Mathf.Cos(q)); p.Spine.x += 6f;
            }
            if (!Lit && !Moving) p.Head.z += 3f * Mathf.Sin(_t * 0.7f);   // slow tilt: "are you lonely?"
            if (_twitchT < 0.25f) { p.Head.z += 22f * Mathf.Sin(_twitchT * 40f) * (1f - _twitchT / 0.25f); p.Head.x -= 10f * (1f - _twitchT / 0.25f); }
            if (LookTarget.HasValue && _head != null)
            {
                var l = transform.InverseTransformPoint(LookTarget.Value); l.y = 0f;
                float yaw = l.sqrMagnitude > 0.01f ? Mathf.Clamp(Mathf.Atan2(l.x, l.z) * Mathf.Rad2Deg, -75f, 75f) : 0f;
                _headYaw = Lit ? yaw : Mathf.Lerp(_headYaw, yaw, AnimMath.Damp(2f, dt));   // lit: snaps (creepy)
                p.Head.y += _headYaw;
            }
            _cur = p;
            Apply(p, dt);
        }

        void Apply(in P p, float dt)
        {
            if (_model == null) return;
            _model.localPosition = new Vector3(0f, p.Bob, 0f);
            _spine.localRotation = Quaternion.Euler(p.Spine); _head.localRotation = Quaternion.Euler(p.Head);
            _shL.localRotation = Quaternion.Euler(p.ShL); _shR.localRotation = Quaternion.Euler(p.ShR);
            _elL.localRotation = Quaternion.Euler(p.ElL); _elR.localRotation = Quaternion.Euler(p.ElR);
            _lgL.localRotation = Quaternion.Euler(p.LgL); _lgR.localRotation = Quaternion.Euler(p.LgR);
            _knL.localRotation = Quaternion.Euler(p.KnL); _knR.localRotation = Quaternion.Euler(p.KnR);
        }

        static P Lerp(in P a, in P b, float t)
        {
            return new P
            {
                Spine = Vector3.Lerp(a.Spine, b.Spine, t), Head = Vector3.Lerp(a.Head, b.Head, t),
                ShL = Vector3.Lerp(a.ShL, b.ShL, t), ShR = Vector3.Lerp(a.ShR, b.ShR, t), ElL = Vector3.Lerp(a.ElL, b.ElL, t), ElR = Vector3.Lerp(a.ElR, b.ElR, t),
                LgL = Vector3.Lerp(a.LgL, b.LgL, t), LgR = Vector3.Lerp(a.LgR, b.LgR, t), KnL = Vector3.Lerp(a.KnL, b.KnL, t), KnR = Vector3.Lerp(a.KnR, b.KnR, t),
                Bob = Mathf.Lerp(a.Bob, b.Bob, t)
            };
        }

        P Target(DummyPose pose)
        {
            var p = new P { ShL = new Vector3(0f, 0f, -7f), ShR = new Vector3(0f, 0f, 7f), ElL = new Vector3(-6f, 0f, 0f), ElR = new Vector3(-6f, 0f, 0f) };
            switch (pose)
            {
                case DummyPose.Reach:
                    p.ShL = new Vector3(-82f, 0f, -6f); p.ShR = new Vector3(-82f, 0f, 6f); p.ElL = p.ElR = new Vector3(-12f, 0f, 0f);
                    p.Spine = new Vector3(8f, 0f, 0f); p.Head = new Vector3(6f, 0f, 14f);
                    break;
                case DummyPose.Hug:
                    p.ShL = new Vector3(-78f, 32f, -4f); p.ShR = new Vector3(-78f, -32f, 4f); p.ElL = new Vector3(-55f, 0f, 0f); p.ElR = new Vector3(-55f, 0f, 0f);
                    p.Spine = new Vector3(12f, 0f, 0f); p.Head = new Vector3(4f, 0f, -18f);
                    break;
                case DummyPose.Peek:
                    p.Spine = new Vector3(4f, 0f, 24f); p.Head = new Vector3(0f, 0f, 26f); p.ShR = new Vector3(-40f, 0f, 30f); p.ElR = new Vector3(-80f, 0f, 0f);
                    p.LgL = new Vector3(0f, 0f, -8f);
                    break;
                case DummyPose.Slump:
                    p.Spine = new Vector3(26f, 0f, 6f); p.Head = new Vector3(48f, 0f, 12f); p.ShL = new Vector3(18f, 0f, -3f); p.ShR = new Vector3(18f, 0f, 3f);
                    p.ElL = p.ElR = Vector3.zero; p.KnL = new Vector3(14f, 0f, 0f); p.KnR = new Vector3(14f, 0f, 0f); p.LgL = p.LgR = new Vector3(-10f, 0f, 0f); p.Bob = -0.03f;
                    break;
                case DummyPose.Wave:
                    p.ShR = new Vector3(-10f, 0f, 150f); p.ElR = new Vector3(0f, 0f, 25f + 30f * Mathf.Sin(_t * 9f)); p.Head = new Vector3(0f, 0f, 16f);
                    break;
                case DummyPose.TPose:
                    p.ShL = new Vector3(0f, 0f, -88f); p.ShR = new Vector3(0f, 0f, 88f); p.ElL = p.ElR = Vector3.zero;
                    break;
            }
            return p;
        }
    }
}
