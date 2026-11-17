// =====================================================================================
//  SHIFTERS - ChaseTrack.cs   [BOSSES module - CH08 / O7 tram chase, LEVELS2 CH08]
//  TRAM (a kit MovingPlatform; this component drives its MoverBody, the platform's own motion is
//  disabled) runs along TrackPoints (LOCAL to this transform) at TramSpeed, docking at each station
//  until its Clear source is ON. Power = AND(ALL ABOARD, station CLEAR); ALL ABOARD = every non-Lost,
//  non-Captured intern on the deck (Downed ones must be carried aboard).
//  PURSUER (optional Transform, kinematic) follows on the same track at PursuerSpeed: the gap starts at
//  StartGap, grows while the tram moves (cap MaxGap), shrinks while it waits. Rubber band: the first time at
//  each station a gap &lt; RubberBandGap trips the pursuer for TripSeconds. Gap 0 = BUSTED -> BustedDelay ->
//  GameSession.RestartFromCheckpoint(); after the reload the track restores at the station whose
//  CheckpointIndex was reached (tram docked there, gap = StartGap). Raises GameEvents.ChaseChanged.
// =====================================================================================
using System;
using UnityEngine;

namespace Shifters
{
    public sealed class ChaseTrack : MonoBehaviour, IResettable
    {
        public MovingPlatform Tram;
        [Tooltip("Track polyline, LOCAL to this transform. Point 0 = the dock; the last point = the end.")] public Vector3[] TrackPoints = new Vector3[0];
        public ChaseStation[] Stations = new ChaseStation[0];
        public float TramSpeed = 6f, PursuerSpeed = 2.5f, StartGap = 40f, MaxGap = 45f;
        [Tooltip("Optional visual of the pursuer (moved along the track behind the tram).")] public Transform Pursuer;
        [Tooltip("Deck volume (local to the tram body; centre at the deck top) used for ALL ABOARD.")] public Vector3 DeckSize = new Vector3(4f, 3f, 7f);
        public float BustedDelay = 2f, RubberBandGap = 12f, TripSeconds = 5f;
        [Tooltip("Start the chase the first time everyone is aboard (else call StartChase).")] public bool AutoStart = true;
        public string BustedBeat = "", TripText = "PORKCHOP TRIPS ON A COIN!";
        /// <summary>Tests/levels: replaces GameSession.RestartFromCheckpoint after BUSTED (null = default).</summary>
        [NonSerialized] public Action<ChaseTrack> RestartOverride;

        public float Gap => _dist - _pursuerDist;
        /// <summary>Last station the tram docked at (-1 = still at the dock).</summary>
        public int StationIndex { get; private set; } = -1;
        public bool AllAboard { get; private set; }
        public bool Busted { get; private set; }
        public bool Started { get; private set; }
        public bool Finished { get; private set; }
        public bool Docked { get; private set; } = true;
        public bool Moving { get; private set; }
        public bool PursuerTripped => _trip > 0f;
        public float TramDistance => _dist;
        public float TrackLength => _cum != null && _cum.Length > 0 ? _cum[_cum.Length - 1] : 0f;
        public int BustCount { get; private set; }
        /// <summary>True while docked at a station whose Clear source is still off.</summary>
        public bool WaitingForClear => Docked && StationIndex >= 0 && StationIndex < Stations.Length && Stations[StationIndex].Clear != null && !Stations[StationIndex].Clear.IsOn;

        public event Action<ChaseTrack> Arrived, BustedEvent, StationReached;

        float[] _cum;
        float _dist, _pursuerDist, _trip, _bustTimer, _statusTimer;
        bool[] _tripUsed;
        int _startFrames = 2;
        ChaseStatus _last;

        void Awake()
        {
            Build();
            if (Tram != null) Tram.enabled = false;           // we drive the tram's body
            _pursuerDist = -StartGap;
            PlaceTram(true);
        }

        // Factories (BossKit.Chase) fill TrackPoints/Stations after AddComponent (= after Awake): rebuild here.
        void Start()
        {
            Build();
            if (StationIndex >= 0) { RestoreAtStation(StationIndex); return; }
            if (!Started) { _dist = 0f; _pursuerDist = -StartGap; }
            PlaceTram(true);
            PlacePursuer();
        }

        void Build()
        {
            int n = TrackPoints != null ? TrackPoints.Length : 0;
            _cum = new float[Mathf.Max(1, n)];
            for (int i = 1; i < n; i++) _cum[i] = _cum[i - 1] + Vector3.Distance(TrackPoints[i - 1], TrackPoints[i]);
            _tripUsed = new bool[Mathf.Max(1, Stations != null ? Stations.Length : 0)];
        }

        /// <summary>Track distance of station i's dock point (bots/HUD).</summary>
        public float StationDistance(int i) => StopDistance(i);
        /// <summary>Track distance where the tram will stop next (a station or the end).</summary>
        public float NextStop => NextStopDistance();

