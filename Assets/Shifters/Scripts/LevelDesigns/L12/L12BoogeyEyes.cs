// =====================================================================================
//  SHIFTERS - L12BoogeyEyes.cs   [SHIFT 11 UNMASKED - the door-sized eyes]
//  Colliderless visuals + a trigger sphere r 0.8 (only while open) so the SAY CHEESE cone finds them ("Boogey" in the
//  class name). Flash = fizzle + blind 2 s (boss.OnFlash); any other shot: "BULLETS DON'T SCARE SHADOWS".
// =====================================================================================
using System.Collections;
using UnityEngine;

namespace Shifters
{
    public sealed class L12BoogeyEyes : MonoBehaviour, IShootable
    {
        public L12BoogeymanBoss Boss;
        public Transform EyeL, EyeR, PupilL, PupilR, SmileL, SmileR;
        public SphereCollider Trigger;
        public float EyeHeight = 2f;

        float _open, _target, _speed = 4f;
        Coroutine _co;
        float _lastBulletText = -9f;

        public bool IsOpen => _open > 0.3f;

        void Start() { _open = 0f; Apply(); SetShown(false); }

        void SetShown(bool on)
        {
            for (int i = 0; i < transform.childCount; i++) transform.GetChild(i).gameObject.SetActive(on);
            if (Trigger != null) Trigger.enabled = on;
        }

        void Apply()
        {
            float s = Mathf.Max(0.02f, _open);
            if (EyeL != null) EyeL.localScale = new Vector3(1f, s, 1f);
            if (EyeR != null) EyeR.localScale = new Vector3(1f, s, 1f);
            bool mild = ScareDirector.Mild;
            if (SmileL != null) SmileL.gameObject.SetActive(mild && _open > 0.5f);
            if (SmileR != null) SmileR.gameObject.SetActive(mild && _open > 0.5f);
            if (Trigger != null) Trigger.enabled = _open > 0.3f;
        }

        void Update()
        {
            if (Mathf.Approximately(_open, _target)) return;
            _open = Mathf.MoveTowards(_open, _target, _speed * Time.deltaTime);
            Apply();
            if (_open <= 0.001f && _target <= 0f) SetShown(false);
        }

        void Place(Vector3 at, Vector3 lookAt)
        {
            transform.position = at;
            var d = lookAt - at; d.y = 0f;
            if (d.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(d.normalized);
        }

        public void OpenAt(Vector3 at, Vector3 lookAt, float tell)
        {
            if (_co != null) StopCoroutine(_co);
            Place(at, lookAt);
            SetShown(true);
            _speed = 1f / Mathf.Max(0.2f, tell * 0.5f);
            _target = 1f;
        }

        public void Close() { _speed = 5f; _target = 0f; }
        public void Squint() { if (_co != null) StopCoroutine(_co); _co = StartCoroutine(SquintRoutine()); }

        public void Blink(Vector3 at, float seconds)
        {
            if (_co != null) StopCoroutine(_co);
            var cam = Camera.main;
            Place(at, cam != null ? cam.transform.position : at + Vector3.back);
            _co = StartCoroutine(BlinkRoutine(seconds));
        }

        public void PlayIntro(Vector3 at)
        {
            if (_co != null) StopCoroutine(_co);
            Place(at, new Vector3(0f, at.y, 0f));
            _co = StartCoroutine(IntroRoutine(at));
        }

        IEnumerator SquintRoutine()
        {
            _speed = 8f; _target = 0.25f;
            yield return new WaitForSeconds(0.35f);
            _speed = 4f; _target = 0f;
        }

        IEnumerator BlinkRoutine(float seconds)
        {
            SetShown(true); _speed = 6f; _target = 1f;
            yield return new WaitForSeconds(seconds);
            _target = 0f;
        }

        IEnumerator IntroRoutine(Vector3 at)
        {
            SetShown(true); _speed = 2f; _target = 1f;
            yield return new WaitForSeconds(0.7f);
            for (int b = 0; b < 2; b++) { _speed = 12f; _target = 0.05f; yield return new WaitForSeconds(0.12f); _target = 1f; yield return new WaitForSeconds(0.3f); }
            Fx.FloatingText(at + Vector3.up * 1.8f, ScareDirector.Mild ? "...split... up... (please)" : "...split... up...", new Color(0.78f, 0.6f, 1f), 1.4f);
            Sfx.At(SfxId.Whisper, at, 1f);
            float t = 0f;
            int beeps = 0;
            while (t < 1.8f)
            {
                t += Time.deltaTime;
                transform.position = at + Vector3.forward * 0.0f + Vector3.up * 0f + new Vector3(0f, 0f, 0f) + (-transform.forward) * (t * 1.2f);
                if (t > beeps * 0.55f && beeps < 3) { beeps++; Sfx.At(SfxId.EnemyBeep, transform.position, 1f, 1.3f); Fx.FloatingText(transform.position + Vector3.up * 1.4f, "BEEP.", UIColors.AccentYellow, 0.6f); }
                yield return null;
            }
            _speed = 3f; _target = 0f;
        }

        public void OnShot(in ShotInfo shot)
        {
            if (!IsOpen) return;
            if (shot.Variant == ExperimentalVariant.SayCheese) { if (Boss != null) Boss.OnFlash(); return; }
            if (Time.time - _lastBulletText < 1f) return;
            _lastBulletText = Time.time;
            Fx.FloatingText(transform.position + Vector3.up * 1.4f, "BULLETS DON'T SCARE SHADOWS", Color.white, 1.0f);
        }
    }
}
