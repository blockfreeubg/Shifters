// =====================================================================================
//  SHIFTERS - BotAnimator.cs   [owned by the WEAPONS_ENEMIES agent]
//  Procedural animation of the BONK-BOT model by child name (any may be missing):
//  "Eye" color per state (green patrol / yellow curious / red alert / flicker stunned),
//  "Antenna" spring wobble, "ArmL"/"ArmR" reach when grabbing/carrying, "Wheel" spin,
//  "Siren" spin + red/blue flashing light while carrying, whole-model spin when stunned.
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    [DisallowMultipleComponent]
    public class BotAnimator : MonoBehaviour
    {
        public float WheelRadius = 0.3f;

        SecurityBot _bot;
        Transform _model, _body, _eye, _siren, _armL, _armR, _wheel, _antenna;
        Quaternion _armLRest, _armRRest, _antennaRest, _sirenRest, _wheelRest;
        Vector3 _bodyRest;
        Material[] _eyeMats = new Material[0], _sirenMats = new Material[0];
        Light _sirenLight;
        Color _eyeColor = Color.green;
        float _wheelAngle, _spin, _wobble, _wobbleVel, _arm, _sirenAngle, _t;
        Vector3 _lastVel;
        bool _ready;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        void Start()
        {
            _bot = GetComponent<SecurityBot>();
            _model = _bot != null && _bot.ModelRoot != null ? _bot.ModelRoot : transform.Find("Model");
            if (_model == null) return;
            _body = WeaponHolder.FindDeep(_model, "Body");
            _eye = WeaponHolder.FindDeep(_model, "Eye");
            _siren = WeaponHolder.FindDeep(_model, "Siren");
            _armL = WeaponHolder.FindDeep(_model, "ArmL");
            _armR = WeaponHolder.FindDeep(_model, "ArmR");
            _wheel = WeaponHolder.FindDeep(_model, "Wheel");
            _antenna = WeaponHolder.FindDeep(_model, "Antenna");
            if (_armL != null) _armLRest = _armL.localRotation;
            if (_armR != null) _armRRest = _armR.localRotation;
            if (_antenna != null) _antennaRest = _antenna.localRotation;
            if (_siren != null) _sirenRest = _siren.localRotation;
            if (_wheel != null) _wheelRest = _wheel.localRotation;
            if (_body != null) _bodyRest = _body.localPosition;
            _eyeMats = InstanceMaterials(_eye);
            _sirenMats = InstanceMaterials(_siren);
            if (_siren != null)
            {
                var lg = new GameObject("SirenLight");
                lg.transform.SetParent(_siren, false);
                lg.transform.localPosition = new Vector3(0f, 0.25f, 0f);
                _sirenLight = lg.AddComponent<Light>();
                _sirenLight.type = LightType.Point;
                _sirenLight.range = 7f;
                _sirenLight.intensity = 0f;
                _sirenLight.shadows = LightShadows.None;
                _sirenLight.enabled = false;
            }
            _ready = true;
        }

        static Material[] InstanceMaterials(Transform t)
        {
            if (t == null) return new Material[0];
            var rs = t.GetComponentsInChildren<Renderer>();
            var mats = new Material[rs.Length];
            for (int i = 0; i < rs.Length; i++)
            {
                mats[i] = rs[i].material; // runtime per-bot instance (destroyed in OnDestroy)
                mats[i].EnableKeyword("_EMISSION");
            }
            return mats;
        }

        void OnDestroy()
        {
            for (int i = 0; i < _eyeMats.Length; i++) if (_eyeMats[i] != null) Destroy(_eyeMats[i]);
            for (int i = 0; i < _sirenMats.Length; i++) if (_sirenMats[i] != null) Destroy(_sirenMats[i]);
        }

        void LateUpdate()
        {
            if (!_ready || _bot == null) return;
            float dt = Time.deltaTime;
            _t += dt;
            var state = _bot.State;
            Vector3 vel = _bot.Body != null ? _bot.Body.linearVelocity : Vector3.zero;
            Vector3 planar = new Vector3(vel.x, 0f, vel.z);
            float speed = planar.magnitude;

            // ---- eye
            Color target;
            switch (state)
            {
                case BotState.Investigate: target = UIColors.AccentYellow; break;
                case BotState.Chase: target = _bot.IsWaitingBelow ? (Mathf.Repeat(_t * 6f, 1f) < 0.5f ? UIColors.Danger : UIColors.Hex(0x7A1020)) : UIColors.Danger; break;
                case BotState.Capture: target = UIColors.Danger; break;
                case BotState.Stunned: target = Mathf.PerlinNoise(_t * 18f, 0.3f) > 0.5f ? Color.white : UIColors.Hex(0x30343C); break;
                default: target = UIColors.Success; break;
            }
            _eyeColor = state == BotState.Stunned ? target : Color.Lerp(_eyeColor, target, 1f - Mathf.Exp(-12f * dt));
            SetColor(_eyeMats, _eyeColor, state == BotState.Stunned ? 0.8f : 1.35f);

            // ---- siren
            bool alarm = state == BotState.Capture || state == BotState.Chase;
            if (_siren != null)
            {
                _sirenAngle += (alarm ? 600f : 0f) * dt;
                _siren.localRotation = _sirenRest * Quaternion.Euler(0f, _sirenAngle, 0f);
                bool redPhase = Mathf.Repeat(_t * 4f, 1f) < 0.5f;
                Color sc = state == BotState.Capture ? (redPhase ? UIColors.Danger : UIColors.Info) : alarm ? UIColors.Danger : UIColors.Hex(0x5A1E24);
                SetColor(_sirenMats, sc, alarm ? 1.8f : 0.15f);
                if (_sirenLight != null)
                {
                    bool on = state == BotState.Capture;
                    _sirenLight.enabled = on;
                    if (on) { _sirenLight.color = sc; _sirenLight.intensity = 2.5f; }
                }
            }

            // ---- wheel
            if (_wheel != null)
            {
                float fwdSpeed = Vector3.Dot(planar, transform.forward);
                _wheelAngle += fwdSpeed / Mathf.Max(0.05f, WheelRadius) * Mathf.Rad2Deg * dt;
                _wheel.localRotation = _wheelRest * Quaternion.Euler(_wheelAngle, 0f, 0f);
            }

            // ---- antenna spring (kicked by acceleration)
            Vector3 accel = (vel - _lastVel) / Mathf.Max(1e-4f, dt);
            _lastVel = vel;
            float kick = Vector3.Dot(accel, transform.forward);
            _wobbleVel += (-_wobble * 90f - _wobbleVel * 6f - kick * 4f) * dt;
            _wobble += _wobbleVel * dt;
            _wobble = Mathf.Clamp(_wobble, -40f, 40f);
            if (_antenna != null)
            {
                float idle = Mathf.Sin(_t * 3.1f) * (state == BotState.Stunned ? 25f : 4f);
                _antenna.localRotation = _antennaRest * Quaternion.Euler(_wobble, 0f, idle);
            }

            // ---- arms reach when grabbing / carrying
            bool reach = state == BotState.Capture || (state == BotState.Chase && _bot.Target != null && (_bot.Target.Feet - transform.position).sqrMagnitude < 9f);
            _arm = Mathf.MoveTowards(_arm, reach ? 1f : 0f, dt * 4f);
            float swing = Mathf.Sin(_t * 7f) * Mathf.Clamp01(speed / 3f) * 18f * (1f - _arm);
            float flail = state == BotState.Stunned ? Mathf.Sin(_t * 23f) * 35f : 0f;
            if (_armL != null) _armL.localRotation = _armLRest * Quaternion.Euler(-80f * _arm + swing + flail, 0f, 0f);
            if (_armR != null) _armR.localRotation = _armRRest * Quaternion.Euler(-80f * _arm - swing - flail, 0f, 0f);

            // ---- body bob & stun spin
            if (_body != null) _body.localPosition = _bodyRest + Vector3.up * (Mathf.Sin(_t * 11f) * 0.025f * Mathf.Clamp01(speed / 3f));
            if (state == BotState.Stunned)
            {
                float k = Mathf.Clamp01(_bot.StunRemaining / Mathf.Max(0.1f, _bot.StunDuration));
                _spin += (240f + 900f * k) * dt;
                _model.localRotation = Quaternion.Euler(Mathf.Sin(_t * 9f) * 8f * k, _spin, Mathf.Cos(_t * 7f) * 8f * k);
            }
            else
            {
                _spin = Mathf.Repeat(_spin, 360f);
                _spin = Mathf.MoveTowardsAngle(_spin, 0f, 720f * dt);
                float lean = Mathf.Clamp(Vector3.Dot(accel, transform.forward) * 0.6f, -8f, 8f);
                _model.localRotation = Quaternion.Euler(lean, _spin, 0f);
            }
        }

        static void SetColor(Material[] mats, Color c, float emission)
        {
            for (int i = 0; i < mats.Length; i++)
            {
                var m = mats[i];
                if (m == null) continue;
                if (m.HasProperty(BaseColorId)) m.SetColor(BaseColorId, c);
                else if (m.HasProperty(ColorId)) m.SetColor(ColorId, c);
                if (m.HasProperty(EmissionId)) m.SetColor(EmissionId, c * emission);
            }
        }
    }
}
