// =====================================================================================
//  SHIFTERS - CampaignCardBadges.cs   (ui_hud agent)
//  Decorations attached (at runtime, as children) to ui's HUD intern cards
//  (UIManager.Hud.Team.Card(slot).Rt): the Act II heist ROLE STAMPS (THE MUSCLE / THE
//  WHEELS / THE BRAINS, gun holder adds "...ALSO THE DISTRACTION") and the SCARED badge
//  (trembles while ShifterCharacter.IsScared). Being children of the cards they inherit
//  the HUD canvas' visibility/suppression and the cards' layout animation.
// =====================================================================================
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Shifters
{
    public class CampaignCardBadges : MonoBehaviour
    {
        /// <summary>Heist role stamps on/off (CampaignHud sets this from the chapter / story beats).</summary>
        public bool RolesOn { get; private set; }

        class Slot
        {
            public HudCharacterCard Card;
            public RectTransform Role, Distraction, Scared;
            public TextMeshProUGUI RoleText;
            public bool ScaredOn;
            public string RoleKey;
        }

        readonly Slot[] _slots = { new Slot(), new Slot(), new Slot() };
        float _nextPoll;
        float _age;

        public static CampaignCardBadges Create(Transform parent)
        {
            var go = new GameObject("CardBadges");
            go.transform.SetParent(parent, false);
            return go.AddComponent<CampaignCardBadges>();
        }

        /// <summary>Role label for an archetype ("THE MUSCLE" / "THE WHEELS" / "THE BRAINS").</summary>
        public static string RoleFor(Archetype a)
        {
            switch (a)
            {
                case Archetype.Brute: return "THE MUSCLE";
                case Archetype.Sprinter: return "THE WHEELS";
                default: return "THE BRAINS";
            }
        }

        public void SetRoles(bool on)
        {
            RolesOn = on;
            _nextPoll = 0f;
        }

        /// <summary>Is the SCARED badge currently shown on this slot's card (tests).</summary>
        public bool IsScaredShown(int slot) => slot >= 0 && slot < 3 && _slots[slot].ScaredOn && _slots[slot].Scared != null && _slots[slot].Scared.gameObject.activeSelf;
        /// <summary>The role stamp text shown on this slot's card, or null (tests).</summary>
        public string RoleShown(int slot) => slot >= 0 && slot < 3 && _slots[slot].Role != null && _slots[slot].Role.gameObject.activeSelf ? _slots[slot].RoleText.text : null;
        public RectTransform RoleRect(int slot) => slot >= 0 && slot < 3 ? _slots[slot].Role : null;
        public RectTransform ScaredRect(int slot) => slot >= 0 && slot < 3 ? _slots[slot].Scared : null;

        // card-local layout (card 340 x 100; name at the top, HR title line at ~y -52, health bar below)
        static readonly Vector2 RolePos = new Vector2(104f, -54f), ScaredPos = new Vector2(-4f, 2f);

        void Build(Slot s, HudCharacterCard card)
        {
            s.Card = card;
            TextMeshProUGUI t;
            // the heist role is rubber-stamped over the intern's HR job title
            s.Role = CampaignHudStyle.Stamp(card.Rt, "CampaignRoleStamp", "THE MUSCLE", CampaignHudStyle.StampInk, 18f, -4f, out t, true);
            s.RoleText = t;
            s.Role.anchorMin = s.Role.anchorMax = new Vector2(0f, 1f);
            s.Role.pivot = new Vector2(0f, 0.5f);
            s.Role.anchoredPosition = RolePos;
            TextMeshProUGUI dt;
            s.Distraction = CampaignHudStyle.Stamp(s.Role, "AlsoDistraction", "...ALSO THE\nDISTRACTION", CampaignHudStyle.StampInk, 11f, 5f, out dt, true);
            dt.characterSpacing = 2f; dt.lineSpacing = -14f;
            s.Distraction.anchorMin = s.Distraction.anchorMax = new Vector2(1f, 0.5f);
            s.Distraction.pivot = new Vector2(0f, 0.5f);
            s.Distraction.anchoredPosition = new Vector2(4f, -2f);
            s.Role.gameObject.SetActive(false);

            s.Scared = UIFactory.Chip(card.Rt, UIIcon.Warning, "SCARED!", CampaignHudStyle.ScaredPurple, 28f, true);
            s.Scared.name = "CampaignScared";
            s.Scared.anchorMin = s.Scared.anchorMax = new Vector2(0f, 1f);
            s.Scared.pivot = new Vector2(0f, 0.5f);
            s.Scared.anchoredPosition = ScaredPos;
            s.Scared.gameObject.SetActive(false);
            s.RoleKey = null; s.ScaredOn = false;
        }

        void Update()
        {
            _age += UITime.Delta;
            var hud = UIManager.Instance != null ? UIManager.Instance.Hud : null;
            var team = hud != null ? hud.Team : null;
            bool poll = UITime.Now >= _nextPoll;
            if (poll) _nextPoll = UITime.Now + 0.25f;
            for (int i = 0; i < 3; i++)
            {
                var s = _slots[i];
                var card = team != null ? team.Card(i) : null;
                if (card == null) { s.Card = null; continue; }
                if (s.Card != card || s.Role == null || s.Scared == null) Build(s, card);
                if (poll) PollSlot(s);
                if (s.ScaredOn)
                {
                    // tremble: small deterministic jitter
                    float jx = Mathf.Sin(_age * 61f + i) * 2.2f, jy = Mathf.Sin(_age * 47f + i * 2f) * 1.6f;
                    s.Scared.anchoredPosition = ScaredPos + new Vector2(jx, jy);
                    s.Scared.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(_age * 33f + i) * 3f);
                }
            }
        }

        void PollSlot(Slot s)
        {
            var c = s.Card.Character;
            bool alive = c != null && c.IsAlive;
            string role = null;
            if (RolesOn && c != null) role = RoleFor(c.Archetype);
            bool gun = c != null && c.Weapon != null && c.Weapon.HasWeapon;
            if (role != s.RoleKey)
            {
                s.RoleKey = role;
                bool on = role != null;
                s.Role.gameObject.SetActive(on);
                if (on)
                {
                    CampaignHudStyle.RefitStamp(s.Role, s.RoleText, role);
                    UITween.Scale(s.Role, 1f, 0.25f, Ease.OutBack, 0.05f * s.Card.transform.GetSiblingIndex(), 2f);
                }
            }
            if (s.Distraction.gameObject.activeSelf != (role != null && gun)) s.Distraction.gameObject.SetActive(role != null && gun);
            bool scared = alive && CampaignHudCompat.IsScared(c);
            if (scared != s.ScaredOn)
            {
                s.ScaredOn = scared;
                s.Scared.gameObject.SetActive(scared);
                if (scared) UITween.Punch(s.Scared, 0.3f, 0.3f);
                else { s.Scared.anchoredPosition = ScaredPos; s.Scared.localRotation = Quaternion.identity; }
            }
        }
    }
}
