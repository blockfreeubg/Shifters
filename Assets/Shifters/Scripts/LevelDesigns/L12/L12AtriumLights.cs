// =====================================================================================
//  SHIFTERS - L12AtriumLights.cs   [SHIFT 11 UNMASKED - the three lights + lit zones]
//  Geometric lit zones (LightRegistry volumes, no real lights): always-lit islands, the RACE LIGHTS (window + 2 s),
//  the ALCOVE (breaker ON) and the three FLOODS W / N / E (latched PuzzleSignals, so P2 / RestorePhase can set them).
//  Each new flood: emissive visuals, ambient step (0.15 / 0.30 / 0.45 / 0.80), WorldColor burst, beat, one Custom boss hit.
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public sealed class L12AtriumLights : MonoBehaviour
    {
        public L12BoogeymanBoss Boss;
        public Lever Crank, Breaker;
        public Terminal Fuse;
        public TimedButton Race;
        public PuzzleSignal FloodW, FloodN, FloodE;
        public GameObject VisW, VisN, VisE, VisRace, VisAlcove;
        public Transform[] AlwaysKeys = new Transform[0];
        public Vector3[] AlwaysSizes = new Vector3[0];
        public Transform KeyW, KeyN, KeyE, KeyRace, KeyAlcove;

        public static readonly Vector3 FloodSize = new Vector3(10f, 5.5f, 30f);
        public static readonly Vector3 RaceSize = new Vector3(32f, 5.5f, 4f);
        public static readonly Vector3 AlcoveSize = new Vector3(4f, 5.5f, 4f);
        static readonly float[] Ambient = { 0.15f, 0.30f, 0.45f, 0.80f };
        static readonly string[] Beat = { "ch11.lightW", "ch11.lightN", "ch11.lightE" };
        static readonly string[] Pa = { "GENERATOR ONLINE. PLEASE STOP SCREAMING IN THE WEST WING.", "BREAKER RESET. WHOEVER DID THAT: SLOW DOWN.", "FUSE REPLACED. NOTE: ADULT SUPERVISION REQUIRED." };

        readonly bool[] _was = new bool[3];
        bool _race, _alcove, _quiet;
        float _raceUntil = -99f;
        int _pending, _shownCount = -1;

        public int LitCount => (FloodW != null && FloodW.IsOn ? 1 : 0) + (FloodN != null && FloodN.IsOn ? 1 : 0) + (FloodE != null && FloodE.IsOn ? 1 : 0);
        public string BarText => "LIGHTS ON " + LitCount + "/3";
        public bool RaceLit => _race;

        void Start()
        {
            for (int i = 0; i < AlwaysKeys.Length && i < AlwaysSizes.Length; i++)
                if (AlwaysKeys[i] != null) LightRegistry.RegisterVolume(AlwaysKeys[i], Vector3.zero, AlwaysSizes[i]);
            SetVis(VisW, false); SetVis(VisN, false); SetVis(VisE, false); SetVis(VisRace, false); SetVis(VisAlcove, false);
            ApplyAmbient(0.5f);
        }

        void OnDestroy()
        {
            for (int i = 0; i < AlwaysKeys.Length; i++) if (AlwaysKeys[i] != null) LightRegistry.UnregisterVolume(AlwaysKeys[i]);
            Unreg(KeyW); Unreg(KeyN); Unreg(KeyE); Unreg(KeyRace); Unreg(KeyAlcove);
        }

        static void Unreg(Transform t) { if (t != null) LightRegistry.UnregisterVolume(t); }
        static void SetVis(GameObject g, bool on) { if (g != null && g.activeSelf != on) g.SetActive(on); }

        public void OnBossIntro() { ApplyAmbient(1f); }

        /// <summary>P2 start / C2 restore: all three floods ON, quietly (no beats, no hits).</summary>
        public void ForceAllOn()
        {
            _quiet = true;
            if (FloodW != null) FloodW.Set(true);
            if (FloodN != null) FloodN.Set(true);
            if (FloodE != null) FloodE.Set(true);
            _pending = 0;
            Update();
            _quiet = false;
        }

        void Update()
        {
            if (Crank != null && Crank.IsOn && FloodW != null && !FloodW.IsOn) FloodW.Set(true);
            if (Breaker != null && Breaker.IsOn && FloodN != null && !FloodN.IsOn) FloodN.Set(true);
            if (Fuse != null && Fuse.IsOn && FloodE != null && !FloodE.IsOn) FloodE.Set(true);

            var sig = new[] { FloodW, FloodN, FloodE };
            var vis = new[] { VisW, VisN, VisE };
            var key = new[] { KeyW, KeyN, KeyE };
            for (int k = 0; k < 3; k++)
            {
                bool on = sig[k] != null && sig[k].IsOn;
                if (on == _was[k]) continue;
                _was[k] = on;
                SetVis(vis[k], on);
                if (key[k] != null) { if (on) LightRegistry.RegisterVolume(key[k], Vector3.zero, FloodSize); else LightRegistry.UnregisterVolume(key[k]); }
                if (!on) continue;
                var c = key[k] != null ? key[k].position : transform.position;
                WorldColor.AddBurst(new Vector3(c.x, 0f, c.z), 12f, 6f);
                if (_quiet) continue;
                Sfx.At(SfxId.PuzzleSolved, c, 1f);
                Sfx.At(SfxId.ElevatorHum, c, 0.6f, 0.8f);
                Fx.FloatingText(new Vector3(c.x, 5.5f, c.z), Pa[k], UIColors.AccentYellow, 2.2f);
                GameEvents.RaiseStoryBeat(Beat[k]);
                _pending++;
            }

            // RACE LIGHTS: while the window runs and 2.0 s after ("THE LIGHTS ARE SLOW. LIKE MANAGEMENT.")
            bool window = Race != null && Race.IsOn;
            if (window) _raceUntil = Time.time + 2f;
            bool race = window || Time.time < _raceUntil;
            if (race != _race)
            {
                _race = race;
                SetVis(VisRace, race);
                if (KeyRace != null) { if (race) LightRegistry.RegisterVolume(KeyRace, Vector3.zero, RaceSize); else LightRegistry.UnregisterVolume(KeyRace); }
                ScareDirector.SetWindow("race", race);
            }
            bool alcove = Breaker != null && Breaker.IsOn;
            if (alcove != _alcove)
            {
                _alcove = alcove;
                SetVis(VisAlcove, alcove);
                if (KeyAlcove != null) { if (alcove) LightRegistry.RegisterVolume(KeyAlcove, Vector3.zero, AlcoveSize); else LightRegistry.UnregisterVolume(KeyAlcove); }
            }

            int n = LitCount;
            if (n != _shownCount) { _shownCount = n; ApplyAmbient(1.5f); if (Boss != null && Boss.Phase == 0) { Boss.BarLabel = BarText; Boss.RaiseStatus(); GameEvents.RaiseObjective(n < 3 ? BarText + " — STICK TOGETHER" : "LIGHTS ON!"); } }

            // one Custom hit per new light (retried through the 0.5 s hit cooldown and the intro)
            if (_pending > 0 && Boss != null)
            {
                if (Boss.Phase != 0 || Boss.IsDefeated) _pending = 0;
                else if (Boss.IsFighting && Boss.TryHit(BossHitKind.Custom, transform.position, null)) _pending--;
            }
        }

        void ApplyAmbient(float seconds)
        {
            int n = Mathf.Clamp(LitCount, 0, 3);
            float a = Ambient[n];
            LevelLighting.SetOverride(0.25f + a * 0.9f, a, seconds);
        }
    }
}
