// =====================================================================================
//  SHIFTERS - StoryScript.cs   [story]
//  Data of one cutscene / broadcast: a flat list of StorySteps (Parallel steps hold lanes).
//  Built by StoryScriptBuilder from StoryScripts.Define; played by StoryPlayer.
// =====================================================================================
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Shifters
{
    public enum StoryStepKind
    {
        Line, Broadcast, Shot, ShotTeam, Wait, Fade, Music, Sfx, Banner, WorldWave, Face, Gesture, Move, Puppet, Scare,
        Objective, Card, Parallel, Prompt, Toast, Beat, Do, Tv, CameraDrift
    }

    public sealed class StoryStep
    {
        public StoryStepKind Kind;
        public string Speaker;          // Line / Broadcast: speaker token ("MAX", "PERCY", "{BRUTE}"...); Face/Gesture/Puppet target
        public string Text;             // Line text / mark / banner title / cue id / objective / act
        public string Text2;            // look-at mark / banner subtitle / card title / prompt action id
        public float A, B, C, D;        // durations, fov, world-color from/to...
        public bool Star;               // kept in STORY: SHORT
        public bool FullOnly;           // dropped in STORY: SHORT
        public Expression Face;
        public VisualAction Action;
        public Archetype Who;
        public MusicId Music;
        public SfxId Sfx;
        public BannerStyle Style;
        public Vector3 P0, P1;          // Shot with world positions (when Text == null)
        public List<StoryStep>[] Lanes; // Parallel
        public Action Callback;         // Do

        /// <summary>Steps that still run (instantly) when the scene is skipped / STORY OFF / AutoSkip, so game state stays right.</summary>
        public bool Essential => Kind == StoryStepKind.Music || Kind == StoryStepKind.WorldWave || Kind == StoryStepKind.Objective
                                 || Kind == StoryStepKind.Do || Kind == StoryStepKind.Prompt || Kind == StoryStepKind.Beat;
    }

    public sealed class StoryScript
    {
        public string Id;
        public bool Blocking;
        public bool Letterbox;
        public bool Once;               // at most once per scene load (proximity / checkpoint beats)
        public bool HighPriority;       // non-blocking: jumps the PiP queue and interrupts barks
        public float Watchdog = 45f;
        public readonly List<StoryStep> Steps = new List<StoryStep>();

        public bool HasStars
        {
            get { return AnyStar(Steps); }
        }

        static bool AnyStar(List<StoryStep> steps)
        {
            for (int i = 0; i < steps.Count; i++)
            {
                var s = steps[i];
                if ((s.Kind == StoryStepKind.Line || s.Kind == StoryStepKind.Broadcast) && s.Star) return true;
                if (s.Kind == StoryStepKind.Parallel && s.Lanes != null)
                    for (int l = 0; l < s.Lanes.Length; l++) if (s.Lanes[l] != null && AnyStar(s.Lanes[l])) return true;
            }
            return false;
        }

        /// <summary>Every Line/Broadcast step (lanes included), for tests and tools.</summary>
        public void CollectLines(List<StoryStep> into) { Collect(Steps, into); }

        static void Collect(List<StoryStep> steps, List<StoryStep> into)
        {
            for (int i = 0; i < steps.Count; i++)
            {
                var s = steps[i];
                if (s.Kind == StoryStepKind.Line || s.Kind == StoryStepKind.Broadcast) into.Add(s);
                else if (s.Kind == StoryStepKind.Parallel && s.Lanes != null)
                    for (int l = 0; l < s.Lanes.Length; l++) if (s.Lanes[l] != null) Collect(s.Lanes[l], into);
            }
        }
    }
}