        float StopDistance(int station) => Stations != null && station >= 0 && station < Stations.Length ? _cum[Mathf.Clamp(Stations[station].PointIndex, 0, _cum.Length - 1)] : 0f;

        public Vector3 PointAt(float d)
        {
            int n = TrackPoints != null ? TrackPoints.Length : 0;
            if (n == 0) return transform.position;
            if (n == 1 || d <= 0f)
            {
                var dir0 = n > 1 ? (TrackPoints[1] - TrackPoints[0]).normalized : Vector3.forward;
                return transform.TransformPoint(TrackPoints[0] + dir0 * d);
            }
            for (int i = 1; i < n; i++)
                if (d <= _cum[i] || i == n - 1)
                {
                    float seg = _cum[i] - _cum[i - 1];
                    float t = seg > 1e-4f ? Mathf.Clamp01((d - _cum[i - 1]) / seg) : 1f;
                    return transform.TransformPoint(Vector3.Lerp(TrackPoints[i - 1], TrackPoints[i], t));
                }
            return transform.TransformPoint(TrackPoints[n - 1]);
        }

        Vector3 DirAt(float d)
        {
            var a = PointAt(d - 0.5f); var b = PointAt(d + 0.5f); var v = b - a; v.y = 0f;
            return v.sqrMagnitude > 1e-4f ? v.normalized : transform.forward;
        }

        void PlaceTram(bool snap)
        {
            if (Tram == null || Tram.Body == null) return;
            var p = PointAt(_dist);
            if (snap) Tram.Body.Snap(p); else Tram.Body.MoveTo(p);
        }

        // ------------------------------------------------------------------ API
        public void StartChase() { if (!Started && !Finished) { Started = true; Raise(true); } }

        /// <summary>Checkpoint restore: tram docked at station index (-1 = dock), gap = StartGap, not BUSTED.</summary>
        public void RestoreAtStation(int index)
        {
            if (_cum == null) Build();
            index = Mathf.Clamp(index, -1, (Stations != null ? Stations.Length : 0) - 1);
            StationIndex = index;
            _dist = index >= 0 ? StopDistance(index) : 0f;
            _pursuerDist = _dist - StartGap;
            _trip = 0f; Busted = false; Finished = false; Docked = true; Moving = false;
            Started = false;
            PlaceTram(true);
            PlacePursuer();
            Raise(true);
        }

        public void ResetState() { BustCount = 0; for (int i = 0; i < _tripUsed.Length; i++) _tripUsed[i] = false; RestoreAtStation(-1); }

        /// <summary>Station whose checkpoint is the highest one reached (-1 none).</summary>
        public int StationForCheckpoint(int checkpointIndex)
        {
            int best = -1, bestCk = 0;
            if (Stations != null)
                for (int i = 0; i < Stations.Length; i++)
                {
                    int ck = Stations[i].CheckpointIndex;
                    if (ck > 0 && ck <= checkpointIndex && ck >= bestCk) { best = i; bestCk = ck; }
                }
            return best;
        }

        // ------------------------------------------------------------------ loop
        void Update()
        {
            if (_startFrames > 0)
            {
                var lm = LevelManager.Instance;
                if (lm != null && !lm.IsStarted) return;
                if (--_startFrames == 0)
                {
                    var mgr = CheckpointManager.Instance;
                    int st = mgr != null ? StationForCheckpoint(mgr.CurrentIndex) : -1;
                    if (st >= 0) RestoreAtStation(st);
                }
            }
        }

        void FixedUpdate()
        {
            if (_cum == null) Build();
            float dt = Time.fixedDeltaTime;
            AllAboard = ComputeAllAboard();
            if (!Started && AutoStart && AllAboard && !Finished && _startFrames == 0) { Started = true; Raise(true); }
            if (Busted)
            {
                _bustTimer -= dt;
                if (_bustTimer <= 0f && _bustTimer > -100f)
                {
                    _bustTimer = -999f;
                    if (RestartOverride != null) RestartOverride(this);
                    else if (GameSession.Instance != null) GameSession.Instance.RestartFromCheckpoint();
                    else RestoreAtStation(StationIndex);
                }
                return;
            }
            if (!Started || Finished) { PlaceTram(false); PlacePursuer(); StatusTick(dt); return; }

            bool clear = !WaitingForClear;
            Moving = AllAboard && clear;
            if (Moving)
            {
                Docked = false;
                float next = NextStopDistance();
                _dist = Mathf.Min(next, _dist + TramSpeed * dt);
                // tolerance must exceed NextStopDistance's 1e-3 look-ahead, else float rounding (49.9999) skips a station
                if (_dist >= next - 5e-3f) { _dist = next; Arrive(next); }
            }
            PlaceTram(false);

            if (_trip > 0f) _trip -= dt;
            else _pursuerDist += PursuerSpeed * dt;
            _pursuerDist = Mathf.Max(_pursuerDist, _dist - MaxGap);
            if (Docked && StationIndex >= 0 && StationIndex < _tripUsed.Length && !_tripUsed[StationIndex] && Gap < RubberBandGap && Gap > 0f)
            {
                _tripUsed[StationIndex] = true;
                _trip = TripSeconds;
                GameEvents.RaiseToast(TripText, UIColors.AccentYellow);
                Sfx.At(SfxId.Oink, Pursuer != null ? Pursuer.position : PointAt(_pursuerDist), 1f, 1.2f);
            }
            PlacePursuer();
            if (Gap <= 0f) Bust();
            StatusTick(dt);
        }

