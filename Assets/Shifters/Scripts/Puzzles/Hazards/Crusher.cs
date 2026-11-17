using UnityEngine;

namespace Shifters
{
    /// <summary>
    /// Cycling hydraulic crusher: waits up (warning lamps blink for the last WarningTime), slams down in SlamTime,
    /// holds, rises. Anyone under the head during the slam is KO'd and squirted out sideways. Parks up when inactive.
    /// </summary>
    public class Crusher : PuzzleHazard
    {
        public float UpTime = 1.8f;
        public float SlamTime = 0.16f;
        public float DownTime = 0.6f;
        public float RiseTime = 1.1f;
        [Tooltip("How far the head drops (m).")] public float Drop = 2.6f;
        [Tooltip("Half height of the head block (m).")] public float HeadHalfHeight = 0.4f;
        public Vector2 Footprint = new Vector2(2f, 2f);

        [Header("Parts")]
        public MoverBody Head;
        public Vector3 HeadUpLocal;
        public PuzzleLamp[] WarningLamps = new PuzzleLamp[0];
        [Tooltip("Static piston rod stretched between the head top and RodTopY (local).")] public Transform Rod;
        public float RodTopY = 4f;
        public Revealable Reveal;

        public int Crushed { get; private set; }
        /// <summary>0 = up, 1 = down.</summary>
        public float Extension { get; private set; }

        float _t;
        int _phase; // 0 up, 1 slam, 2 down, 3 rise
        readonly PuzzleCooldown _cool = new PuzzleCooldown(1f);

        void Start()
        {
            _t = Phase;
            if (Reveal != null) Reveal.DynamicLabel = v => _phase == 0 ? "SLAMS IN " + Mathf.Max(0f, UpTime - _t).ToString("0.0") + " s" : "CRUSHER";
        }

        protected override void OnHazardState(bool active, bool warning, bool silent) { }

        void FixedUpdate()
        {
            if (Head == null || IsFrozen) return;
            float dt = Time.fixedDeltaTime;
            if (!IsActive)
            {
                _phase = 0; _t = 0f;
                Extension = Mathf.MoveTowards(Extension, 0f, dt / Mathf.Max(0.1f, RiseTime));
            }
            else
            {
                _t += dt;
                switch (_phase)
                {
                    case 0: if (_t >= UpTime) Next(1); break;
                    case 1:
                        Extension = Mathf.Clamp01(_t / SlamTime);
                        CheckVictims();
                        if (_t >= SlamTime) { Extension = 1f; Next(2); Slammed(); }
                        break;
                    case 2: CheckVictims(); if (_t >= DownTime) Next(3); break;
                    default:
                        Extension = 1f - PuzzleUtil.Ease(_t / RiseTime);
                        if (_t >= RiseTime) { Extension = 0f; Next(0); }
                        break;
                }
            }
            Head.MoveTo(transform.TransformPoint(HeadUpLocal + Vector3.down * (Drop * Extension)));
        }

        void Next(int p) { _phase = p; _t = 0f; }

        void Slammed()
        {
            var p = transform.position + Vector3.up * 0.2f;
            Sfx.At(SfxId.Crusher, p);
            Fx.Puff(p, 1.6f);
            GameEvents.RaiseNoise(p, 8f, null, 0.5f);
        }

        void CheckVictims()
        {
            float bottom = HeadUpLocal.y - Drop * Extension - HeadHalfHeight;
            var center = new Vector3(HeadUpLocal.x, bottom * 0.5f, HeadUpLocal.z);
            var half = new Vector3(Footprint.x * 0.5f, Mathf.Max(0.3f, bottom * 0.5f), Footprint.y * 0.5f);
            float headBottomWorld = transform.TransformPoint(new Vector3(0f, bottom, 0f)).y;
            var all = ShifterCharacter.All;
            for (int i = 0; i < all.Count; i++)
            {
                var c = all[i];
                if (c == null || !c.IsAlive || c.Condition == CharacterCondition.Carried) continue;
                if (c.HeadTop.y < headBottomWorld) continue;
                if (!PuzzleUtil.CharacterInBox(transform, center, half, c) || !_cool.Ready(c)) continue;
                var away = c.transform.position - transform.TransformPoint(HeadUpLocal); away.y = 0f;
                if (away.sqrMagnitude < 0.01f) away = transform.right;
                Crushed++;
                c.Damage(c.Health + 1f, away.normalized * 9f + Vector3.up * 3f, "got crushed");
                Fx.Stars(c.transform, 2f);
                Fx.FloatingText(c.HeadTop, "SQUISH!", UIColors.Danger, 1.2f);
            }
        }

        protected override void HazardUpdate()
        {
            if (Rod != null && Head != null)
            {
                float headTop = transform.InverseTransformPoint(Head.transform.position).y + HeadHalfHeight;
                float len = Mathf.Max(0.05f, RodTopY - headTop);
                Rod.localPosition = new Vector3(HeadUpLocal.x, headTop + len * 0.5f, HeadUpLocal.z);
                Rod.localScale = new Vector3(1f, len, 1f);
            }
            bool warn = IsActive && _phase == 0 && _t > UpTime - WarningTime;
            foreach (var l in WarningLamps)
            {
                if (l == null) continue;
                if (warn) l.Blink(PuzzleLampColor.Yellow, PuzzleLampColor.Off, 8f);
                else l.Set(!IsActive ? PuzzleLampColor.Off : _phase == 0 ? PuzzleLampColor.Green : PuzzleLampColor.Red);
            }
        }
    }
}
