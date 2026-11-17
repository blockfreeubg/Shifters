// =====================================================================================
//  SHIFTERS - StoryScriptBuilder.cs   [story]
//  Fluent DSL used by StoryScripts.Define (CAMPAIGN §18.2). Every step is recorded in order; the
//  StoryPlayer executes them. Waiting steps: Line, Broadcast, Wait, Fade (out part), Prompt, Parallel
//  (all lanes). Everything else is instant (Shot/Move start a motion and continue in the background).
//  Example:
//    StoryScripts.Define("C99", b => b.Blocking().Letterbox()
//        .Line("MAX", "Hello, {NAME}.", star: true)
//        .Gesture("{BRUTE}", VisualAction.Wave).Wait(0.5f)
//        .Banner("TITLE", "SUB", BannerStyle.Story));
// =====================================================================================
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Shifters
{
    public sealed class StoryScriptBuilder
    {
        readonly StoryScript _s;
        readonly List<StoryStep> _steps;
        StoryStep _last;

        internal StoryScriptBuilder(StoryScript s) { _s = s; _steps = s.Steps; }
        StoryScriptBuilder(StoryScript s, List<StoryStep> lane) { _s = s; _steps = lane; }

        public StoryScript Script => _s;

        StoryScriptBuilder Add(StoryStep st) { _steps.Add(st); _last = st; return this; }

        // ---- script flags
        /// <summary>Blocking scene: InputContext.Cutscene, HUD suppressed, CutsceneCamera, letterbox, skippable.</summary>
        public StoryScriptBuilder Blocking() { _s.Blocking = true; _s.Letterbox = true; return this; }
        public StoryScriptBuilder Letterbox() { _s.Letterbox = true; return this; }
        /// <summary>Play at most once per scene load (proximity / checkpoint beats that may fire twice).</summary>
        public StoryScriptBuilder Once() { _s.Once = true; return this; }
        /// <summary>Non-blocking: interrupts barks and jumps the PiP queue (boss phases).</summary>
        public StoryScriptBuilder Priority() { _s.HighPriority = true; return this; }
        public StoryScriptBuilder Watchdog(float seconds) { _s.Watchdog = Mathf.Max(1f, seconds); return this; }
        /// <summary>Marks the previous step as dropped in STORY: SHORT.</summary>
        public StoryScriptBuilder FullOnly() { if (_last != null) _last.FullOnly = true; return this; }

        // ---- dialogue
        /// <summary>A subtitle line. speakerToken: "MAX", "PERCY", "PA", "{BRUTE}"... star = kept in STORY: SHORT.
        /// Text supports {TOKENS} (StoryText.Resolve). In non-blocking scripts Max/Percy/PA lines go to the PiP.</summary>
        public StoryScriptBuilder Line(string speakerToken, string text, bool star = false)
            => Add(new StoryStep { Kind = StoryStepKind.Line, Speaker = speakerToken, Text = text, Star = star, Face = Expression.Neutral });
        /// <summary>A line with a face for the speaker (Max's VillainStudio / an intern's puppet).</summary>
        public StoryScriptBuilder Line(string speakerToken, Expression face, string text, bool star = false)
            => Add(new StoryStep { Kind = StoryStepKind.Line, Speaker = speakerToken, Text = text, Star = star, Face = face });
        /// <summary>ME-TV / radio PiP line (StoryLine.Broadcast = true), also inside blocking scenes.</summary>
        public StoryScriptBuilder Broadcast(string speakerId, Expression face, string text)
            => Add(new StoryStep { Kind = StoryStepKind.Broadcast, Speaker = speakerId, Text = text, Face = face });
        public StoryScriptBuilder Broadcast(string speakerId, Expression face, string text, bool star)
            => Add(new StoryStep { Kind = StoryStepKind.Broadcast, Speaker = speakerId, Text = text, Face = face, Star = star });
        /// <summary>ME-TV channel switch cue (VillainStudio hat / channel bug): "ME NEWS", "ME WEATHER"... "off" ends full-screen TV.</summary>
        public StoryScriptBuilder Tv(string channel) => Add(new StoryStep { Kind = StoryStepKind.Tv, Text = channel });

        // ---- camera
        /// <summary>Cut/glide the CutsceneCamera to LevelMark <paramref name="mark"/> looking at <paramref name="lookAtMark"/>
        /// (null = keep direction) over <paramref name="seconds"/> (0 = cut). Non-waiting. Missing marks: no-op.</summary>
        public StoryScriptBuilder Shot(string mark, string lookAtMark, float fov, float seconds)
            => Add(new StoryStep { Kind = StoryStepKind.Shot, Text = mark, Text2 = lookAtMark, A = fov, B = seconds });
        public StoryScriptBuilder Shot(Vector3 worldPos, Vector3 worldLookAt, float fov, float seconds)
            => Add(new StoryStep { Kind = StoryStepKind.Shot, P0 = worldPos, P1 = worldLookAt, A = fov, B = seconds });
        /// <summary>Frame the whole team from the front of the active intern at <paramref name="distance"/> m.</summary>
        public StoryScriptBuilder ShotTeam(float distance, float fov, float seconds)
            => Add(new StoryStep { Kind = StoryStepKind.ShotTeam, A = fov, B = seconds, C = distance });
        /// <summary>Slow push-in / drift of the cutscene camera (meters per second along its forward).</summary>
        public StoryScriptBuilder CameraDrift(float metersPerSecond) => Add(new StoryStep { Kind = StoryStepKind.CameraDrift, A = metersPerSecond });

        // ---- timing / presentation
        public StoryScriptBuilder Wait(float s) => Add(new StoryStep { Kind = StoryStepKind.Wait, A = Mathf.Max(0f, s) });
        /// <summary>Fade to black over outS (waits), then fade back in over inS (non-waiting).</summary>
        public StoryScriptBuilder Fade(float outS, float inS) => Add(new StoryStep { Kind = StoryStepKind.Fade, A = outS, B = inS });
        public StoryScriptBuilder Music(MusicId id) => Add(new StoryStep { Kind = StoryStepKind.Music, Music = id });
        public StoryScriptBuilder Sfx(SfxId id) => Add(new StoryStep { Kind = StoryStepKind.Sfx, Sfx = id });
        public StoryScriptBuilder Banner(string t, string sub, BannerStyle s)
            => Add(new StoryStep { Kind = StoryStepKind.Banner, Text = t, Text2 = sub, Style = s });
        public StoryScriptBuilder Toast(string text) => Add(new StoryStep { Kind = StoryStepKind.Toast, Text = text });
        /// <summary>World color wave from a LevelMark (null = active intern) over seconds, base from -> to (§14).</summary>
        public StoryScriptBuilder WorldWave(string originMark, float seconds, float from, float to)
            => Add(new StoryStep { Kind = StoryStepKind.WorldWave, Text = originMark, A = seconds, B = from, C = to });
        public StoryScriptBuilder Card(string act, string title) => Add(new StoryStep { Kind = StoryStepKind.Card, Text = act, Text2 = title });
        public StoryScriptBuilder Objective(string text) => Add(new StoryStep { Kind = StoryStepKind.Objective, Text = text });

        // ---- actors
        /// <summary>Expression on a target: intern token ("{BRUTE}") = its puppet; anything else ("MAX", "PERCY") = StoryPlayer.Cue.</summary>
        public StoryScriptBuilder Face(string target, Expression e) => Add(new StoryStep { Kind = StoryStepKind.Face, Speaker = target, Face = e });
        public StoryScriptBuilder Gesture(string target, VisualAction a) => Add(new StoryStep { Kind = StoryStepKind.Gesture, Speaker = target, Action = a });
        /// <summary>Walk the intern of that archetype to a LevelMark (ExternalControl; teleports after 4 s). Non-waiting.</summary>
        public StoryScriptBuilder Move(Archetype who, string mark) => Add(new StoryStep { Kind = StoryStepKind.Move, Who = who, Text = mark });
        /// <summary>Villain puppet action ("CrownSlip", "OneHandClap", "Monologue", "Rage"...): StoryPlayer.Cue("puppet", "MAX", action).</summary>
        public StoryScriptBuilder Puppet(string villainAction) => Add(new StoryStep { Kind = StoryStepKind.Puppet, Speaker = "MAX", Text = villainAction });
        /// <summary>Scare cue (story_kit ScareDirector): StoryPlayer.Cue("scare", cueId) + GameEvents.RaiseStoryBeat(cueId).</summary>
        public StoryScriptBuilder Scare(string cueId) => Add(new StoryStep { Kind = StoryStepKind.Scare, Text = cueId });
        /// <summary>Raise GameEvents.StoryBeat(id) (props like TallyBoard / level code react). Essential: also runs when skipped.</summary>
        public StoryScriptBuilder Beat(string id) => Add(new StoryStep { Kind = StoryStepKind.Beat, Text = id });
        /// <summary>Arbitrary callback. Essential: also runs when skipped / STORY OFF.</summary>
        public StoryScriptBuilder Do(Action a) => Add(new StoryStep { Kind = StoryStepKind.Do, Callback = a });

        // ---- structure
        /// <summary>Run lanes at the same time; waits for all of them.</summary>
        public StoryScriptBuilder Parallel(params Action<StoryScriptBuilder>[] lanes)
        {
            var st = new StoryStep { Kind = StoryStepKind.Parallel, Lanes = new List<StoryStep>[lanes != null ? lanes.Length : 0] };
            for (int i = 0; i < st.Lanes.Length; i++)
            {
                st.Lanes[i] = new List<StoryStep>();
                lanes[i]?.Invoke(new StoryScriptBuilder(_s, st.Lanes[i]));
            }
            return Add(st);
        }

        /// <summary>Interactive prompt (C15 "WHO RULES TODAY?"): raises StoryPlayer.PromptRequested(text, actionId) and waits
        /// until StoryPlayer.CompletePrompt(actionId) (or StoryPlayer.PromptTimeout / AutoSkip).</summary>
        public StoryScriptBuilder Prompt(string text, string actionId)
            => Add(new StoryStep { Kind = StoryStepKind.Prompt, Text = text, Text2 = actionId });
    }
}
