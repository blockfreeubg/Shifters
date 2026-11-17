// =====================================================================================
//  SHIFTERS - ScareRules.cs   [owned by STORY_KIT]
//  Pure-logic core of the ScareDirector (CAMPAIGN §10.1.5), time injected so it is unit-testable:
//    * each cue plays ONCE per attempt (seen ids survive checkpoint restarts: the ScareDirector keys them to
//      LevelAttempt.LevelIndex);
//    * <= 1 scripted scare per CooldownSeconds (30 s);
//    * <= MaxPerChapter (6) per chapter (= per level attempt);
//    * never during a blocking window (hazard / capture / timed window / boss fight / chase / cutscene / custom).
// =====================================================================================
using System.Collections.Generic;

namespace Shifters
{
    public enum ScareVerdict { Ok, Disabled, EmptyId, AlreadySeen, Cooldown, ChapterCap, Blocked }

    public sealed class ScareRules
    {
        public const float CooldownSeconds = 30f;
        public const int MaxPerChapter = 6;
        public const float PunchlineMaxSeconds = 3f;
        public const float StalkerFreezeSeconds = 2f;

        readonly HashSet<string> _seen = new HashSet<string>();
        readonly Dictionary<string, float> _windows = new Dictionary<string, float>();   // id -> until (float.MaxValue = open)
        readonly List<string> _tmp = new List<string>();
        float _last = float.NegativeInfinity;

        public bool Enabled = true;
        public int Count { get; private set; }
        public float LastScareTime => _last;
        public IEnumerable<string> SeenIds => _seen;

        public void Reset() { _seen.Clear(); _windows.Clear(); Count = 0; _last = float.NegativeInfinity; }
        /// <summary>Forget timing windows only (scene change); seen ids / count stay.</summary>
        public void ClearWindows() => _windows.Clear();

        public bool HasSeen(string cueId) => cueId != null && _seen.Contains(cueId);

        /// <summary>Opens (on) or closes a named blocking window (e.g. "timer", "capture", "boss", a level's hazard id).</summary>
        public void SetWindow(string id, bool on)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (on) _windows[id] = float.MaxValue; else _windows.Remove(id);
        }

        /// <summary>Blocks scares until now + seconds (keeps the later end if a window with that id is already open).</summary>
        public void BlockUntil(string id, float until)
        {
            if (string.IsNullOrEmpty(id)) id = "block";
            if (_windows.TryGetValue(id, out var cur) && cur >= until) return;
            _windows[id] = until;
        }

        public bool IsBlocked(float now, out string by)
        {
            by = null;
            _tmp.Clear();
            bool blocked = false;
            foreach (var kv in _windows)
            {
                if (kv.Value > now) { if (!blocked) { blocked = true; by = kv.Key; } }
                else _tmp.Add(kv.Key);
            }
            for (int i = 0; i < _tmp.Count; i++) _windows.Remove(_tmp[i]);
            return blocked;
        }

        public ScareVerdict Check(string cueId, float now)
        {
            if (!Enabled) return ScareVerdict.Disabled;
            if (string.IsNullOrEmpty(cueId)) return ScareVerdict.EmptyId;
            if (_seen.Contains(cueId)) return ScareVerdict.AlreadySeen;
            if (Count >= MaxPerChapter) return ScareVerdict.ChapterCap;
            if (now - _last < CooldownSeconds) return ScareVerdict.Cooldown;
            if (IsBlocked(now, out _)) return ScareVerdict.Blocked;
            return ScareVerdict.Ok;
        }

        /// <summary>Check + commit: marks the cue seen, counts it and starts the cooldown.</summary>
        public ScareVerdict TryBegin(string cueId, float now)
        {
            var v = Check(cueId, now);
            if (v != ScareVerdict.Ok) return v;
            _seen.Add(cueId);
            Count++;
            _last = now;
            return v;
        }
    }
}
