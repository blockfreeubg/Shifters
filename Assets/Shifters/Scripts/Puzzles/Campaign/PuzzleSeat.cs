// =====================================================================================
//  SHIFTERS - PuzzleSeat.cs   [PUZZLES - campaign wave; promoted copy of L3VolunteerChair]
//  "INSERT INTERN TO CONTINUE": E to sit (Condition.Seated, kinematic at the seat, locked in).
//  The chair is a SignalSource that is ON while occupied (powers the hard-light bridge). Any change
//  of ReleaseInput (the far-side RELEASE lever) frees the volunteer; the signal then stays ON for
//  Tuning.TimedWindow(RouteLength, fastest sprint) seconds (HUD countdown) and switches off -> only a
//  TURBO volunteer makes it back across. ReleasedSignal latches ON at the first release (opens the
//  maintenance tunnel = the recovery route). Anti-softlock: if nobody else is alive to pull the lever,
//  the chair lets the volunteer go by itself. Built by PuzzleKit.Seat.
// =====================================================================================
using TMPro;
using UnityEngine;

namespace Shifters
{
    public class PuzzleSeat : SignalSource, IInteractable, IResettable
    {
        [Header("Wiring")]
        [Tooltip("Occupant feet pose (character faces this transform's +Z).")] public Transform Seat;
        [Tooltip("Where the released volunteer is put down.")] public Transform ExitPoint;
        [Tooltip("Any change of this source while occupied releases the volunteer (the RELEASE lever).")] public SignalSource ReleaseInput;
        [Tooltip("Latches ON at the first release (maintenance tunnel door).")] public PuzzleSignal ReleasedSignal;
        [Tooltip("Walking route chair -> far side (m). The grace window is Tuning.TimedWindow(route, fastest sprint).")] public float RouteLength = 21f;
        [Tooltip("Seconds without any other free teammate before the chair releases the volunteer by itself.")] public float SoftlockSeconds = 4f;

        [Header("Feedback")]
        [Tooltip("Screen text while empty (PuzzleKit.Seat label).")] public string IdleText = "INSERT INTERN\nTO CONTINUE";
        public TextMeshPro Screen;
        public PuzzleLamp Lamp;
        [Tooltip("Lap bar that swings down while occupied (rotates about local X).")] public Transform Restraint;
        public float RestraintDownAngle = 70f;

        /// <summary>The seated volunteer (null when empty).</summary>
        public ShifterCharacter Occupant { get; private set; }
        public bool IsOccupied => Occupant != null;
        /// <summary>Seconds left before the bridge retracts after a release (0 = not counting).</summary>
        public float GraceRemaining { get; private set; }
        public int TimesSeated { get; private set; }
        public int TimesReleased { get; private set; }

        static readonly string[] SitLines = { "Why did I volunteer?", "Is this covered by dental?", "It's warm. Why is it warm?", "Tell my plant I love it." };
        static readonly string[] FreeLines = { "I'M FREE! Now what?", "My legs are asleep!", "Never again. Probably.", "I can see sounds now." };
        int _line;
        float _restraint, _softlock, _blink;
        string _shownText;

        // ------------------------------------------------------------------ lifecycle
        SignalSource _subscribed;

        /// <summary>(Re)subscribe to ReleaseInput (factories set it after AddComponent, so OnEnable alone is not enough).</summary>
        void Subscribe()
        {
            if (_subscribed == ReleaseInput) return;
            if (_subscribed != null) _subscribed.Changed -= OnReleaseInput;
            _subscribed = ReleaseInput;
            if (_subscribed != null) _subscribed.Changed += OnReleaseInput;
        }

        void OnEnable() => Subscribe();
        void OnDisable()
        {
            if (_subscribed != null) _subscribed.Changed -= OnReleaseInput;
            _subscribed = null;
            if (GraceRemaining > 0f) GameEvents.RaiseTimer(0f);
        }

        void Start() { Subscribe(); RefreshScreen(true); }

        void OnReleaseInput(SignalSource src, bool on)
        {
            if (IsOccupied) Release();
            else if (on) Sfx.At(SfxId.Denied, transform.position, 0.6f);
        }

