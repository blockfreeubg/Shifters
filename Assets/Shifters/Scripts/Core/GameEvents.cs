// =====================================================================================
//  SHIFTERS - GameEvents.cs   [CONTRACT FILE]
//  Global event bus. Gameplay code RAISES, UI/Audio/FX LISTEN. Gameplay never calls the UI directly:
//  e.g. a pressure plate raises PuzzleSolved, a hazard raises Banner, the HUD listens.
//  Always unsubscribe in OnDisable/OnDestroy. All Raise* methods are null-safe.
//  Call GameEvents.ClearAll() only on full game shutdown (never between scenes).
// =====================================================================================
using System;
using UnityEngine;

namespace Shifters
{
    public static class GameEvents
    {
        // ---------------------------------------------------------------- Team / characters
        /// <summary>(previous, next). previous may be null on the first activation.</summary>
        public static event Action<ShifterCharacter, ShifterCharacter> CharacterSwitched;
        public static event Action<ShifterCharacter> CharacterStateChanged;      // Alive/Downed/Captured/Lost changed
        public static event Action<ShifterCharacter> CharacterConditionChanged;  // Normal/Carried/Thrown/Bridging...
        public static event Action<ShifterCharacter, float> CharacterDamaged;    // amount
        public static event Action<ShifterCharacter> CharacterHealthChanged;
        public static event Action TeamSpawned;                                  // CharacterManager finished spawning
        public static event Action<TeamAIMode> AIModeChanged;
        public static event Action TeamWiped;                                    // no selectable characters remain
        public static event Action<ShifterCharacter, string> Speech;             // speech bubble over a character

        // ---------------------------------------------------------------- Switcher / perspective / camera
        public static event Action<bool> SwitcherOpenChanged;
        public static event Action<int> SwitcherSelectionChanged;               // highlighted slot (-1 none)
        public static event Action<PerspectiveMask, ShifterCharacter> PerspectiveChanged;
        public static event Action<bool> SwitchTransitionChanged;               // camera transition running

        // ---------------------------------------------------------------- UI feedback
        public static event Action<string, string, BannerStyle> Banner;          // title, subtitle, style
        public static event Action<string, Color> Toast;
        public static event Action<PromptInfo?> Prompt;                          // null clears
        public static event Action<string> ObjectiveChanged;
        public static event Action<TutorialMessage> TutorialShow;
        public static event Action TutorialHide;
        public static event Action<string> Hint;
        public static event Action<float, string> Progress;                      // 0..1 progress bar (hacking); <0 hides
        public static event Action<bool> CrosshairChanged;
        public static event Action<float> TimerChanged;                          // timed-door countdown seconds (<=0 hides)

        // ---------------------------------------------------------------- Puzzles / level flow
        public static event Action<string> PuzzleSolved;                         // display text or id
        public static event Action<int> CheckpointReached;
        public static event Action<LevelResult> LevelCompleted;
        public static event Action LevelStarted;
        public static event Action<bool> PauseChanged;
        public static event Action<NoiseEvent> Noise;                            // enemies listen
        public static event Action SettingsChanged;

        // ---------------------------------------------------------------- Campaign wave (W0, CAMPAIGN §19)
        /// <summary>Story beat id fired by level scripts / triggers / bosses ("ch14.vote", "c08.lights"...). Story (VillainBarks,
        /// StoryPlayer), story kit props (TallyBoard) and the campaign listen. Ids are lower-case dotted strings.</summary>
        public static event Action<string> StoryBeat;
        /// <summary>A dialogue / subtitle line appears (StoryPlayer, VillainBarks). ui_hud's DialogueBox / MeTvPip draw it.</summary>
        public static event Action<StoryLine> StoryLineShown;
        /// <summary>The current line is gone (skipped, timed out, script ended).</summary>
        public static event Action StoryLineHidden;
        /// <summary>true while a BLOCKING cutscene runs (letterbox, HUD suppressed, gameplay input off), false when it ends.</summary>
        public static event Action<bool> CutsceneChanged;
        /// <summary>(on, speakerId): a ME-TV / radio broadcast picture-in-picture starts (on) or ends; speakerId e.g. "MAX".</summary>
        public static event Action<bool, string> BroadcastChanged;
        /// <summary>Boss bar state (BossBase raises it on every change; Visible = false hides the bar).</summary>
        public static event Action<BossStatus> BossChanged;
        /// <summary>Chase meter state (ChaseTrack; Visible = false hides the meter).</summary>
        public static event Action<ChaseStatus> ChaseChanged;
        /// <summary>Like PuzzleSolved but with the solving element's world position (SignalSource raises both on the first ON
        /// with a SolvedMessage). The campaign drops a world-color burst there.</summary>
        public static event Action<string, Vector3> PuzzleSolvedAt;
        /// <summary>The campaign's world-color base changed (0 = grey .. 1 = full color, &gt; 1 = over-saturated finale).</summary>
        public static event Action<float> WorldColorChanged;
        /// <summary>An evidence exhibit ("A"/"B"/"C") was awarded (ui_hud's exhibit toast, CASE FILE).</summary>
        public static event Action<string> ExhibitAcquired;
        /// <summary>(act, title): show the chapter / act card ("ACT II", "THE FEDERAL COLOR RESERVE").</summary>
        public static event Action<string, string> ChapterCard;

