// =====================================================================================
//  SHIFTERS - HudRoot.cs   (UI agent)
//  In-game HUD container on the HUD canvas (sort 10). Always alive (so events raised before
//  the HUD is shown are not lost); visibility is the HUD canvas' enabled flag (UIManager).
//  Hides the interactive bits (prompt, crosshair, progress, hint) while the switcher is open.
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public class HudRoot : MonoBehaviour
    {
        public HudDamage Damage { get; private set; }
        public HudSpeech Speech { get; private set; }
        public HudTeamPanel Team { get; private set; }
        public HudObjective Objective { get; private set; }
        public HudPrompt Prompt { get; private set; }
        public HudCrosshair Crosshair { get; private set; }
        public HudPerspective Perspective { get; private set; }
        public HudTimer Timer { get; private set; }
        public HudProgress Progress { get; private set; }
        public HudHint Hint { get; private set; }
        public HudTutorial Tutorial { get; private set; }

        CanvasGroup _interactive, _all;
        RectTransform _clickToPlay;
        bool _switcherOpen, _menuOpen, _suppressed;

        public static HudRoot Create(Transform parent)
        {
            var rt = UIFactory.Rect("HUD", parent);
            UIFactory.Stretch(rt);
            var h = rt.gameObject.AddComponent<HudRoot>();
            h._all = UIFactory.Group(rt.gameObject);
            h._all.blocksRaycasts = false;
            h._all.interactable = false;
            h.Damage = HudDamage.Create(rt);
            h.Speech = HudSpeech.Create(rt);
            var inter = UIFactory.Rect("Interactive", rt);
            UIFactory.Stretch(inter);
            h._interactive = UIFactory.Group(inter.gameObject);
            h.Crosshair = HudCrosshair.Create(inter);
            h.Progress = HudProgress.Create(inter);
            h.Prompt = HudPrompt.Create(inter);
            h.Hint = HudHint.Create(inter);
            h.Objective = HudObjective.Create(rt);
            h.Perspective = HudPerspective.Create(rt);
            h.Timer = HudTimer.Create(rt);
            h.Team = HudTeamPanel.Create(rt);
            h.Tutorial = HudTutorial.Create(rt);
            h._clickToPlay = UIFactory.Chip(rt, UIIcon.Crosshair, "CLICK TO PLAY", UIColors.AccentYellow, 44f, true);
            h._clickToPlay.anchorMin = h._clickToPlay.anchorMax = new Vector2(0.5f, 0.5f);
            h._clickToPlay.pivot = new Vector2(0.5f, 0.5f);
            h._clickToPlay.anchoredPosition = new Vector2(0f, -120f);
            h._clickToPlay.gameObject.SetActive(false);
            DevicePromptText.Attach(h._clickToPlay, "{kb:CLICK TO PLAY}");   // mouse-only hint: the chip is hidden on a pad (see Update)
            return h;
        }

        /// <summary>WebGL: the browser needs a click to (re)acquire pointer lock - say so until it is locked.
        /// ShiftersInput.CursorLocked (not Cursor.lockState): Unity reads Locked even while the browser refused the lock.</summary>
        void Update()
        {
            if (_clickToPlay == null) return;
            bool show = Application.platform == RuntimePlatform.WebGLPlayer && !_menuOpen && !_switcherOpen
                        && ShiftersInput.Context == InputContext.Gameplay && !ShiftersInput.CursorLocked
                        && !ShiftersInput.UsingGamepad;   // pad play needs no pointer lock: nothing to click
            if (_clickToPlay.gameObject.activeSelf != show) _clickToPlay.gameObject.SetActive(show);
        }

        void OnEnable() => GameEvents.SwitcherOpenChanged += OnSwitcher;
        void OnDisable() => GameEvents.SwitcherOpenChanged -= OnSwitcher;

        void OnSwitcher(bool open)
        {
            _switcherOpen = open;
            UITween.Fade(_interactive, open ? 0f : 1f, open ? 0.1f : 0.25f);
            Team?.SetDim(open);
        }

        /// <summary>Fresh level: clear transient widgets.</summary>
        public void ResetState()
        {
            Prompt?.Clear(); Crosshair?.Set(false); Timer?.Clear(); Progress?.Clear(); Hint?.Clear();
            Tutorial?.Clear(); Speech?.Clear(); Damage?.Clear(); Objective?.Clear(); Perspective?.Clear(); Team?.Clear();
            UIManager.Instance?.Banners?.Clear();
            UIManager.Instance?.Toasts?.Clear();
            _switcherOpen = false;
            if (_interactive != null) _interactive.alpha = 1f;
        }

        /// <summary>Called when the HUD becomes visible (level scene).</summary>
        public void OnShown()
        {
            Objective?.RefreshLevel();
            var cm = CharacterManager.Instance;
            if (cm != null && cm.Active != null) Perspective?.Set(cm.Active.Perspectives, false);
        }

        /// <summary>Full-screen menus (pause, level complete, team wiped) hide the whole HUD (they show their own team info).</summary>
        public void SetMenuOpen(bool open)
        {
            if (_menuOpen == open) return;
            _menuOpen = open;
            UITween.Fade(_interactive, open || _switcherOpen ? 0f : 1f, 0.15f);
            UITween.Fade(_all, open || _suppressed ? 0f : 1f, open ? 0.12f : 0.25f);
        }

        /// <summary>UIManager.SetHudSuppressed: hide the whole HUD (cutscenes) independent of menus.</summary>
        public void SetSuppressed(bool suppressed)
        {
            if (_suppressed == suppressed) return;
            _suppressed = suppressed;
            UITween.Fade(_all, _menuOpen || _suppressed ? 0f : 1f, suppressed ? 0.2f : 0.35f);
        }
    }
}
