// =====================================================================================
//  SHIFTERS - L14Word.cs   [LEVEL 14 - one falling FILIBUSTER word (StoryKit.LetterBlock + kinematic Rigidbody)]
//  States: Cloud (hidden, up in the speech cloud) -> Telegraph (footprint shadow fills 1.5 s, "INCOMING WORD")
//  -> Drop (0.5 s ease-in from 12 m) -> Landed. POLITE: a character overlapping the footprint (+0.3 m, vertically
//  overlapping the block) during the last 0.5 s makes the word HOVER ("...ahem.") until clear; after 10 s of hovering
//  it nudges the occupant out at 2 m/s along NudgeDir (never off an edge) and lands. Driven by L14Hearing.
// =====================================================================================
using UnityEngine;
using TMPro;

namespace Shifters
{
    public class L14Word : MonoBehaviour
    {
        public enum WordState { Cloud, Telegraph, Drop, Hover, Landed }

        public int Index;
        public string Word = "WORD";
        public Vector3 RestCenter;           // world center when landed
        public Vector3 FootSize;             // world AABB size when landed (x, y, z)
        public Vector3 NudgeDir = Vector3.back;
        public float DropHeight = 12f;
        public Rigidbody Body;
        public Transform Shadow;             // footprint decal (scaled 0..1 during the telegraph)
        public GameObject IncomingLabel;
        public float BlipPitch = 0.62f;
        public bool Collidable = true;       // HUMBLE turns collider-free once shot down

        public WordState State { get; private set; } = WordState.Cloud;
        public bool Landed => State == WordState.Landed;
        public float HoverSeconds { get; private set; }
        public float TotalHover { get; private set; }
        public int HoverCount { get; private set; }
        public bool Stepped { get; private set; }

        float _t, _clear;
        Vector3 _from;
        Renderer[] _rends;
        Collider[] _cols;

        public static float TelegraphSeconds = 1.5f, DropSeconds = 0.5f;

        void Awake()
        {
            _rends = GetComponentsInChildren<Renderer>(true);
            _cols = GetComponentsInChildren<Collider>(true);
            if (Body == null) Body = GetComponent<Rigidbody>();
        }

        void SetVisible(bool on)
        {
            if (_rends == null) Awake();
            for (int i = 0; i < _rends.Length; i++) if (_rends[i] != null && (Shadow == null || !_rends[i].transform.IsChildOf(Shadow))) _rends[i].enabled = on;
            for (int i = 0; i < _cols.Length; i++) if (_cols[i] != null) _cols[i].enabled = on && Collidable;
        }

        void Place(Vector3 center)
        {
            if (Body != null && Body.isKinematic && Application.isPlaying) { Body.position = center; Body.MovePosition(center); }
            transform.position = center;
        }

        /// <summary>Back to the cloud (fresh timeline or checkpoint restore).</summary>
        public void ResetToCloud()
        {
            State = WordState.Cloud; _t = 0f; HoverSeconds = 0f; Stepped = false;
            Place(RestCenter + Vector3.up * DropHeight);
            SetVisible(false);
            if (Shadow != null) Shadow.gameObject.SetActive(false);
            if (IncomingLabel != null) IncomingLabel.SetActive(false);
        }

        /// <summary>Restore as already landed (checkpoint C3).</summary>
        public void LandNow()
        {
            State = WordState.Landed;
            Place(RestCenter);
            SetVisible(true);
            if (Shadow != null) Shadow.gameObject.SetActive(false);
            if (IncomingLabel != null) IncomingLabel.SetActive(false);
        }

        public void BeginTelegraph()
        {
            if (State != WordState.Cloud) return;
            State = WordState.Telegraph; _t = 0f;
            Place(RestCenter + Vector3.up * DropHeight);
            SetVisible(true);
            if (Shadow != null) { Shadow.gameObject.SetActive(true); Shadow.localScale = new Vector3(0.05f, 1f, 0.05f); }
            if (IncomingLabel != null) IncomingLabel.SetActive(true);
            Sfx.At(SfxId.SlideWhistleDown, RestCenter, 0.55f, 1.25f);
        }

        public void BeginDrop()
        {
            if (State == WordState.Cloud) BeginTelegraph();
            if (State != WordState.Telegraph) return;
            if (Occupied(out _)) { StartHover(); return; }
            State = WordState.Drop; _t = 0f; _from = transform.position;
        }

        void StartHover()
        {
            State = WordState.Hover; HoverSeconds = 0f; _clear = 0f; HoverCount++;
            Place(HoverCenter());
            L14Hearing.Instance?.LogEvent("hover." + Word + "." + Index);
            StoryKitSay.Say("MAX", "MAX", VillainLook.Gold, "...ahem.", 1.6f, Expression.Smug);
        }

