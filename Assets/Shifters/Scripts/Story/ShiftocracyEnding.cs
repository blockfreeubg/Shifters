// =====================================================================================
//  SHIFTERS - ShiftocracyEnding.cs   [story]
//  C15 "WHO RULES TODAY? [RIGHT SHIFT]" (CAMPAIGN §12.4 / §24.6). Lives on the [STORY] root and waits for
//  the C15 Prompt step (PromptId). The pick goes through the real character switcher: the input context
//  is opened to Gameplay for the prompt so RIGHT SHIFT / 1-2-3 work; the intern who is active when the
//  switcher closes (or after PickTimeout, or Enter/Space) rules today. Then: decree per archetype (+ the
//  gunner's weapon line), the vista beat "c15.vista.<archetype>" (+ a color pulse), the founders' statue
//  (StoryKit.FoundersStatue at LevelMark "story.statue" when story_kit provides it; beat "c15.statue").
//  Bots / skip / STORY OFF: auto-pick (seeded by the team seed), no lines.
// =====================================================================================
using System;
using System.Reflection;
using UnityEngine;

namespace Shifters
{
    public sealed class ShiftocracyEnding : MonoBehaviour
    {
        public const string PromptId = "rule.pick";
        public const string StatueMark = "story.statue";
        public static float PickTimeout = 15f;
        public static ShiftocracyEnding Instance { get; private set; }
        /// <summary>(president, decree) after every pick (also auto-picks).</summary>
        public static event Action<ShifterCharacter, string> PresidentChosen;

        public ShifterCharacter President { get; private set; }
        public string PresidentName { get; private set; }
        public Archetype PresidentArchetype { get; private set; }
        public string Decree { get; private set; }
        public string GunDecree { get; private set; }
        public bool Picking => _picking;
        public GameObject Statue { get; private set; }

        bool _picking, _switched;
        float _t;
        InputContext _ctxBefore;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; PickTimeout = 15f; PresidentChosen = null; }

        void Awake() { Instance = this; }
        void OnDestroy() { if (Instance == this) Instance = null; }

        void OnEnable()
        {
            StoryPlayer.PromptRequested += OnPrompt;
            StoryPlayer.PromptAutoResolve += OnAuto;
            GameEvents.CharacterSwitched += OnSwitched;
        }

        void OnDisable()
        {
            StoryPlayer.PromptRequested -= OnPrompt;
            StoryPlayer.PromptAutoResolve -= OnAuto;
            GameEvents.CharacterSwitched -= OnSwitched;
            if (_picking) EndPickContext();
        }

        void OnPrompt(string text, string id)
        {
            if (id != PromptId) return;
            if (StoryPlayer.AutoSkip) { Finish(AutoPick(), true); return; }
            _picking = true; _switched = false; _t = 0f;
            _ctxBefore = ShiftersInput.Context;
            ShiftersInput.Context = InputContext.Gameplay;   // the switcher (RIGHT SHIFT, 1/2/3) needs Gameplay/Switcher
            GameEvents.RaisePrompt(PromptInfo.Make("RIGHT SHIFT", string.IsNullOrEmpty(text) ? "WHO RULES TODAY?" : text));
        }

        void OnAuto(string id)
        {
            if (id != PromptId) return;
            if (_picking) EndPickContext();
            ApplyPick(_picking ? Active() ?? AutoPick() : AutoPick(), false);
            _picking = false;
        }

        void OnSwitched(ShifterCharacter from, ShifterCharacter to) { if (_picking) _switched = true; }

        void Update()
        {
            if (!_picking) return;
            _t += ShiftersCamera.UnscaledDeltaTime * Mathf.Max(0.01f, StoryPlayer.TimeScale);
            bool wheelOpen = ShiftersInput.Context == InputContext.Switcher;
            if (wheelOpen) return;
            if (_switched || _t >= PickTimeout || (_t > 0.5f && StoryBridge.AdvancePressed)) Finish(Active() ?? AutoPick(), false);
        }