        // ------------------------------------------------------------------ IInteractable
        public int InteractPriority => 2;
        public Vector3 InteractPoint => (Seat != null ? Seat.position : transform.position) + Vector3.up * 0.9f;

        public bool CanInteract(ShifterCharacter who, out string blockedReason)
        {
            blockedReason = null;
            if (who == null) return false;
            if (IsOccupied)
            {
                blockedReason = who == Occupant ? "You're strapped in. A coworker must pull RELEASE." : Occupant.DisplayName + " already volunteered. Bravely.";
                return false;
            }
            if (!who.CanAct) return false;
            if (who.Grabber != null && who.Grabber.IsHolding) { blockedReason = "Hands full. Put it down first."; return false; }
            return true;
        }

        public string GetPrompt(ShifterCharacter who) => "Volunteer (sit down)";

        public void Interact(ShifterCharacter who)
        {
            if (!CanInteract(who, out var reason))
            {
                if (reason != null) { Sfx.At(SfxId.Denied, transform.position); GameEvents.RaiseToast(reason, UIColors.Danger); }
                return;
            }
            SeatCharacter(who);
        }

        // ------------------------------------------------------------------ seating
        /// <summary>Lock a character into the chair (scripts/tests too).</summary>
        public bool SeatCharacter(ShifterCharacter c)
        {
            if (c == null || IsOccupied || !c.IsAlive) return false;
            if (c.Grabber != null && c.Grabber.IsHolding) c.Grabber.ForceRelease();
            if (!c.SetCondition(CharacterCondition.Seated)) return false;
            var rb = c.Body;
            if (rb != null)
            {
                if (!rb.isKinematic) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
                rb.isKinematic = true;
            }
            Occupant = c;
            TimesSeated++;
            _softlock = 0f;
            SnapOccupant();
            if (c.Visual != null) { c.Visual.SetPose(PoseOverride.Seated); c.Visual.SetExpression(Expression.Scared, 2.5f); }
            c.Say(SitLines[_line++ % SitLines.Length]);
            Sfx.At(SfxId.DoorSlam, transform.position, 0.7f, 1.5f);
            Sfx.At(SfxId.ElevatorHum, transform.position, 0.8f, 1.3f);
            Fx.Sparks(transform.position + Vector3.up * 2.2f, Vector3.up, 16, UIColors.Info);
            bool wasCounting = GraceRemaining > 0f;
            GraceRemaining = 0f;
            if (wasCounting) GameEvents.RaiseTimer(0f);
            if (!IsOn) GameEvents.RaiseBanner("VOLUNTEER ACCEPTED", c.DisplayName + " is now load-bearing. Cross the bridge.", BannerStyle.Puzzle);
            SetOn(true);
            RefreshScreen(true);
            return true;
        }

        /// <summary>Free the volunteer. The bridge signal stays on for the grace window, then turns off.</summary>
        public void Release()
        {
            var c = Occupant;
            if (c == null) return;
            Occupant = null;
            TimesReleased++;
            var exit = ExitPoint != null ? ExitPoint : transform;
            var rb = c.Body;
            if (rb != null) rb.isKinematic = false;
            if (c.Motor != null) c.Motor.Teleport(exit.position, exit.rotation);
            else c.transform.SetPositionAndRotation(exit.position, exit.rotation);
            c.SetCondition(CharacterCondition.Normal);
            if (c.Visual != null) { c.Visual.SetPose(PoseOverride.None); c.Visual.SetExpression(Expression.Happy, 2f); }
            c.Say(FreeLines[_line++ % FreeLines.Length]);
            Fx.Poof(c.Center, c.Color, 1f);
            Sfx.At(SfxId.Revive, c.Center);
            Sfx.At(SfxId.Alarm, transform.position, 0.7f);
            var cm = CharacterManager.Instance;
            float fastest = cm != null ? cm.FastestSprintSpeed : 7f;
            GraceRemaining = Tuning.TimedWindow(RouteLength, fastest);
            GameEvents.RaiseTimer(GraceRemaining);
            GameEvents.RaiseBanner("VOLUNTEER RELEASED", "Bridge retracts in " + GraceRemaining.ToString("0.0") + " s. Not fast? Take the MAINTENANCE TUNNEL.", BannerStyle.Warning);
            if (ReleasedSignal != null) ReleasedSignal.Set(true);
            RefreshScreen(true);
        }

