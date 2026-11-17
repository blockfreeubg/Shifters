// SHIFTERS - L7Director.cs  ACT NATURAL runtime glue: chapter-only story lines (ch04.disguise, ch04.exit), the
// stealth grade for the Level Complete screen (detections outside S0's lesson), the keycard beat, objective
// updates per checkpoint, and the lonely-customer greeting (ch04.alone, cooldown 30 s) at the teller line.
using UnityEngine;

namespace Shifters
{
    public class L7Director : MonoBehaviour
    {
        public AlarmLatch Lesson;
        public AlarmLatch[] Graded = new AlarmLatch[0];
        public Terminal Printer;
        public Door StaffDoor;
        public Vector3 AloneCenter = new Vector3(3f, 0f, 48f);
        public Vector3 AloneSize = new Vector3(18f, 4f, 8f);
        public float AloneCooldown = 30f;
        public float AloneBuddyRadius = 6f;

        public int Detections { get; private set; }
        public int LessonDetections { get; private set; }
        float _nextAlone, _poll;

        public static void DefineLines()
        {
            if (!StoryScripts.Exists("ch04.disguise"))
                StoryScripts.Define("ch04.disguise", b => b
                    .Line("PERCY", "Disguises on. Fake mustaches: legally distinct from your real faces.")
                    .Line("{BRAINIAC}", "Mine itches.", true)
                    .Line("PERCY", "That's how you know it's working."));
            if (!StoryScripts.Exists("ch04.exit"))
                StoryScripts.Define("ch04.exit", b => b.Priority()
                    .Broadcast("MAX", Expression.Surprised, "Someone's in my basement?")
                    .Broadcast("MAX", Expression.Smug, "...Nobody goes in my basement. Not even me."));
        }

        void Awake() { DefineLines(); }

        void OnEnable()
        {
            if (Lesson != null) Lesson.OnTripped += OnLesson;
            if (Graded != null) foreach (var a in Graded) if (a != null) a.OnTripped += OnGraded;
            if (Printer != null) Printer.Changed += OnPrinter;
            CheckpointManager.CheckpointActivated += OnCheckpoint;
        }

        void OnDisable()
        {
            if (Lesson != null) Lesson.OnTripped -= OnLesson;
            if (Graded != null) foreach (var a in Graded) if (a != null) a.OnTripped -= OnGraded;
            if (Printer != null) Printer.Changed -= OnPrinter;
            CheckpointManager.CheckpointActivated -= OnCheckpoint;
        }

        void Start() { PushGrade(); _nextAlone = Time.time + 6f; }

        void OnLesson(AlarmLatch a)
        {
            LessonDetections++;
            if (LessonDetections == 1) Toast("LESSON LEARNED: DON'T WALK IN AS A GROUP. (THIS ONE IS FREE.)");
        }

        void OnGraded(AlarmLatch a) { Detections++; PushGrade(); }

        void PushGrade()
        {
            LevelCompleteExtras.Detections = Detections;
            LevelCompleteExtras.StealthGrade = LevelCompleteExtras.GradeFor(Detections);
        }

        void OnPrinter(SignalSource s, bool on)
        {
            if (!on) return;
            GameEvents.RaiseStoryBeat("ch04.keycard");
            GameEvents.RaiseObjective("Staff door: ONE (1) employee at a time. Wait far apart.");
        }

        void OnCheckpoint(Checkpoint cp)
        {
            if (cp == null) return;
            if (cp.Index == 1) GameEvents.RaiseObjective("Get behind the teller counter. The founder's statue makes great cover.");
            else if (cp.Index == 2) GameEvents.RaiseObjective("Take the basement stairs. Act natural.");
        }

        static void Toast(string text) { GameEvents.RaiseToast(text, UIColors.AccentYellow); }

        void Update()
        {
            if (Time.time < _poll) return;
            _poll = Time.time + 0.25f;
            if (Time.time < _nextAlone) return;
            var all = ShifterCharacter.All;
            ShifterCharacter lone = null; int inside = 0;
            var b = new Bounds(AloneCenter + Vector3.up * AloneSize.y * 0.5f, AloneSize);
            for (int i = 0; i < all.Count; i++)
            {
                var c = all[i];
                if (c == null || !c.IsAlive) continue;
                if (b.Contains(c.transform.position + Vector3.up * 0.5f)) { inside++; lone = c; }
            }
            if (inside != 1) return;
            float r2 = AloneBuddyRadius * AloneBuddyRadius;
            for (int i = 0; i < all.Count; i++)
            {
                var c = all[i];
                if (c == null || c == lone || !c.IsAlive) continue;
                if ((c.transform.position - lone.transform.position).sqrMagnitude < r2) return;
            }
            _nextAlone = Time.time + AloneCooldown;
            GameEvents.RaiseStoryBeat("ch04.alone");
        }
    }
}