        float NextStopDistance()
        {
            float end = TrackLength;
            if (Stations != null)
                for (int i = 0; i < Stations.Length; i++)
                {
                    float s = StopDistance(i);
                    if (s > _dist + 1e-3f && s < end) end = s;
                }
            return end;
        }

        void Arrive(float at)
        {
            Docked = true;
            if (at >= TrackLength - 1e-3f)
            {
                Finished = true; Moving = false;
                Sfx.At(SfxId.TramBell, PointAt(_dist), 1f);
                Raise(true);
                Arrived?.Invoke(this);
                return;
            }
            for (int i = 0; i < Stations.Length; i++)
                if (Mathf.Abs(StopDistance(i) - at) < 1e-3f) { StationIndex = i; break; }
            // the station is the checkpoint: BUSTED -> Restart Checkpoint resumes here (StationForCheckpoint)
            if (StationIndex >= 0 && StationIndex < Stations.Length && Stations[StationIndex].CheckpointIndex > 0)
                BossCompat.ActivateCheckpoint(Stations[StationIndex].CheckpointIndex);
            Sfx.At(SfxId.TramBell, PointAt(_dist), 0.9f);
            Raise(true);
            StationReached?.Invoke(this);
        }

        void Bust()
        {
            Busted = true;
            BustCount++;
            _bustTimer = BustedDelay;
            Moving = false;
            Fx.Confetti(PointAt(_dist) + Vector3.up * 2f);
            Sfx.At(SfxId.CoinShower, PointAt(_dist), 1f);
            Sfx.At(SfxId.SlideWhistleDown, PointAt(_dist), 1f);
            GameEvents.RaiseBanner("BUSTED!", "Back to the last station...", BannerStyle.Danger);
            if (!string.IsNullOrEmpty(BustedBeat)) GameEvents.RaiseStoryBeat(BustedBeat);
            Raise(true);
            BustedEvent?.Invoke(this);
        }

        void PlacePursuer()
        {
            if (Pursuer == null) return;
            var p = PointAt(_pursuerDist);
            Pursuer.position = new Vector3(p.x, p.y, p.z);
            var d = DirAt(_pursuerDist);
            if (d.sqrMagnitude > 1e-4f) Pursuer.rotation = Quaternion.LookRotation(d, Vector3.up);
        }

        bool ComputeAllAboard()
        {
            if (Tram == null || Tram.Body == null) return false;
            var team = BossTeam.Members;
            var deck = Tram.Body.transform;
            int needed = 0;
            for (int i = 0; i < team.Count; i++)
            {
                var c = team[i];
                if (c == null || c.State == CharacterState.Lost || c.State == CharacterState.Captured) continue;
                needed++;
                var l = deck.InverseTransformPoint(c.Feet);
                if (Mathf.Abs(l.x) > DeckSize.x * 0.5f + 0.3f || Mathf.Abs(l.z) > DeckSize.z * 0.5f + 0.3f || l.y < -1.2f || l.y > DeckSize.y) return false;
            }
            return needed > 0;
        }

        void StatusTick(float dt)
        {
            _statusTimer -= dt;
            if (_statusTimer <= 0f) { _statusTimer = 0.1f; Raise(false); }
        }

        public ChaseStatus BuildStatus() => new ChaseStatus
        {
            Gap = Mathf.Max(0f, Gap), MaxGap = MaxGap, Station = Mathf.Max(0, StationIndex), StationCount = Stations != null ? Stations.Length : 0,
            Busted = Busted, Visible = Started && !Finished || Busted
        };

        void Raise(bool force)
        {
            var s = BuildStatus();
            if (!force && Mathf.Abs(s.Gap - _last.Gap) < 0.05f && s.Station == _last.Station && s.Busted == _last.Busted && s.Visible == _last.Visible) return;
            _last = s;
            GameEvents.RaiseChaseChanged(s);
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            if (TrackPoints == null) return;
            Gizmos.color = Color.cyan;
            for (int i = 1; i < TrackPoints.Length; i++) Gizmos.DrawLine(transform.TransformPoint(TrackPoints[i - 1]), transform.TransformPoint(TrackPoints[i]));
        }
#endif
    }
}