        // ---------------------------------------------------------------- Raise helpers
        public static void RaiseCharacterSwitched(ShifterCharacter prev, ShifterCharacter next) => CharacterSwitched?.Invoke(prev, next);
        public static void RaiseCharacterStateChanged(ShifterCharacter c) => CharacterStateChanged?.Invoke(c);
        public static void RaiseCharacterConditionChanged(ShifterCharacter c) => CharacterConditionChanged?.Invoke(c);
        public static void RaiseCharacterDamaged(ShifterCharacter c, float amount) => CharacterDamaged?.Invoke(c, amount);
        public static void RaiseCharacterHealthChanged(ShifterCharacter c) => CharacterHealthChanged?.Invoke(c);
        public static void RaiseTeamSpawned() => TeamSpawned?.Invoke();
        public static void RaiseAIModeChanged(TeamAIMode m) => AIModeChanged?.Invoke(m);
        public static void RaiseTeamWiped() => TeamWiped?.Invoke();
        public static void RaiseSpeech(ShifterCharacter c, string text) => Speech?.Invoke(c, text);

        public static void RaiseSwitcherOpenChanged(bool open) => SwitcherOpenChanged?.Invoke(open);
        public static void RaiseSwitcherSelectionChanged(int slot) => SwitcherSelectionChanged?.Invoke(slot);
        public static void RaisePerspectiveChanged(PerspectiveMask m, ShifterCharacter viewer) => PerspectiveChanged?.Invoke(m, viewer);
        public static void RaiseSwitchTransitionChanged(bool running) => SwitchTransitionChanged?.Invoke(running);

        public static void RaiseBanner(string title, string subtitle = null, BannerStyle style = BannerStyle.Info) => Banner?.Invoke(title, subtitle, style);
        public static void RaiseToast(string text) => Toast?.Invoke(text, UIColors.Text);
        public static void RaiseToast(string text, Color color) => Toast?.Invoke(text, color);
        public static void RaisePrompt(PromptInfo? p) => Prompt?.Invoke(p);
        public static void RaiseObjective(string text) => ObjectiveChanged?.Invoke(text);
        public static void RaiseTutorialShow(TutorialMessage m) => TutorialShow?.Invoke(m);
        public static void RaiseTutorialHide() => TutorialHide?.Invoke();
        public static void RaiseHint(string text) => Hint?.Invoke(text);
        public static void RaiseProgress(float t01, string label = null) => Progress?.Invoke(t01, label);
        public static void RaiseCrosshair(bool visible) => CrosshairChanged?.Invoke(visible);
        public static void RaiseTimer(float seconds) => TimerChanged?.Invoke(seconds);

        public static void RaisePuzzleSolved(string text) => PuzzleSolved?.Invoke(text);
        public static void RaiseCheckpointReached(int index) => CheckpointReached?.Invoke(index);
        public static void RaiseLevelCompleted(LevelResult r) => LevelCompleted?.Invoke(r);
        public static void RaiseLevelStarted() => LevelStarted?.Invoke();
        public static void RaisePauseChanged(bool paused) => PauseChanged?.Invoke(paused);
        public static void RaiseNoise(NoiseEvent e) => Noise?.Invoke(e);
        public static void RaiseNoise(Vector3 pos, float radius, ShifterCharacter source = null, float priority = 1f)
            => Noise?.Invoke(new NoiseEvent { Position = pos, Radius = radius, Source = source, Priority = priority });
        public static void RaiseSettingsChanged() => SettingsChanged?.Invoke();

