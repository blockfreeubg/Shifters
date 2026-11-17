// =====================================================================================
//  SHIFTERS - L9ChaseDirector.cs   [LEVEL 9 - THE GETAWAY]
//  Glue around the bosses kit's ChaseTrack:
//    * story beats ch08.board (first departure) and ch08.st1/st2/st3 (station arrivals);
//    * the tram's CONSOLE only accepts a hack while docked at the TOLL GATE (its colliders are off elsewhere);
//    * PORKCHOP (the pursuer rig) is hidden while it is still "under" the vault platform (z < EmergeZ) and
//      SURFACES out of the coin river with a splash; gait / red eyes / "trips on a coin" wobble;
//    * Restart Checkpoint past a station restores what the team already did (bricks gone, toll paid) so the
//      pig never clips through an intact wall behind you, and the pig stays hidden in the boss arena (C4+).
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public sealed class L9ChaseDirector : MonoBehaviour
    {
        public ChaseTrack Track;
        [Tooltip("Child of the pursuer rig (hidden while submerged).")] public Transform PigModel;
        public PorkchopModel Pig;
        public Breakable Wall;
        public Terminal Console;
        [Tooltip("Shown on the console while it is out of toll range.")] public GameObject ConsoleIdleSign;
        public L9Signal TollRestore;
        public float EmergeZ = 2.5f;
        [Tooltip("First boss checkpoint index: from here on the chase is over.")] public int ArenaCheckpoint = 4;

        public bool Ready { get; private set; }
        public int RestoredCheckpoint { get; private set; }
        public bool ConsoleLive { get; private set; }
        public int Beats { get; private set; }

        bool _wasStarted, _pigShown, _consoleInit;
        int _frames = 3;
        float _gait;
        Vector3 _lastPig;
        Collider[] _consoleCols;

        void Start()
        {
            if (Track != null) { Track.StationReached += OnStation; Track.Arrived += OnArrived; }
            if (Console != null) _consoleCols = Console.GetComponentsInChildren<Collider>(true);
            SetConsoleLive(false);
            if (PigModel != null) PigModel.gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            if (Track != null) { Track.StationReached -= OnStation; Track.Arrived -= OnArrived; }
        }

        void Update()
        {
            if (!Ready)
            {
                var lm = LevelManager.Instance;
                if (lm != null && !lm.IsStarted) return;
                if (--_frames > 0) return;
                Ready = true;
                var mgr = CheckpointManager.Instance;
                RestoredCheckpoint = mgr != null ? mgr.CurrentIndex : 0;
                if (RestoredCheckpoint >= 2 && Wall != null && !Wall.IsBroken)
                {
                    if (Wall.Intact != null) Wall.Intact.SetActive(false);
                    if (Wall.Solid != null) Wall.Solid.enabled = false;
                }
                if (RestoredCheckpoint >= 3 && TollRestore != null) TollRestore.Set(true);
            }
            if (Track == null) return;
            if (Track.Started && !_wasStarted)
            {
                _wasStarted = true;
                if (Track.StationIndex < 0) Beat("ch08.board");
            }
            bool live = Console != null && !Console.IsOn && Track.Docked && Track.StationIndex == 1 && !Track.Finished;
            SetConsoleLive(live);
        }

        void LateUpdate()
        {
            if (PigModel == null || Track == null || Track.Pursuer == null) return;
            var p = Track.Pursuer.position;
            bool show = Ready && !Track.Finished && RestoredCheckpoint < ArenaCheckpoint && p.z > EmergeZ;
            if (show != _pigShown)
            {
                _pigShown = show;
                PigModel.gameObject.SetActive(show);
                _lastPig = p;
                if (show)
                {
                    Fx.CoinShower(p + Vector3.up * 0.5f, 60, 2f);
                    Sfx.At(SfxId.Splash, p, 1f, 0.8f);
                    Sfx.At(SfxId.Oink, p, 1f, 0.9f);
                    GameEvents.RaiseToast("PORKCHOP SURFACES! KEEP MOVING!", UIColors.Danger);
                }
            }
            if (!show || Pig == null) return;
            float dt = Mathf.Max(1e-4f, Time.deltaTime);
            float v = (p - _lastPig).magnitude / dt;
            _lastPig = p;
            _gait += v * dt * 2.4f;
            Pig.SetGait(_gait, Mathf.Clamp(v * 10f, 0f, 30f));
            Pig.SetEyesRed(Track.Gap < 16f);
            PigModel.localRotation = Track.PursuerTripped
                ? Quaternion.Euler(Mathf.Sin(Time.time * 7f) * 10f, 0f, 22f * Mathf.Sin(Time.time * 9f))
                : Quaternion.identity;
        }

        void OnStation(ChaseTrack t)
        {
            switch (t.StationIndex)
            {
                case 0: Beat("ch08.st1"); break;
                case 1: Beat("ch08.st2"); break;
                case 2: Beat("ch08.st3"); break;
            }
        }

        void OnArrived(ChaseTrack t)
        {
            GameEvents.RaiseToast("END OF THE LINE. MIND THE HAM.", UIColors.AccentYellow);
        }

        void Beat(string id) { Beats++; GameEvents.RaiseStoryBeat(id); }

        void SetConsoleLive(bool on)
        {
            if (_consoleInit && on == ConsoleLive) return;
            _consoleInit = true;
            ConsoleLive = on;
            if (_consoleCols != null) for (int i = 0; i < _consoleCols.Length; i++) if (_consoleCols[i] != null) _consoleCols[i].enabled = on || (Console != null && Console.IsOn);
            if (ConsoleIdleSign != null) ConsoleIdleSign.SetActive(!on && (Console == null || !Console.IsOn));
            if (on) GameEvents.RaiseToast("TOLL CONSOLE ONLINE: BIG BRAINS ONLY", UIColors.AccentYellow);
        }
    }
}
