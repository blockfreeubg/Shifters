// =====================================================================================
//  SHIFTERS - ScareBlackoutDriver.cs   [owned by STORY_KIT]
//  Fallback for ScareKit.Blackout while rendering's LevelLighting.SetOverride is absent: dims directional lights
//  (+ point/spot lights unless keepAccents) and ambient to 'floor', and shows a dark veil (screen-space canvas, sorting
//  order -50 so the HUD stays on top). Restores exactly; overlapping calls extend the end time. Unscaled time.
// =====================================================================================
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Shifters
{
    public sealed class ScareBlackoutDriver : MonoBehaviour
    {
        static ScareBlackoutDriver _inst;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _inst = null; _ovSaved = false; _ovPrevHeld = false; _ovPrevI = _ovPrevA = 1f; _ovBlackI = _ovBlackA = -1f; }

        public static bool Active => _inst != null && _inst._active;
        public static float VeilAlpha => _inst != null && _inst._active ? _inst._veilAlpha : 0f;

        readonly List<Light> _lights = new List<Light>();
        readonly List<float> _intensity = new List<float>();
        Color _amb, _sky, _eq, _gr;
        float _ambI, _end, _veilAlpha;
        bool _active, _dim;
        Image _veil;

        public static void Begin(float seconds, bool keepAccents, float floor, float veilAlpha)
        {
            if (!Application.isPlaying) return;
            if (_inst == null) _inst = new GameObject("ScareBlackout").AddComponent<ScareBlackoutDriver>();
            _inst.Run(seconds, keepAccents, floor, veilAlpha);
        }

        public static void EndNow() { if (_inst != null) _inst.Restore(); }

        // polish: the LevelLighting override that was in effect before the blackout (restored when it ends)
        static bool _ovSaved, _ovPrevHeld;
        static float _ovPrevI = 1f, _ovPrevA = 1f, _ovBlackI = -1f, _ovBlackA = -1f;

        /// <summary>Called by ScareKit.Blackout after it set the override: the blackout's own targets (to tell them from
        /// an override a level sets while the blackout runs).</summary>
        public static void NoteBlackoutOverride(float intensity, float ambient) { _ovBlackI = intensity; _ovBlackA = ambient; }

        /// <summary>Called by ScareKit.Blackout right before it sets the lighting override: snapshot the level's own
        /// override (none, or e.g. L12's atrium dimming) so Restore puts it back instead of leaving the blackout on.</summary>
        public static void SaveLightingOverride()
        {
            _ovSaved = true;
            _ovPrevHeld = LevelLighting.OverrideHeld;
            _ovPrevI = LevelLighting.OverrideTargetIntensity;
            _ovPrevA = LevelLighting.OverrideTargetAmbient;
        }

        static void RestoreLightingOverride()
        {
            if (!_ovSaved) return;
            _ovSaved = false;
            if (!Application.isPlaying) return;
            // only undo our own blackout: if a level replaced the override meanwhile (L10 garage etc.), keep theirs
            if (!ScareKit.LastBlackoutUsedOverride || !LevelLighting.OverrideHeld) return;
            if (Mathf.Abs(LevelLighting.OverrideTargetIntensity - _ovBlackI) > 0.001f || Mathf.Abs(LevelLighting.OverrideTargetAmbient - _ovBlackA) > 0.001f) return;
            if (_ovPrevHeld) LevelLighting.SetOverride(_ovPrevI, _ovPrevA, 0.15f);
            else StoryKitLate.TryClearLightingOverride();
        }

        void Run(float seconds, bool keepAccents, float floor, float veilAlpha)
        {
            float end = Time.time + seconds;
            if (_active) { _end = Mathf.Max(_end, end); return; }
            _active = true; _end = end; _veilAlpha = veilAlpha;
            _lights.Clear(); _intensity.Clear();
            _dim = floor < 0.999f;
            if (_dim) foreach (var l in FindObjectsByType<Light>())
            {
                if (l == null || !l.enabled) continue;
                if (keepAccents && l.type != LightType.Directional) continue;
                _lights.Add(l); _intensity.Add(l.intensity);
                l.intensity *= floor;
            }
            if (_dim) StoreAmbient(floor);
            EnsureVeil();
            _veil.color = new Color(0.01f, 0.01f, 0.03f, veilAlpha);
            _veil.enabled = true;
        }

        void StoreAmbient(float floor)
        {
            _amb = RenderSettings.ambientLight; _sky = RenderSettings.ambientSkyColor; _eq = RenderSettings.ambientEquatorColor;
            _gr = RenderSettings.ambientGroundColor; _ambI = RenderSettings.ambientIntensity;
            RenderSettings.ambientLight = _amb * floor; RenderSettings.ambientSkyColor = _sky * floor;
            RenderSettings.ambientEquatorColor = _eq * floor; RenderSettings.ambientGroundColor = _gr * floor;
            RenderSettings.ambientIntensity = _ambI * floor;
        }

        void EnsureVeil()
        {
            if (_veil != null) return;
            var cgo = new GameObject("ScareVeil");
            cgo.transform.SetParent(transform, false);
            var canvas = cgo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = -50;
            var igo = new GameObject("Veil");
            igo.transform.SetParent(cgo.transform, false);
            _veil = igo.AddComponent<Image>();
            _veil.raycastTarget = false;
            var rt = _veil.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        void Restore()
        {
            if (!_active) return;
            _active = false;
            for (int i = 0; i < _lights.Count; i++) if (_lights[i] != null) _lights[i].intensity = _intensity[i];
            _lights.Clear(); _intensity.Clear();
            if (_dim)
            {
                RenderSettings.ambientLight = _amb; RenderSettings.ambientSkyColor = _sky; RenderSettings.ambientEquatorColor = _eq;
                RenderSettings.ambientGroundColor = _gr; RenderSettings.ambientIntensity = _ambI;
            }
            if (_veil != null) _veil.enabled = false;
            RestoreLightingOverride();
        }

        void Update() { if (_active && Time.time >= _end) Restore(); }
        void OnDisable() { Restore(); }
        void OnDestroy() { Restore(); if (_inst == this) _inst = null; }
    }
}