        // campaign wave (W0)
        public static void RaiseStoryBeat(string id) => StoryBeat?.Invoke(id);
        public static void RaiseStoryLineShown(StoryLine line) => StoryLineShown?.Invoke(line);
        public static void RaiseStoryLineHidden() => StoryLineHidden?.Invoke();
        public static void RaiseCutsceneChanged(bool blocking) => CutsceneChanged?.Invoke(blocking);
        public static void RaiseBroadcastChanged(bool on, string speakerId = null) => BroadcastChanged?.Invoke(on, speakerId);
        public static void RaiseBossChanged(BossStatus status) => BossChanged?.Invoke(status);
        public static void RaiseChaseChanged(ChaseStatus status) => ChaseChanged?.Invoke(status);
        public static void RaisePuzzleSolvedAt(string text, Vector3 position) => PuzzleSolvedAt?.Invoke(text, position);
        public static void RaiseWorldColorChanged(float base01) => WorldColorChanged?.Invoke(base01);
        public static void RaiseExhibitAcquired(string id) => ExhibitAcquired?.Invoke(id);
        public static void RaiseChapterCard(string act, string title) => ChapterCard?.Invoke(act, title);

        /// <summary>Full reset (domain-reload-less play mode safety). Called by GameBootstrap at SubsystemRegistration.</summary>
        public static void ClearAll()
        {
            CharacterSwitched = null; CharacterStateChanged = null; CharacterConditionChanged = null; CharacterDamaged = null;
            CharacterHealthChanged = null; TeamSpawned = null; AIModeChanged = null; TeamWiped = null; Speech = null;
            SwitcherOpenChanged = null; SwitcherSelectionChanged = null; PerspectiveChanged = null; SwitchTransitionChanged = null;
            Banner = null; Toast = null; Prompt = null; ObjectiveChanged = null; TutorialShow = null; TutorialHide = null; Hint = null;
            Progress = null; CrosshairChanged = null; TimerChanged = null; PuzzleSolved = null; CheckpointReached = null;
            LevelCompleted = null; LevelStarted = null; PauseChanged = null; Noise = null; SettingsChanged = null;
            StoryBeat = null; StoryLineShown = null; StoryLineHidden = null; CutsceneChanged = null; BroadcastChanged = null;
            BossChanged = null; ChaseChanged = null; PuzzleSolvedAt = null; WorldColorChanged = null; ExhibitAcquired = null;
            ChapterCard = null;
        }
    }

    /// <summary>Shared UI/brand colors (used by gameplay for toasts, by UI for everything). [CONTRACT]</summary>
    public static class UIColors
    {
        public static readonly Color Background = Hex(0x0E1116, 0.94f);
        public static readonly Color Panel = Hex(0x161B22, 0.96f);
        public static readonly Color PanelLight = Hex(0x222A35);
        public static readonly Color Border = Hex(0x2A313C);
        public static readonly Color Accent = Hex(0xFF8A1F);        // signature orange
        public static readonly Color AccentYellow = Hex(0xFFC93C);  // signature yellow
        public static readonly Color Text = Hex(0xF2F4F8);
        public static readonly Color TextMuted = Hex(0x9AA4B2);
        public static readonly Color Danger = Hex(0xFF4D5E);
        public static readonly Color Success = Hex(0x3DDC97);
        public static readonly Color Info = Hex(0x3DE0FF);
        public static readonly Color Strength = Hex(0xFF8A3D);
        public static readonly Color Speed = Hex(0x3DE0FF);
        public static readonly Color Intellect = Hex(0xB07CFF);
        public static readonly Color Weapon = Hex(0xFF4D5E);

        public static Color ForPerspective(PerspectiveMask m) =>
            (m & PerspectiveMask.Strength) != 0 ? Strength :
            (m & PerspectiveMask.Speed) != 0 ? Speed :
            (m & PerspectiveMask.Intellect) != 0 ? Intellect :
            (m & PerspectiveMask.Weapon) != 0 ? Weapon : Text;

        public static Color Hex(int rgb, float a = 1f) =>
            new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, a);
    }
}