        void SnapOccupant()
        {
            var c = Occupant;
            if (c == null || Seat == null) return;
            c.transform.SetPositionAndRotation(Seat.position, Seat.rotation);
            if (c.Body != null) { c.Body.position = Seat.position; c.Body.rotation = Seat.rotation; }
        }

        void FixedUpdate()
        {
            var c = Occupant;
            if (c == null) return;
            if (!c.IsAlive || c.Condition != CharacterCondition.Seated)
            {
                // somebody else took over the body (checkpoint restore, KO...): forget it quietly
                Occupant = null;
                if (GraceRemaining <= 0f) SetOn(false);
                RefreshScreen(true);
                return;
            }
            SnapOccupant();
            bool anyoneElse = false;
            var all = ShifterCharacter.All;
            for (int i = 0; i < all.Count; i++)
                if (all[i] != null && all[i] != c && all[i].IsAlive) { anyoneElse = true; break; }
            _softlock = anyoneElse ? 0f : _softlock + Time.fixedDeltaTime;
            if (_softlock >= SoftlockSeconds)
            {
                GameEvents.RaiseToast("NO SUPERVISOR DETECTED. RELEASING VOLUNTEER. PLEASE ENJOY THE TUNNEL.", UIColors.AccentYellow);
                Release();
            }
        }

        void Update()
        {
            if (_subscribed != ReleaseInput) Subscribe();
            if (GraceRemaining > 0f && !IsOccupied)
            {
                GraceRemaining -= Time.deltaTime;
                _blink -= Time.deltaTime;
                if (_blink <= 0f)
                {
                    _blink = Mathf.Lerp(0.15f, 0.6f, Mathf.Clamp01(GraceRemaining / 3f));
                    Sfx.At(SfxId.TimerTick, transform.position, 0.8f, 1.4f);
                }
                if (GraceRemaining <= 0f)
                {
                    GraceRemaining = 0f;
                    GameEvents.RaiseTimer(0f);
                    Sfx.Play(SfxId.TimerEnd);
                    SetOn(false);
                }
                else GameEvents.RaiseTimer(GraceRemaining);
            }
            float target = IsOccupied ? 1f : 0f;
            if (!Mathf.Approximately(_restraint, target))
            {
                _restraint = Mathf.MoveTowards(_restraint, target, Time.deltaTime * 3f);
                if (Restraint != null) Restraint.localRotation = Quaternion.Euler(-RestraintDownAngle * (1f - PuzzleUtil.Ease(_restraint)), 0f, 0f);
            }
            if (Lamp != null)
            {
                if (GraceRemaining > 0f) Lamp.Blink(PuzzleLampColor.Yellow, PuzzleLampColor.Red, 6f);
                else Lamp.Set(IsOccupied ? PuzzleLampColor.Green : PuzzleLampColor.Red);
            }
            RefreshScreen(false);
        }

        void RefreshScreen(bool force)
        {
            if (Screen == null) return;
            string t = IsOccupied ? "THANK YOU,\n" + Occupant.DisplayName.ToUpperInvariant()
                     : GraceRemaining > 0f ? "BRIDGE OFF IN\n" + GraceRemaining.ToString("0.0") + " S"
                     : IdleText;
            if (!force && t == _shownText) return;
            _shownText = t;
            Screen.text = t;
            Screen.color = IsOccupied ? UIColors.Success : GraceRemaining > 0f ? UIColors.AccentYellow : UIColors.Info;
        }

        public void ResetState()
        {
            if (IsOccupied) Release();
            GraceRemaining = 0f;
            SetOn(false);
            RefreshScreen(true);
        }
    }
}
