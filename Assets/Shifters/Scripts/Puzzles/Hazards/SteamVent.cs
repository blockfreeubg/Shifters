using UnityEngine;

namespace Shifters
{
    /// <summary>
    /// Floor grate that vents steam on a cycle. While venting, anything above it (within Radius, up to the launch
    /// height) is blasted up to LaunchHeight (+ForwardSpeed along local +Z). Hisses before venting (WarningTime).
    /// Steam is drawn with a ring of pooled puff spheres (no particle system).
    /// </summary>
    public class SteamVent : PuzzleHazard
    {
        public float LaunchHeight = 4.5f;
        public float ForwardSpeed = 0f;
        public float Radius = 0.8f;

        [Header("Parts")]
        public Transform[] Puffs = new Transform[0];
        public Revealable Reveal;

        public int Launches { get; private set; }
        readonly PuzzleCooldown _cool = new PuzzleCooldown(0.45f);
        float _hiss;
        float[] _puffT;

        void Start()
        {
            if (Reveal != null) Reveal.DynamicLabel = v => { float t = TimeToSwitch; return t < 0f ? "STEAM LIFT" : (IsActive ? "STEAM: " : "STEAM IN ") + t.ToString("0.0") + " s"; };
        }

        protected override void OnHazardState(bool active, bool warning, bool silent)
        {
            if (!silent && active) Sfx.At(SfxId.Steam, transform.position);
            for (int i = 0; i < Puffs.Length; i++) if (Puffs[i] != null) Puffs[i].gameObject.SetActive(active);
        }

        protected override void HazardUpdate()
        {
            if (IsWarning)
            {
                _hiss -= Time.deltaTime;
                if (_hiss <= 0f) { _hiss = 0.25f; Fx.Puff(transform.position + Vector3.up * 0.2f, 0.4f, Color.white); }
            }
            if (!IsActive || Puffs.Length == 0) return;
            if (_puffT == null || _puffT.Length != Puffs.Length) { _puffT = new float[Puffs.Length]; for (int i = 0; i < _puffT.Length; i++) _puffT[i] = i / (float)_puffT.Length; }
            float h = Mathf.Max(1.5f, LaunchHeight * 0.8f);
            for (int i = 0; i < Puffs.Length; i++)
            {
                var p = Puffs[i];
                if (p == null) continue;
                _puffT[i] = Mathf.Repeat(_puffT[i] + Time.deltaTime * 1.6f, 1f);
                float t = _puffT[i];
                float ang = i * 2.39996f;
                p.localPosition = new Vector3(Mathf.Cos(ang) * Radius * 0.35f * t, 0.2f + t * h, Mathf.Sin(ang) * Radius * 0.35f * t);
                p.localScale = Vector3.one * Mathf.Lerp(0.3f, 1.3f, t) * (1f - t * t * 0.6f);
            }
        }

        void FixedUpdate()
        {
            if (!IsActive) return;
            float target = Mathf.Sqrt(2f * Tuning.Gravity * Mathf.Max(0.1f, LaunchHeight));
            var all = ShifterCharacter.All;
            var basePos = transform.position;
            for (int i = 0; i < all.Count; i++)
            {
                var c = all[i];
                if (c == null || !c.IsAlive || c.Condition == CharacterCondition.Carried || c.Body == null || c.Body.isKinematic) continue;
                var d = c.transform.position - basePos;
                float y = d.y; d.y = 0f;
                if (d.magnitude > Radius + c.Radius * 0.5f || y < -0.3f || y > LaunchHeight * 0.6f) continue;
                if (c.Body.linearVelocity.y > target * 0.6f || !_cool.Ready(c)) continue;
                Launches++;
                var fwd = transform.forward * ForwardSpeed;
                var dv = new Vector3(fwd.x, target - c.Body.linearVelocity.y, fwd.z);
                if (c.Motor != null) c.Motor.AddImpulse(dv, false, 0f); else c.Body.AddForce(dv, ForceMode.VelocityChange);
                Sfx.At(SfxId.Steam, c.Center, 0.7f, 1.2f);
                Sfx.Voice(c, VoiceLine.Thrown);
            }

            // Props (crates, barrels) ride the steam too: blast a crate up onto a ledge or a plate.
            var fwdP = transform.forward * ForwardSpeed;
            int n = PuzzleUtil.OverlapBodies(basePos + Vector3.up * 0.75f, new Vector3(Radius, 0.75f, Radius), transform.rotation, _bodies);
            for (int i = 0; i < n; i++)
            {
                var rb = _bodies[i];
                if (rb.isKinematic || rb.TryGetComponent(out ShifterCharacter _)) continue;
                var v = rb.linearVelocity;
                if (v.y > target * 0.6f || !_cool.Ready(rb)) continue;
                Launches++;
                rb.linearVelocity = new Vector3(v.x + fwdP.x, target, v.z + fwdP.z);
                Sfx.At(SfxId.Steam, rb.worldCenterOfMass, 0.6f, 1.3f);
            }
        }

        readonly System.Collections.Generic.List<Rigidbody> _bodies = new System.Collections.Generic.List<Rigidbody>(8);
    }
}