        static ShifterCharacter Active() { var cm = CharacterManager.Instance; return cm != null ? cm.Active : null; }

        static ShifterCharacter AutoPick()
        {
            var cm = CharacterManager.Instance;
            if (cm == null || cm.Team == null || cm.Team.Count == 0) return null;
            var gs = GameSession.Instance;
            int seed = gs != null ? gs.Seed : 0;
            int n = cm.Team.Count;
            for (int k = 0; k < n; k++)
            {
                var c = cm.Team[(((seed % n) + n) % n + k) % n];
                if (c != null && c.State != CharacterState.Lost) return c;
            }
            return cm.Team[0];
        }

        void EndPickContext()
        {
            GameEvents.RaisePrompt(null);
            if (ShiftersInput.Context == InputContext.Gameplay || ShiftersInput.Context == InputContext.Switcher)
                ShiftersInput.Context = _ctxBefore == InputContext.Cutscene || StoryPlayer.Instance == null || StoryPlayer.Instance.IsBlocking
                    ? InputContext.Cutscene : _ctxBefore;
        }

        void Finish(ShifterCharacter pick, bool silent)
        {
            if (_picking) EndPickContext();
            _picking = false;
            ApplyPick(pick, true);
            var sp = StoryPlayer.Instance;
            if (sp == null || !sp.PromptOpen(PromptId)) return;
            var decree = Decree; var gun = GunDecree; var gunner = StoryText.FindGunner();
            var arch = PresidentArchetype;
            sp.CompletePrompt(PromptId, b =>
            {
                b.Sfx(SfxId.Stamp).Banner("PRESIDENT OF THE DAY", "{PRESIDENT}", BannerStyle.Story);
                b.Gesture("{PRESIDENT}", VisualAction.Celebrate);
                b.Line("{PRESIDENT}", Expression.Happy, decree, true);
                if (!string.IsNullOrEmpty(gun))
                    b.Line(gunner != null ? "{GUNNER}" : "{PRESIDENT}", Expression.Happy, gun, true);
                b.Beat("c15.vista." + arch.ToString().ToLowerInvariant()).Sfx(SfxId.ColorBurst);
            });
        }

        void ApplyPick(ShifterCharacter pick, bool withLines)
        {
            President = pick;
            PresidentArchetype = pick != null ? pick.Archetype : Archetype.Brainiac;
            PresidentName = pick != null ? pick.DisplayName.ToUpperInvariant() : StoryText.InternName(PresidentArchetype);
            StoryText.President = PresidentName;
            var gs = GameSession.Instance;
            int seed = gs != null ? gs.Seed : 0;
            int idx = Mathf.Abs(seed * 7 + DateTime.Now.DayOfYear);
            Decree = StoryText.DecreeFor(PresidentArchetype, idx);
            var gunner = StoryText.FindGunner();
            GunDecree = gunner != null && gunner.Profile != null ? StoryText.GunDecree(gunner.Profile.Weapon) : null;
            if (!withLines) GameEvents.RaiseStoryBeat("c15.vista." + PresidentArchetype.ToString().ToLowerInvariant());
            SpawnFoundersStatue();
            try { PresidentChosen?.Invoke(pick, Decree); } catch (Exception e) { Debug.LogError("[Story] PresidentChosen: " + e); }
        }

        /// <summary>Founders' statue of the current team (StoryKit.FoundersStatue, story_kit) at LevelMark "story.statue".</summary>
        public GameObject SpawnFoundersStatue()
        {
            if (Statue != null) return Statue;
            if (!LevelMark.Exists(StatueMark)) return null;
            var mark = LevelMark.Find(StatueMark);
            try
            {
                Statue = StoryKit.FoundersStatue(mark.transform, Vector3.zero, null);
            }
            catch (Exception e) { Debug.LogWarning("[Story] FoundersStatue: " + e.Message); }
            return Statue;
        }
    }
}
