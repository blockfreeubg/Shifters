// =====================================================================================
//  SHIFTERS - PercyMachineView.cs   [owned by STORY_KIT]   (StoryKit.PercyMachine runtime)
//  Proximity talk zone (no collider): polls the active intern's distance at 5 Hz. First approach -> FirstLine (default
//  the cold-open wink), later approaches cycle Lines (cooldown). Lines are non-blocking StoryLines (speaker PERCY) with a
//  steam puff + gurgle and a matching screen face. Idle blinks; dozes "(-_-)zzz" after 25 s alone.
// =====================================================================================
using TMPro;
using UnityEngine;

namespace Shifters
{
    public sealed class PercyMachineView : MonoBehaviour
    {
        public TextMeshPro Face;
        /// <summary>visuals' Percy model (faces via PercyPuppet.SetFace); null = the kit's own mesh + Face text.</summary>
        public PercyPuppet Puppet;
        public MeshRenderer Lamp;
        public Material LampOn, LampOff;
        public float TalkRadius = 3.2f;
        public float Cooldown = 14f;
        public string FirstLine = "percy.wink";
        public string[] Lines;
        public bool TalkEnabled = true;

        float _poll, _cool, _alone, _blink, _faceHold, _lineEnd;
        bool _near, _greeted;
        int _line, _token;
        string _restFace = "(o_o)";

        /// <summary>Show a line now (face e.g. "(^_^)"; null keeps the current one). Returns the StoryKitSay token.</summary>
        public int Say(string text, string face = "(^_^)", float seconds = 0f)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            SetFace(face ?? _restFace, 2.5f);
            if (text == ";)") { SetFace(";)", 1.6f); Fx.Steam(transform.position + transform.up * 1.38f + transform.forward * -0.1f, Vector3.up, 0.6f); Sfx.Play(SfxId.Gurgle, 0.7f); return 0; }
            _token = StoryKitSay.Percy(text, seconds);
            if (Puppet != null) { Puppet.Puff(); Puppet.Kick(0.4f); }
            _lineEnd = Time.time + (seconds > 0f ? seconds : StoryKitSay.AutoSeconds(text));
            Fx.Steam(transform.position + transform.up * 1.38f, Vector3.up, 0.8f);
            Sfx.Play(SfxId.Gurgle, 0.8f);
            return _token;
        }

        public void SetFace(string face, float hold = 0f)
        {
            if (Face != null) Face.text = face;
            if (Puppet != null) Puppet.SetFace(FaceOf(face), hold);
            _faceHold = hold > 0f ? Time.time + hold : 0f;
        }

        public static PercyFace FaceOf(string face)
        {
            switch (face)
            {
                case "(^_^)": return PercyFace.Happy;
                case ";)": case "(^_~)": return PercyFace.Wink;
                case "(-_-)zzz": return PercyFace.Sleepy;
                case "(O_O)": return PercyFace.Shocked;
                case "(T_T)": return PercyFace.Crying;
                case "(>_<)": return PercyFace.Upset;
                default: return PercyFace.Neutral;
            }
        }

        void Update()
        {
            float now = Time.time;
            if (_token != 0 && now >= _lineEnd) { StoryKitSay.Hide(_token); _token = 0; }
            if (Puppet != null) Puppet.Talking = _token != 0;
            if (Lamp != null && LampOn != null && LampOff != null) Lamp.sharedMaterial = _token != 0 || (now % 2.4f) < 0.3f ? LampOn : LampOff;
            if (_faceHold > 0f && now >= _faceHold) { _faceHold = 0f; if (Face != null) Face.text = _restFace; }
            if (_faceHold == 0f && Face != null)
            {
                _blink -= Time.deltaTime;
                if (_blink <= 0f) { _blink = 3.5f + (Mathf.Abs(transform.position.x) % 1.7f); Face.text = "(-_-)"; _faceHold = now + 0.15f; }
            }
            _poll -= Time.deltaTime;
            if (_poll > 0f) return;
            _poll = 0.2f;
            _cool -= 0.2f;
            var a = StoryKit.Active;
            bool near = TalkEnabled && a != null && a.IsAlive && (a.transform.position - transform.position).sqrMagnitude < TalkRadius * TalkRadius;
            if (near) { _alone = 0f; if (_restFace == "(-_-)zzz") { _restFace = "(o_o)"; if (Puppet != null) Puppet.SetFace(PercyFace.Neutral); } }
            else { _alone += 0.2f; if (_alone > 25f && _restFace != "(-_-)zzz") { _restFace = "(-_-)zzz"; if (_faceHold == 0f && Face != null) Face.text = _restFace; if (_faceHold == 0f && Puppet != null) Puppet.SetFace(PercyFace.Sleepy); } }
            if (near && !_near && _cool <= 0f)
            {
                string text;
                if (!_greeted) { _greeted = true; text = StoryKitLines.Resolve(FirstLine); if (string.IsNullOrEmpty(text)) text = FirstLine; }
                else
                {
                    var lines = Lines != null && Lines.Length > 0 ? Lines : StoryKitLines.PercyIdle;
                    text = StoryKitLines.Resolve(lines[_line % lines.Length]);
                    if (string.IsNullOrEmpty(text)) text = lines[_line % lines.Length];
                    _line++;
                }
                if (!string.IsNullOrEmpty(text)) { Say(text, "(^_^)"); _cool = Cooldown; }
            }
            _near = near;
        }

        void OnDisable() { if (_token != 0) { StoryKitSay.Hide(_token); _token = 0; } }
    }
}
