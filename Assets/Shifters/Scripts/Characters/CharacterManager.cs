// =====================================================================================
//  SHIFTERS - CharacterManager.cs   [CONTRACT API - implementation owned by the CORE agent]
//  Scene singleton owning the team of exactly three characters. Created by LevelManager.
//
//  Switching flow (SwitchTo, non-instant):
//    1. previous character -> SetActive(false) immediately (its AI takes over, it never moves/teleports)
//    2. Active = next, GameEvents.CharacterSwitched(prev, next), SwitchTransitionChanged(true)
//    3. ShiftersCamera.TransitionTo(next) - glide or GTA sky cam (<= 1.2 s unscaled)
//    4. on arrival: next.SetActive(true) (input only now), SwitchTransitionChanged(false), quip + wave
//  Unavailable active -> banner, 1.2 s beat, auto-switch to the next available; none -> TeamWiped.
// =====================================================================================
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Shifters
{
    public class CharacterManager : MonoBehaviour
    {
        public static CharacterManager Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; Reprinted = null; }

        /// <summary>Raised after Reprint replaced a slot's character (argument = the NEW character). TeamSpawned is raised too.</summary>
        public static event System.Action<ShifterCharacter> Reprinted;

        readonly List<ShifterCharacter> _team = new List<ShifterCharacter>();
        public IReadOnlyList<ShifterCharacter> Team => _team;
        public ShifterCharacter Active { get; private set; }
        public int ActiveIndex => Active != null ? Active.Slot : -1;
        public TeamAIMode AIMode { get; private set; } = TeamAIMode.Stay;
        public bool IsTransitioning { get; protected set; }

        /// <summary>Path of the active character for Follow-mode AI.</summary>
        public BreadcrumbTrail Trail { get; } = new BreadcrumbTrail();
        /// <summary>Dramatic beat before auto-switching away from an unavailable active character.</summary>
        public float AutoSwitchDelay = 1.2f;
        /// <summary>True after TeamWiped was raised (reset when someone becomes available again).</summary>
        public bool IsTeamWiped { get; private set; }

        Coroutine _autoSwitch;
        float _transitionElapsed;
        ShifterCharacter _trailLeader;
        const float TransitionWatchdog = 3f;

        protected virtual void Awake() { Instance = this; }

        protected virtual void OnDestroy()
        {
            if (Instance == this) Instance = null;
            foreach (var c in _team) if (c != null) c.StateChanged -= OnCharacterStateChanged;
            BindTrailLeader(null);
        }

        public ShifterCharacter Get(int slot) => slot >= 0 && slot < _team.Count ? _team[slot] : null;
        public int AliveCount { get { int n = 0; foreach (var c in _team) if (c != null && c.IsAlive) n++; return n; } }

        /// <summary>Fastest sprint speed among Alive characters (timed doors use this).</summary>
        public float FastestSprintSpeed
        {
            get
            {
                float best = 0f;
                foreach (var c in _team) if (c != null && c.IsAlive && c.Stats != null) best = Mathf.Max(best, Tuning.SprintSpeed(c.Stats.Speed));
                return best > 0f ? best : 7f;
            }
        }

        /// <summary>Spawns the three characters at the given poses (index = slot) and activates activeSlot.</summary>
        public virtual void SpawnTeam(TeamData team, IList<Pose> spawns, int activeSlot = 0)
        {
            if (team == null || team.Members == null || spawns == null || spawns.Count == 0)
            {
                Debug.LogError("[CharacterManager] SpawnTeam needs a team and at least one spawn pose");
                return;
            }
            DespawnTeam();
            for (int i = 0; i < team.Members.Length; i++)
            {
                if (team.Members[i] == null) continue;
                var p = spawns[Mathf.Min(i, spawns.Count - 1)];
                var c = CharacterFactory.Create(team.Members[i], i, p.position, p.rotation, transform);
                c.StateChanged += OnCharacterStateChanged;
                _team.Add(c);
            }
            var studio = PortraitStudio.Instance;
            if (studio == null) studio = new GameObject("[PortraitStudio]").AddComponent<PortraitStudio>();
            studio.Build(team);

            int slot = activeSlot;
            if (Get(slot) == null || !Get(slot).IsSelectable) slot = FirstSelectable(-1);
            if (slot < 0) slot = 0;
            SwitchTo(slot, true);
            GameEvents.RaiseTeamSpawned();
        }

        void DespawnTeam()
        {
            if (_autoSwitch != null) { StopCoroutine(_autoSwitch); _autoSwitch = null; }
            foreach (var c in _team) if (c != null) { c.StateChanged -= OnCharacterStateChanged; Destroy(c.gameObject); }
            _team.Clear();
            Active = null;
            IsTransitioning = false;
            IsTeamWiped = false;
            BindTrailLeader(null);
        }

        /// <summary>True when Reprint(slot, ...) would work: the slot exists and its intern is Lost.</summary>
        public bool CanReprint(int slot) { var c = Get(slot); return c != null && c.State == CharacterState.Lost && c.Profile != null; }

        /// <summary>INTERN PRINTER (kiosk, campaign assists): replaces the LOST intern of <paramref name="slot"/> with a
        /// re-clone (CharacterGenerator.Reclone: CloneGeneration + 1, -1 secondary stat, same rule as between levels) standing
        /// Alive at pos/rot (world). Updates the session team (GameSession.Team.Members[slot]) and portraits, counts
        /// Stats.Reprints, raises TeamSpawned + CharacterStateChanged + Reprinted. The gun is NOT duplicated: the clone keeps
        /// the weapon only if the lost body was still holding it. Returns the new character, or null when the slot is not
        /// Lost (Downed / Captured interns are revived / rescued instead). The caller re-saves the checkpoint if it wants
        /// (LevelKit.ReprintKiosk does).</summary>
        public virtual ShifterCharacter Reprint(int slot, Vector3 pos, Quaternion rot)
        {
            var old = Get(slot);
            if (old == null || old.State != CharacterState.Lost || old.Profile == null) return null;
            var session = GameSession.Instance;
            var team = session != null && session.Team != null ? session.Team : (LevelManager.Instance != null ? LevelManager.Instance.Team : null);
            int seed = unchecked((team != null ? team.Seed : 7) * 31 + (session != null ? session.LevelIndex : 0) * 1009 + slot * 97 + 7919 * (old.Profile.CloneGeneration + 1));
            var profile = CharacterGenerator.Reclone(old.Profile, seed);
            bool keepsGun = old.Weapon != null && old.Weapon.HasWeapon;
            if (!keepsGun) profile.Weapon = null;
            else if (old.Weapon.Data != null) profile.Weapon = old.Weapon.Data;
            if (team != null && team.Members != null && slot < team.Members.Length) team.Members[slot] = profile;

            bool wasActive = Active == old;
            old.StateChanged -= OnCharacterStateChanged;
            if (_trailLeader == old) BindTrailLeader(null);
            if (wasActive) Active = null;
            old.gameObject.SetActive(false);
            Destroy(old.gameObject);

            var c = CharacterFactory.Create(profile, slot, pos, rot, transform);
            c.StateChanged += OnCharacterStateChanged;
            _team[slot] = c;
            IsTeamWiped = false;
            var studio = PortraitStudio.Instance;
            if (studio == null) studio = new GameObject("[PortraitStudio]").AddComponent<PortraitStudio>();
            if (team != null) studio.Build(team);
            if (session != null && session.Stats != null) session.Stats.Reprints++;

            if (Active == null || !Active.IsSelectable) SwitchTo(slot, true);
            else c.SetActive(false);
            GameEvents.RaiseTeamSpawned();
            GameEvents.RaiseCharacterStateChanged(c);
            try { Reprinted?.Invoke(c); } catch (System.Exception e) { Debug.LogException(e); }
            return c;
        }

        public bool CanSwitchTo(int slot) { var c = Get(slot); return c != null && c.IsSelectable && c != Active; }

        /// <summary>Switch control. instant = no camera transition (spawn/restore).
        /// Refuses (Denied buzz) unavailable characters and requests made while a transition runs.</summary>
        public virtual bool SwitchTo(int slot, bool instant = false)
        {
            var next = Get(slot);
            if (next == null || !next.IsSelectable)
            {
                if (!instant) Sfx.Play(SfxId.Denied);
                return false;
            }
            if (next == Active)
            {
                if (instant && !next.IsActive) { next.SetActive(true); ShiftersCamera.Instance?.SetTarget(next, true); }
                return true;
            }
            if (IsTransitioning && !instant) return false;

            var prev = Active;
            if (prev != null) prev.SetActive(false);
            Active = next;
            BindTrailLeader(next);
            if (_autoSwitch != null) { StopCoroutine(_autoSwitch); _autoSwitch = null; }
            GameEvents.RaiseCharacterSwitched(prev, next);

            var cam = ShiftersCamera.Instance;
            if (instant || cam == null)
            {
                if (cam != null) cam.SetTarget(next, true);
                if (IsTransitioning) { IsTransitioning = false; GameEvents.RaiseSwitchTransitionChanged(false); }
                Arrive(next, greet: !instant);
            }
            else
            {
                IsTransitioning = true;
                _transitionElapsed = 0f;
                GameEvents.RaiseSwitchTransitionChanged(true);
                cam.TransitionTo(next, () => OnTransitionArrived(next));
            }
            if (!instant && GameSession.Instance != null && GameSession.Instance.Stats != null) GameSession.Instance.Stats.Switches++;
            return true;
        }

        void OnTransitionArrived(ShifterCharacter next)
        {
            if (next != Active || !IsTransitioning) return; // superseded
            IsTransitioning = false;
            GameEvents.RaiseSwitchTransitionChanged(false);
            Arrive(next, greet: true);
        }

        void Arrive(ShifterCharacter next, bool greet)
        {
            if (next == null || next != Active) return;
            if (!next.IsSelectable) { ScheduleAutoSwitch(next); return; }
            next.SetActive(true);
            if (!greet) return;
            next.Say(CharacterQuips.SwitchIn(next));
            if (next.Visual != null) next.Visual.Play(Random.value < 0.5f ? VisualAction.Wave : VisualAction.FistPump);
        }

        public virtual void SwitchToNextAvailable()
        {
            int s = FirstSelectable(ActiveIndex);
            if (s >= 0) SwitchTo(s);
        }

        /// <summary>First selectable slot after 'from' (cyclic, excluding the active one). -1 if none.</summary>
        int FirstSelectable(int from)
        {
            int n = Mathf.Max(1, _team.Count);
            for (int i = 1; i <= n; i++)
            {
                int s = ((from < 0 ? -1 : from) + i + n) % n;
                var c = Get(s);
                if (c != null && c.IsSelectable && c != Active) return s;
            }
            return -1;
        }

        public virtual void SetAIMode(TeamAIMode mode)
        {
            bool changed = mode != AIMode;
            AIMode = mode;
            GameEvents.RaiseAIModeChanged(mode);
            if (changed) GameEvents.RaiseToast(mode == TeamAIMode.Follow ? "TEAM: FOLLOW ME" : "TEAM: HOLD POSITION", UIColors.AccentYellow);
        }

        public void ToggleAIMode() => SetAIMode(AIMode == TeamAIMode.Stay ? TeamAIMode.Follow : TeamAIMode.Stay);

        // ---------------------------------------------------------------- per-frame
        void Update()
        {
            if (ShiftersInput.AIModePressed && !TimeController.IsPaused) ToggleAIMode();
            if (IsTransitioning) _transitionElapsed += ShiftersCamera.UnscaledDeltaTime;
            if (IsTransitioning && _transitionElapsed > TransitionWatchdog)
            {
                // camera never called back (missing/broken): hand over control anyway
                IsTransitioning = false;
                GameEvents.RaiseSwitchTransitionChanged(false);
                Arrive(Active, greet: true);
            }
        }

        void FixedUpdate()
        {
            if (Active != null && Active.IsAlive) Trail.Record(Active);
        }

        void BindTrailLeader(ShifterCharacter leader)
        {
            if (_trailLeader != null && _trailLeader.Motor != null) _trailLeader.Motor.Jumped -= OnLeaderJumped;
            _trailLeader = leader;
            if (leader != null && leader.Motor != null) leader.Motor.Jumped += OnLeaderJumped;
            Trail.Reset(leader);
        }

        void OnLeaderJumped() { if (_trailLeader != null) Trail.MarkJump(_trailLeader); }

        // ---------------------------------------------------------------- state changes
        void OnCharacterStateChanged(ShifterCharacter c)
        {
            bool silent = c.IsRestoringState;
            if (c.IsAlive)
            {
                IsTeamWiped = false;
                if (!silent) RaiseStateBanner(c);
                if (Active != null && !Active.IsSelectable) ScheduleAutoSwitch(Active);
                return;
            }
            if (!silent) RaiseStateBanner(c);
            if (!silent) CountIncident(c.State); // [integrator] RunStats.KnockOuts / Lost were never counted
            if (c == Active) ScheduleAutoSwitch(c, silent ? 0f : AutoSwitchDelay);
            else if (FirstSelectableIncludingActive() < 0) ScheduleAutoSwitch(Active, silent ? 0f : AutoSwitchDelay);
        }

        static void CountIncident(CharacterState s)
        {
            var stats = GameSession.Instance != null ? GameSession.Instance.Stats : null;
            if (stats == null) return;
            if (s == CharacterState.Downed) stats.KnockOuts++;
            else if (s == CharacterState.Lost) stats.Lost++;
        }

        int FirstSelectableIncludingActive()
        {
            for (int i = 0; i < _team.Count; i++) if (_team[i] != null && _team[i].IsSelectable) return i;
            return -1;
        }

        void ScheduleAutoSwitch(ShifterCharacter from, float delay = -1f)
        {
            if (_autoSwitch != null) StopCoroutine(_autoSwitch);
            _autoSwitch = StartCoroutine(AutoSwitchRoutine(from, delay < 0f ? AutoSwitchDelay : delay));
        }

        IEnumerator AutoSwitchRoutine(ShifterCharacter from, float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            _autoSwitch = null;
            if (Active != null && Active.IsSelectable) yield break; // recovered meanwhile (or player switched)
            int s = FirstSelectable(ActiveIndex);
            if (s >= 0)
            {
                // the transition guard must not block the automatic switch
                if (IsTransitioning) { IsTransitioning = false; GameEvents.RaiseSwitchTransitionChanged(false); }
                SwitchTo(s, delay <= 0f);
            }
            else if (!IsTeamWiped)
            {
                IsTeamWiped = true;
                GameEvents.RaiseTeamWiped();
            }
        }

        static void RaiseStateBanner(ShifterCharacter c)
        {
            string name = c.DisplayName.ToUpperInvariant();
            string cause = string.IsNullOrEmpty(c.LastCause) ? "unknown (HR is investigating)" : c.LastCause;
            switch (c.State)
            {
                case CharacterState.Downed:
                    GameEvents.RaiseBanner(name + " HAS BEEN KNOCKED OUT", "Incident report: " + cause + ". Carry them to a checkpoint.", BannerStyle.Danger);
                    break;
                case CharacterState.Lost:
                    GameEvents.RaiseBanner(name + " HAS BEEN LOST TO THE VOID", "Incident report: " + cause + ". A replacement clone has been ordered.", BannerStyle.Danger);
                    break;
                case CharacterState.Captured:
                    GameEvents.RaiseBanner(name + " HAS BEEN CAPTURED", "Incident report: " + cause + ". Open their holding cell to free them.", BannerStyle.Warning);
                    break;
                default:
                    if (c.PreviousState == CharacterState.Captured) GameEvents.RaiseBanner(name + " HAS BEEN RESCUED", "Back to work, intern.", BannerStyle.Success);
                    else if (c.PreviousState == CharacterState.Downed) GameEvents.RaiseBanner(name + " IS BACK ON THEIR FEET", "HR retrieved your coworker.", BannerStyle.Success);
                    break;
            }
        }
    }
}
