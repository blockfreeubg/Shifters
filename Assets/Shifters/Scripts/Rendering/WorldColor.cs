// =====================================================================================
//  SHIFTERS - WorldColor.cs   (RENDERING, CAMPAIGN 14: the grey-to-color SHIFT)
//  Drives the shader globals read by Shifters/StylizedLit + Shifters/GradientSky (ShiftersWorldColor.hlsl):
//    _ShiftersWorldColor (base saturation), _ShiftersBursts[8] (ink bursts), _ShiftersWave (big sweep),
//    _ShiftersWorldParams (x = on, y = saturation inside the wave).
//  OFF (Enabled == false, or Settings.ColorShiftFx == false) = full color everywhere, exactly the shipped look.
//  The campaign enables it and sets the base per chapter; FREE PLAY / OVERTIME leave it off.
//  Grey-immune materials (_GreyImmune = 1): puzzle language, Gold, interns (MaterialLibrary.Instance), Fx/Hologram/Unlit.
//  Fog and sun colors are desaturated on the CPU by LevelLighting (see LevelLighting.RefreshLook).
// =====================================================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shifters
{
    public static class WorldColor
    {
        public const int MaxBursts = 8;
        public const float DefaultBurstRadius = 11f;
        public const float DefaultBurstSeconds = 1.4f;
        /// <summary>Once all 8 burst slots are used, each further solve adds this to the base (cap OverflowCap).</summary>
        public const float OverflowStep = 0.05f, OverflowCap = 0.25f;
        /// <summary>A burst requested within this distance of an existing burst centre is ignored (re-solves, restores).</summary>
        public const float DedupeRadius = 1.5f;
        public const float DefaultWaveRadius = 200f;
        /// <summary>The grey tint of the formula: color = lerp(luma * GreyTint, c, sat).</summary>
        public static readonly Vector3 GreyTint = new Vector3(0.93f, 0.96f, 1.04f);

        public static readonly int BaseId = Shader.PropertyToID("_ShiftersWorldColor");
        public static readonly int BurstsId = Shader.PropertyToID("_ShiftersBursts");
        public static readonly int WaveId = Shader.PropertyToID("_ShiftersWave");
        public static readonly int ParamsId = Shader.PropertyToID("_ShiftersWorldParams");
        public static readonly int GreyImmuneId = Shader.PropertyToID("_GreyImmune");

        static readonly Vector4 Unused = new Vector4(0f, -9999f, 0f, -1000f);
        static readonly Vector4[] _bursts = new Vector4[MaxBursts];
        static readonly float[] _burstTarget = new float[MaxBursts];
        static readonly float[] _burstAge = new float[MaxBursts];
        static readonly float[] _burstDur = new float[MaxBursts];
        static int _count;

        static bool _enabled;
        static float _base = 1f, _extra;
        static float _fadeFrom = 1f, _fadeTo = 1f, _fadeAge, _fadeDur;
        static bool _fading;

        static bool _waveOn;
        static Vector3 _waveOrigin;
        static float _waveAge, _waveSeconds, _waveFrom, _waveTo, _waveMax;

        static int _attemptLevel = -1;
        static bool _animating;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _enabled = false; _base = 1f; _extra = 0f; _fading = false; _fadeFrom = _fadeTo = 1f; _fadeAge = _fadeDur = 0f;
            _waveOn = false; _attemptLevel = -1; _animating = false;
            AutoBurst = true; BurstColorOverride = null; Changed = null; _immuneCopies.Clear();
            ClearSlots();
            Shader.SetGlobalVector(ParamsId, Vector4.zero);   // OFF until the campaign enables it
            Shader.SetGlobalFloat(BaseId, 1f);
            Shader.SetGlobalVectorArray(BurstsId, _bursts);
            Shader.SetGlobalVector(WaveId, new Vector4(0f, 0f, 0f, -1f));
        }

        // ------------------------------------------------------------------ state
        /// <summary>World-color effect wanted by the game mode (the campaign sets true; FREE PLAY / menu default false).
        /// While false the world renders in full color and PuzzleSolvedAt does not burst.</summary>
        public static bool Enabled
        {
            get => _enabled;
            set { if (_enabled == value) return; _enabled = value; Push(); RaiseChanged(); }
        }

        /// <summary>Settings → COLOR SHIFT EFFECT (SettingsData.ColorShiftFx). Off = saturation 1 everywhere.</summary>
        public static bool SettingAllows
        {
            get { var s = GameSession.Instance != null ? GameSession.Instance.Settings : null; return s == null || s.ColorShiftFx; }
        }

        /// <summary>True when the shaders actually grey the world (Enabled and the setting allows it).</summary>
        public static bool EffectActive => _enabled && SettingAllows;

        /// <summary>Auto-burst on GameEvents.PuzzleSolvedAt while Enabled (default true).</summary>
        public static bool AutoBurst = true;

        /// <summary>Optional color for burst Fx (default: the active intern's color).</summary>
        public static Color? BurstColorOverride;

        /// <summary>Base saturation as last set by SetBase (target of a running fade), without the overflow bonus.</summary>
        public static float Base => _fading ? _fadeTo : _base;

        /// <summary>Overflow bonus from solves beyond 8 bursts (0..OverflowCap), part of the level attempt.</summary>
        public static float OverflowBonus => _extra;

        /// <summary>Effective world saturation right now (base + overflow, wave/fade progress included); 1 when the effect
        /// is off. Values above 1 = oversaturated (epilogue party).</summary>
        public static float Current
        {
            get
            {
                if (!EffectActive) return 1f;
                if (_waveOn) return Mathf.Lerp(_waveFrom, _waveTo, WaveProgress) + _extra;
                return _base + _extra;
            }
        }

        public static int BurstCount => _count;
        public static bool WaveRunning => _waveOn;
        /// <summary>0..1 progress of the running wave (1 when none).</summary>
        public static float WaveProgress => _waveOn && _waveSeconds > 0f ? Mathf.Clamp01(_waveAge / _waveSeconds) : 1f;
        /// <summary>Current wave radius in metres (-1 when none).</summary>
        public static float WaveRadius => _waveOn ? _waveMax * EaseWave(WaveProgress) : -1f;

        /// <summary>Raised whenever base/enabled/bursts change (the CPU look - fog, sun - listens). Also mirrored to
        /// GameEvents.WorldColorChanged(base) on SetBase / wave end.</summary>
        public static event Action Changed;

        // ------------------------------------------------------------------ API (CAMPAIGN 18.8)
        /// <summary>Sets the base saturation (Floor + (1 - Floor) * meter is the campaign's job). seconds &gt; 0 fades.</summary>
        public static void SetBase(float base01, float seconds = 0f)
        {
            base01 = Mathf.Clamp(base01, 0f, 2f);
            EnsureRuntime();
            _waveOn = false;   // an explicit base wins over a running wave
            if (seconds > 0f && Application.isPlaying)
            {
                _fadeFrom = _base; _fadeTo = base01; _fadeAge = 0f; _fadeDur = seconds; _fading = true; _animating = true;
            }
            else { _base = base01; _fading = false; }
            Push();
            RaiseChanged();
            GameEvents.RaiseWorldColorChanged(Base);
        }

        /// <summary>Ink-ring color burst at c: radius 0 → radius over seconds (noise-edged), rising chime, team-colored
        /// ScanPulse. Permanent for the level attempt. Returns false when it was a duplicate (within 1.5 m of an existing
        /// burst) or when the 8 slots are full (then the base gains +0.05, cap +0.25).</summary>
        public static bool AddBurst(Vector3 c, float radius = DefaultBurstRadius, float seconds = DefaultBurstSeconds)
            => AddBurst(c, radius, seconds, BurstColorOverride, true);

        /// <summary>AddBurst with an explicit Fx color (null = active intern) and optional Fx/sound.</summary>
        public static bool AddBurst(Vector3 c, float radius, float seconds, Color? fxColor, bool fx)
        {
            EnsureRuntime();
            for (int i = 0; i < _count; i++)
                if ((new Vector3(_bursts[i].x, _bursts[i].y, _bursts[i].z) - c).sqrMagnitude < DedupeRadius * DedupeRadius) return false;
            if (_count >= MaxBursts)
            {
                float before = _extra;
                _extra = Mathf.Min(OverflowCap, _extra + OverflowStep);
                if (fx) BurstFx(c, Mathf.Min(radius, 6f), fxColor, _count);
                if (_extra != before) { Push(); RaiseChanged(); GameEvents.RaiseWorldColorChanged(Base + _extra); }
                return false;
            }
            int k = _count++;
            radius = Mathf.Max(0.5f, radius);
            _burstTarget[k] = radius;
            _burstDur[k] = Mathf.Max(0f, seconds);
            _burstAge[k] = 0f;
            bool animate = seconds > 0f && Application.isPlaying;
            _bursts[k] = new Vector4(c.x, c.y, c.z, animate ? 0f : radius);
            if (animate) _animating = true;
            if (fx) BurstFx(c, radius, fxColor, k);
            Push();
            RaiseChanged();
            return true;
        }

        /// <summary>Big scripted sweep (vault explosion, the vote, sunrise): from origin the saturation inside the growing
        /// sphere (0 → maxRadius over seconds) is toBase, outside fromBase; at the end the base becomes toBase.</summary>
        public static void Wave(Vector3 origin, float seconds, float fromBase, float toBase)
            => Wave(origin, seconds, fromBase, toBase, DefaultWaveRadius);

        public static void Wave(Vector3 origin, float seconds, float fromBase, float toBase, float maxRadius)
        {
            EnsureRuntime();
            _fading = false;
            _base = Mathf.Clamp(fromBase, 0f, 2f);
            if (seconds <= 0f || !Application.isPlaying) { _waveOn = false; SetBase(toBase); return; }
            _waveOn = true; _waveOrigin = origin; _waveAge = 0f; _waveSeconds = seconds;
            _waveFrom = _base; _waveTo = Mathf.Clamp(toBase, 0f, 2f); _waveMax = Mathf.Max(1f, maxRadius);
            _animating = true;
            Push();
            RaiseChanged();
        }

        /// <summary>Removes every burst and the overflow bonus (a fresh level start does this automatically).</summary>
        public static void ClearBursts()
        {
            ClearSlots();
            _extra = 0f;
            Push();
            RaiseChanged();
        }

        /// <summary>Ends a running wave immediately (base = its target).</summary>
        public static void FinishWave()
        {
            if (!_waveOn) return;
            _waveOn = false;
            _base = _waveTo;
            Push(); RaiseChanged(); GameEvents.RaiseWorldColorChanged(Base);
        }

        /// <summary>Centre (xyz) and current radius (w) of burst i (0..BurstCount-1).</summary>
        public static Vector4 BurstAt(int i) => i >= 0 && i < _count ? _bursts[i] : Unused;

        /// <summary>CPU mirror of the shader saturation at p (without the noise edge). 1 when the effect is off.</summary>
        public static float SaturationAt(Vector3 p)
        {
            if (!EffectActive) return 1f;
            float sat = _base + _extra;
            if (_waveOn && (p - _waveOrigin).sqrMagnitude < WaveRadius * WaveRadius) sat = _waveTo + _extra;
            for (int i = 0; i < _count; i++)
            {
                var b = _bursts[i];
                if ((new Vector3(b.x, b.y, b.z) - p).sqrMagnitude < b.w * b.w) return Mathf.Max(sat, 1f);
            }
            return sat;
        }

        /// <summary>The shader formula on a (gamma) color: lerp(luma * GreyTint, c, sat), computed in linear space.</summary>
        public static Color Desaturate(Color c, float sat)
        {
            var lin = c.linear;
            float l = lin.r * 0.2126f + lin.g * 0.7152f + lin.b * 0.0722f;
            var g = new Color(l * GreyTint.x, l * GreyTint.y, l * GreyTint.z, lin.a);
            var o = Color.LerpUnclamped(g, lin, sat);
            o.r = Mathf.Max(0f, o.r); o.g = Mathf.Max(0f, o.g); o.b = Mathf.Max(0f, o.b);
            var r = o.gamma; r.a = c.a;
            return r;
        }

        // ------------------------------------------------------------------ grey immunity
        /// <summary>Marks a StylizedLit/GradientSky material grey-immune (keeps full color in the grey world).</summary>
        public static void SetImmune(Material m, bool immune = true)
        {
            if (m != null && m.HasProperty(GreyImmuneId)) m.SetFloat(GreyImmuneId, immune ? 1f : 0f);
        }

        /// <summary>True for materials that ignore the world color (immune flag, or a shader without world color).</summary>
        public static bool IsImmune(Material m)
        {
            if (m == null) return true;
            if (!m.HasProperty(GreyImmuneId)) return true;
            return m.GetFloat(GreyImmuneId) > 0.5f;
        }

        static readonly Dictionary<Material, Material> _immuneCopies = new Dictionary<Material, Material>();
        static readonly List<Material> _mats = new List<Material>(8);

        /// <summary>Makes every renderer under root grey-immune. Materials shared through MaterialLibrary's cache (or baked
        /// assets) are swapped for cached immune copies; per-object instances are flagged in place. Returns the number of
        /// renderers touched. Call once after building a model (characters, the founders' statue, canisters...).</summary>
        public static int MakeImmune(GameObject root)
        {
            if (root == null) return 0;
            int touched = 0;
            var rs = root.GetComponentsInChildren<Renderer>(true);
            for (int r = 0; r < rs.Length; r++)
            {
                var ren = rs[r];
                if (ren == null) continue;
                ren.GetSharedMaterials(_mats);
                bool swapped = false, any = false;
                for (int i = 0; i < _mats.Count; i++)
                {
                    var m = _mats[i];
                    if (m == null || !m.HasProperty(GreyImmuneId) || m.GetFloat(GreyImmuneId) > 0.5f) continue;
                    any = true;
                    if (MaterialLibrary.IsShared(m))
                    {
                        if (!_immuneCopies.TryGetValue(m, out var copy) || copy == null)
                        {
                            copy = new Material(m) { name = m.name + "_immune" };
                            copy.SetFloat(GreyImmuneId, 1f);
                            _immuneCopies[m] = copy;
                        }
                        _mats[i] = copy; swapped = true;
                    }
                    else m.SetFloat(GreyImmuneId, 1f);
                }
                if (swapped) ren.sharedMaterials = _mats.ToArray();
                if (any) touched++;
            }
            _mats.Clear();
            return touched;
        }

        // ------------------------------------------------------------------ internals
        static void ClearSlots()
        {
            for (int i = 0; i < MaxBursts; i++) { _bursts[i] = Unused; _burstTarget[i] = 0f; _burstAge[i] = 0f; _burstDur[i] = 0f; }
            _count = 0;
        }

        static float EaseOut(float t) { t = Mathf.Clamp01(t); return 1f - (1f - t) * (1f - t) * (1f - t); }
        static float EaseWave(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t) * 0.35f + t * 0.65f; }

        static void RaiseChanged() { try { Changed?.Invoke(); } catch (Exception e) { Debug.LogException(e); } }

        static void EnsureRuntime() { if (Application.isPlaying) RenderingRuntime.Ensure(); }

        static Color ActiveColor()
        {
            var cm = CharacterManager.Instance;
            var a = cm != null ? cm.Active : null;
            return a != null ? a.Color : new Color(1f, 0.55f, 0.15f);
        }

        static void BurstFx(Vector3 c, float radius, Color? fxColor, int index)
        {
            if (!Application.isPlaying) return;
            var col = fxColor ?? ActiveColor();
            Fx.ScanPulse(c, col, radius, DefaultBurstSeconds);
            Fx.ColorBurst(c, col, radius);
            Sfx.At(SfxId.ColorBurst, c, 0.9f, 1f + 0.07f * Mathf.Min(index, 8));
        }

        /// <summary>Per-frame animation (bursts growing, base fades, waves). Called by RenderingRuntime.</summary>
        internal static void Tick(float dt)
        {
            if (!_animating) return;
            bool still = false, changed = false;
            for (int i = 0; i < _count; i++)
            {
                if (_bursts[i].w >= _burstTarget[i]) continue;
                _burstAge[i] += dt;
                float t = _burstDur[i] > 0f ? _burstAge[i] / _burstDur[i] : 1f;
                _bursts[i].w = _burstTarget[i] * EaseOut(t);
                if (t >= 1f) _bursts[i].w = _burstTarget[i]; else still = true;
                changed = true;
            }
            if (_fading)
            {
                _fadeAge += dt;
                float t = _fadeDur > 0f ? Mathf.Clamp01(_fadeAge / _fadeDur) : 1f;
                _base = Mathf.Lerp(_fadeFrom, _fadeTo, t * t * (3f - 2f * t));
                if (t >= 1f) { _fading = false; _base = _fadeTo; } else still = true;
                changed = true;
            }
            if (_waveOn)
            {
                _waveAge += dt;
                changed = true;
                if (_waveAge >= _waveSeconds) { _waveOn = false; _base = _waveTo; GameEvents.RaiseWorldColorChanged(Base); }
                else still = true;
            }
            _animating = still;
            if (changed) { Push(); RaiseChanged(); }
        }

        /// <summary>Writes the shader globals (and refreshes the CPU fog/sun look).</summary>
        public static void Push()
        {
            bool on = EffectActive;
            float waveTo = _waveOn ? _waveTo + _extra : 1f;
            Shader.SetGlobalVector(ParamsId, new Vector4(on ? 1f : 0f, waveTo, 0.6f, 0f));
            Shader.SetGlobalFloat(BaseId, _base + _extra);
            Shader.SetGlobalVectorArray(BurstsId, _bursts);
            Shader.SetGlobalVector(WaveId, _waveOn ? new Vector4(_waveOrigin.x, _waveOrigin.y, _waveOrigin.z, WaveRadius) : new Vector4(0f, 0f, 0f, -1f));
            LevelLighting.RefreshLook();
        }

        // ------------------------------------------------------------------ lifecycle hooks (RenderingRuntime)
        internal static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single) return;
            _waveOn = false; _fading = false;
            // bursts belong to a level attempt: a scene without a LevelManager (menu) ends it
            if (UnityEngine.Object.FindAnyObjectByType<LevelManager>() == null) { _attemptLevel = -1; ClearSlots(); _extra = 0f; }
            else
            {
                // restored bursts appear at full size immediately
                for (int i = 0; i < _count; i++) _bursts[i].w = _burstTarget[i];
            }
            Push();
        }

        internal static void OnLevelStarted()
        {
            var lm = LevelManager.Instance;
            if (lm == null) return;
            bool continuing = lm.StartedFromCheckpoint && lm.LevelIndex == _attemptLevel;
            if (!continuing) { ClearSlots(); _extra = 0f; }
            _attemptLevel = lm.LevelIndex;
            Push();
            RaiseChanged();
        }

        internal static void OnPuzzleSolvedAt(string text, Vector3 pos)
        {
            if (!_enabled || !AutoBurst) return;
            AddBurst(pos);
        }

        internal static void OnSettingsChanged() { Push(); RaiseChanged(); }
    }
}