        Vector3 HoverCenter()
        {
            float feet = 0f;
            var all = ShifterCharacter.All;
            for (int i = 0; i < all.Count; i++) if (all[i] != null && OverlapsFoot(all[i])) feet = Mathf.Max(feet, all[i].transform.position.y);
            float bottom = Mathf.Max(RestCenter.y - FootSize.y * 0.5f + 2.5f, feet + 2.4f);
            return new Vector3(RestCenter.x, bottom + FootSize.y * 0.5f, RestCenter.z);
        }

        bool OverlapsFoot(ShifterCharacter c)
        {
            if (c == null || !c.IsAlive) return false;
            Vector3 p = c.transform.position;
            float r = c.Radius + 0.3f;
            Vector3 h = FootSize * 0.5f;
            if (Mathf.Abs(p.x - RestCenter.x) > h.x + r || Mathf.Abs(p.z - RestCenter.z) > h.z + r) return false;
            float bottom = RestCenter.y - h.y, top = RestCenter.y + h.y;
            float feet = p.y, head = p.y + 1.7f;
            return feet < top - 0.05f && head > bottom;
        }

        public bool Occupied(out ShifterCharacter who)
        {
            who = null;
            var all = ShifterCharacter.All;
            for (int i = 0; i < all.Count; i++) if (OverlapsFoot(all[i])) { who = all[i]; return true; }
            return false;
        }

        void FixedUpdate()
        {
            if (!Application.isPlaying) return;
            float dt = Time.fixedDeltaTime;
            switch (State)
            {
                case WordState.Telegraph:
                    _t += dt;
                    if (Shadow != null) { float k = Mathf.Clamp01(_t / TelegraphSeconds); Shadow.localScale = new Vector3(Mathf.Lerp(0.05f, 1f, k), 1f, Mathf.Lerp(0.05f, 1f, k)); }
                    break;
                case WordState.Drop:
                    _t += dt;
                    if (Occupied(out _)) { StartHover(); break; }
                    float u = Mathf.Clamp01(_t / DropSeconds);
                    Place(Vector3.Lerp(_from, RestCenter, u * u));
                    if (u >= 1f) Land();
                    break;
                case WordState.Hover:
                    HoverSeconds += dt; TotalHover += dt;
                    bool occ = false;
                    var all = ShifterCharacter.All;
                    for (int i = 0; i < all.Count; i++)
                    {
                        var c = all[i];
                        if (!OverlapsFoot(c)) continue;
                        occ = true;
                        if (HoverSeconds >= 10f && c.Body != null && c.Condition != CharacterCondition.Seated)
                            c.Body.MovePosition(c.Body.position + NudgeDir.normalized * 2f * dt);
                    }
                    _clear = occ ? 0f : _clear + dt;
                    if (_clear >= 0.25f) { State = WordState.Drop; _t = 0f; _from = transform.position; }
                    else Place(Vector3.Lerp(transform.position, HoverCenter(), 0.2f));
                    break;
                case WordState.Landed:
                    if (!Stepped && Collidable) CheckStep();
                    break;
            }
        }

        void Land()
        {
            State = WordState.Landed;
            Place(RestCenter);
            if (Shadow != null) Shadow.gameObject.SetActive(false);
            if (IncomingLabel != null) IncomingLabel.SetActive(false);
            Fx.ShockwaveRing(new Vector3(RestCenter.x, RestCenter.y - FootSize.y * 0.5f + 0.05f, RestCenter.z), Mathf.Max(FootSize.x, FootSize.z) * 0.9f, VillainLook.Gold, 0.5f);
            Sfx.At(SfxId.LandHeavy, RestCenter, 0.9f, 1.1f);
            Sfx.At(SfxId.DialogueBlip, RestCenter, 1f, BlipPitch);
            L14Hearing.Instance?.OnWordLanded(this);
        }

        void CheckStep()
        {
            float top = RestCenter.y + FootSize.y * 0.5f;
            Vector3 h = FootSize * 0.5f;
            var all = ShifterCharacter.All;
            for (int i = 0; i < all.Count; i++)
            {
                var c = all[i];
                if (c == null || !c.IsAlive) continue;
                Vector3 p = c.transform.position;
                if (Mathf.Abs(p.x - RestCenter.x) < h.x && Mathf.Abs(p.z - RestCenter.z) < h.z && Mathf.Abs(p.y - top) < 0.25f)
                {
                    Stepped = true;
                    Sfx.At(SfxId.DialogueBlip, p, 1f, BlipPitch + 0.04f * Index);
                    c.Say(Word + "...");
                    return;
                }
            }
        }

        /// <summary>HUMBLE: knocked off its post. Becomes collider-free on the floor.</summary>
        public void KnockDown(Vector3 floorPoint)
        {
            Collidable = false;
            RestCenter = floorPoint + Vector3.up * FootSize.y * 0.5f;
            for (int i = 0; i < _cols.Length; i++) if (_cols[i] != null) _cols[i].enabled = false;
            State = WordState.Drop; _t = 0f; _from = transform.position;
        }
    }
}
