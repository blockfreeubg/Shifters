// =====================================================================================
//  SHIFTERS - PropSocket.cs   [PUZZLES - campaign wave, LEVELS2 CH07 MASTER VALVE]
//  ON while the Accepted prop rests in it. A released (not held) accepted prop whose centre comes within
//  CaptureRadius of the socket is snapped onto the cradle (Anchor). Grabbing it again / knocking it out = OFF.
//  Accepted = null: any non-character Grabbable is accepted. Wrong props get "WRONG PART" (rate-limited).
// =====================================================================================
using TMPro;
using UnityEngine;

namespace Shifters
{
    public class PropSocket : SignalSource, IResettable
    {
        public Grabbable Accepted;
        [Tooltip("Where the bottom of the seated prop goes.")] public Transform Anchor;
        public float CaptureRadius = 1.4f;
        [Tooltip("Seated prop farther than this from the anchor = removed.")] public float LeaveDistance = 0.6f;
        public PuzzleLamp Lamp;
        public TextMeshPro Label;
        public string IdleText = "INSERT PART";

        public Grabbable Seated { get; private set; }
        public int Insertions { get; private set; }

        static readonly Collider[] _hits = new Collider[24];
        float _lastWrong = -10f, _scan;
        bool _shownOn, _shownInit;

        Vector3 AnchorPos => Anchor != null ? Anchor.position : transform.position + Vector3.up * 0.5f;

        bool Accepts(Grabbable g) => g != null && g.Character == null && (Accepted == null || g == Accepted);

        void FixedUpdate()
        {
            if (Seated != null)
            {
                if (Seated.IsHeld || (Seated.transform.position + Seated.BottomOffset - AnchorPos).sqrMagnitude > LeaveDistance * LeaveDistance || !Seated.isActiveAndEnabled)
                {
                    Seated = null;
                    SetOn(false);
                    Sfx.At(SfxId.PlateRelease, AnchorPos, 0.8f, 0.8f);
                }
                return;
            }
            if (Accepted != null) { TryCapture(Accepted); return; }
            _scan -= Time.fixedDeltaTime;
            if (_scan > 0f) return;
            _scan = 0.1f;
            int n = Physics.OverlapSphereNonAlloc(AnchorPos, CaptureRadius, _hits, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var rb = _hits[i].attachedRigidbody;
                if (rb != null && rb.TryGetComponent(out Grabbable g) && TryCapture(g)) return;
            }
        }

        bool TryCapture(Grabbable g)
        {
            if (g == null || g.IsHeld || !g.isActiveAndEnabled) return false;
            var d = g.transform.position - AnchorPos;
            if (d.y < -0.6f || d.y > 2.2f) return false;
            d.y = 0f;
            if (d.sqrMagnitude > CaptureRadius * CaptureRadius) return false;
            if (!Accepts(g))
            {
                if (g.Character == null && PuzzleUtil.RateLimit(ref _lastWrong, 3f)) GameEvents.RaiseToast("WRONG PART. THE SOCKET IS JUDGING YOU.", UIColors.Danger);
                return false;
            }
            var r = g.GetComponent<Respawnable>();
            if (r != null && r.IsRespawning) return false;
            Snap(g);
            return true;
        }

        void Snap(Grabbable g)
        {
            var rot = Anchor != null ? Anchor.rotation : transform.rotation;
            g.transform.rotation = rot;
            var p = AnchorPos - rot * g.BottomOffset + Vector3.up * 0.02f;
            g.transform.position = p;
            if (g.Body != null)
            {
                g.Body.position = p; g.Body.rotation = rot;
                if (!g.Body.isKinematic) { g.Body.linearVelocity = Vector3.zero; g.Body.angularVelocity = Vector3.zero; }
            }
            Seated = g;
            Insertions++;
            Sfx.At(SfxId.PlatePress, p, 1f, 0.7f);
            Sfx.At(SfxId.DoorSlam, p, 0.6f, 1.3f);
            Fx.Sparks(AnchorPos + Vector3.up * 0.3f, Vector3.up, 16, UIColors.Success);
            Fx.FloatingText(AnchorPos + Vector3.up * 2f, "CLICK!", UIColors.Success, 1.2f);
            SetOn(true);
        }

        void Update()
        {
            if (_shownInit && _shownOn == IsOn) return;
            _shownInit = true; _shownOn = IsOn;
            if (Lamp != null) Lamp.Set(IsOn ? PuzzleLampColor.Green : PuzzleLampColor.Red);
            if (Label != null) { Label.text = IsOn ? "ENGAGED" : IdleText; Label.color = IsOn ? UIColors.Success : UIColors.AccentYellow; }
        }

        public void ResetState() { Seated = null; SetOn(false); }
    }
}
