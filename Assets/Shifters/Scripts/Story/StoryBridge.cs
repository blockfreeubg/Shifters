// =====================================================================================
//  SHIFTERS - StoryBridge.cs   [story]
//  Thin null-safe adapters from the story runtime to other modules' campaign-wave seams (all published now):
//  seams: ShiftersInput.AdvancePressed / SkipHeld01, ShiftersCamera.SuppressMainCamera(bool, Camera);
//  ui: UIManager.SetHudSuppressed; rendering: WorldColor.Wave / SetBase. Every call is wrapped so a missing
//  instance (menu scene, DevPlay) or an exception in another module never breaks a cutscene.
// =====================================================================================
using System;
using UnityEngine;

namespace Shifters
{
    public static class StoryBridge
    {
        static bool _camFallbackDisabled;
        static Camera _camFallback;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _camFallbackDisabled = false; _camFallback = null; }

        /// <summary>Tap Enter/Space/LMB/gamepad A (Cutscene + Menu contexts only).</summary>
        public static bool AdvancePressed { get { try { return ShiftersInput.AdvancePressed; } catch { return false; } } }

        /// <summary>Hold Backspace / gamepad Back progress 0..1.</summary>
        public static float SkipHeld01 { get { try { return ShiftersInput.SkipHeld01; } catch { return 0f; } } }

        public static bool HasAdvanceSeam => true;

        public static void SuppressMainCamera(bool on) { SuppressMainCamera(on, null); }

        /// <summary>One camera renders: the gameplay camera stops, the replacement (CutsceneCamera) is tagged MainCamera.</summary>
        public static void SuppressMainCamera(bool on, Camera replacement)
        {
            var sc = ShiftersCamera.Instance;
            if (sc != null)
            {
                try { sc.SuppressMainCamera(on, replacement); return; }
                catch (Exception e) { Debug.LogWarning("[Story] SuppressMainCamera: " + e.Message); }
            }
            // fallback (no ShiftersCamera in the scene): toggle Camera.main directly
            if (on)
            {
                var c = Camera.main;
                if (c != null && c.enabled && c.GetComponent<CutsceneCamera>() == null) { c.enabled = false; _camFallback = c; _camFallbackDisabled = true; }
            }
            else if (_camFallbackDisabled)
            {
                if (_camFallback != null) _camFallback.enabled = true;
                _camFallback = null; _camFallbackDisabled = false;
            }
        }

        /// <summary>The gameplay camera that the CutsceneCamera copies its first pose from.</summary>
        public static Camera GameplayCamera
        {
            get
            {
                var sc = ShiftersCamera.Instance;
                if (sc != null && sc.Cam != null) return sc.Cam;
                if (_camFallback != null) return _camFallback;
                var m = Camera.main;
                return m != null && m.GetComponent<CutsceneCamera>() == null ? m : null;
            }
        }

        /// <summary>HUD hidden during blocking scenes.</summary>
        public static void SetHudSuppressed(bool on)
        {
            var ui = UIManager.Instance;
            if (ui == null) return;
            try { ui.SetHudSuppressed(on); }
            catch (Exception e) { Debug.LogWarning("[Story] SetHudSuppressed: " + e.Message); }
        }

        /// <summary>World color wave from origin (rendering's WorldColor).</summary>
        public static void WorldWave(Vector3 origin, float seconds, float from, float to)
        {
            try { WorldColor.Wave(origin, seconds, from, to); }
            catch (Exception e) { Debug.LogWarning("[Story] WorldWave: " + e.Message); GameEvents.RaiseWorldColorChanged(to); }
        }

        public static void WorldSetBase(float v)
        {
            try { WorldColor.SetBase(v, 0f); }
            catch (Exception e) { Debug.LogWarning("[Story] WorldSetBase: " + e.Message); GameEvents.RaiseWorldColorChanged(v); }
        }
    }
}
