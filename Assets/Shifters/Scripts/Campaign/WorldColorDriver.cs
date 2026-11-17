// =====================================================================================
//  SHIFTERS - WorldColorDriver.cs   [owned by the CAMPAIGN agent]
//  Campaign-side driver of rendering's WorldColor (CAMPAIGN.md §14):
//    campaign level : base = Floor + (1 - Floor) * meter   (Floor 0.40; meter §6.1)
//    main menu      : the same formula on the saved meter (grey stage that gets more colorful
//                     with progress); 1 once the campaign has been completed
//    Free Play / OVERTIME / classic levels / COLOR SHIFT EFFECT off : 1 (the levels as built)
//  Re-applied on LevelStarted, scene loads, completions and SettingsChanged. Raises
//  GameEvents.WorldColorChanged(base) when the value changes. Attached by CampaignDirector.
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public sealed class WorldColorDriver : MonoBehaviour
    {
        public static WorldColorDriver Instance { get; private set; }
        public static float Floor = 0.40f;
        public const float FloorMin = 0.35f, FloorMax = 0.55f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; Floor = 0.40f; }

        /// <summary>Floor + (1 - Floor) * meter. Meter &gt; 1 oversaturates (ending party).</summary>
        public static float BaseFor(float meter) => Floor + (1f - Floor) * Mathf.Max(0f, meter);

        /// <summary>The base last sent to WorldColor (1 = full color).</summary>
        public float CurrentBase { get; private set; } = 1f;
        /// <summary>Whether the last Apply() wanted rendering's effect ON (WorldColor.Enabled): true whenever the
        /// target base differs from 1 (campaign chapters, grey menu stage, oversaturated ending), false in Free Play /
        /// OVERTIME / classic / COLOR SHIFT EFFECT off / completed menu. Recorded even while CampaignDeps.WorldColorOverride is set.</summary>
        public bool EffectEnabled { get; private set; }
        bool _sent;

        /// <summary>True when a base needs the effect on (base != 1).</summary>
        public static bool WantsEffect(float b) => Mathf.Abs(b - 1f) > 0.001f;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            GameEvents.SettingsChanged += Apply;
        }

        void Start() => Apply();

        void OnDestroy()
        {
            if (Instance == this && EffectEnabled) CampaignDeps.SetWorldColorEnabled(false);
            GameEvents.SettingsChanged -= Apply;
            if (Instance == this) Instance = null;
        }

        /// <summary>The base the current state wants (pure; no side effects).</summary>
        public static float TargetBase()
        {
            var gs = GameSession.Instance;
            if (gs != null && gs.Settings != null && !gs.Settings.ColorShiftFx) return 1f;
            var d = CampaignDirector.Instance;
            if (d == null) return 1f;
            if (d.IsCampaign) return BaseFor(d.Meter);
            bool inMenu = gs == null || gs.LevelIndex < 0;
            if (inMenu && UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == LevelCatalog.MainMenuScene)
                return d.CampaignCompleted ? 1f : BaseFor(d.SavedMeter);
            return 1f;
        }

        public void Apply()
        {
            float b = TargetBase();
            EffectEnabled = WantsEffect(b);
            CampaignDeps.SetWorldColorEnabled(EffectEnabled);   // before SetBase (kitfix integrator2-1)
            if (_sent && Mathf.Approximately(b, CurrentBase)) { CampaignDeps.SetWorldColorBase(b); return; }
            _sent = true;
            CurrentBase = b;
            CampaignDeps.SetWorldColorBase(b);
            GameEvents.RaiseWorldColorChanged(b);
        }
    }
}
