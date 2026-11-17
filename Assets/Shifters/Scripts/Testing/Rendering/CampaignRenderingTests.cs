// SHIFTERS - DevPlay: campaign-wave rendering (WorldColor, grey immunity, themes Bank/Night/Tower, FlashlightRig,
// VisionConeMesh, LevelLighting override, campaign Fx). Runs in L1 (real level + team); theme rooms are built 400 m above.
//   devplay.sh <clone> rcamp -devSetup Shifters.CampaignRenderingTests.RunAll -devScene Assets/Shifters/Scenes/L1_TrainingFacility.unity -devSeconds 90
using System.Collections;
using UnityEngine;

namespace Shifters
{
    public static class CampaignRenderingTests
    {
        static readonly Vector3 Room = new Vector3(0f, 400f, 0f);   // above the level: below it is the void kill plane
        static Camera _cam;

        public static IEnumerator RunAll(DevPlayContext ctx)
        {
#if UNITY_EDITOR
            UnityEditor.ShaderUtil.allowAsyncCompilation = false;
#endif
            yield return ctx.Wait(2.5f);   // level start, team spawn
            MakeCamera();
            HideHud(true);   // HUD/banners would dominate the saturation metrics
            yield return ShaderChecks(ctx);
            yield return MaterialChecks(ctx);
            yield return WorldColorL1(ctx);
            yield return BurstLogic(ctx);
            yield return Themes(ctx);
            yield return Immunity(ctx);
            yield return Flashlight(ctx);
            yield return Override(ctx);
            yield return VisionCones(ctx);
            yield return FxSheet(ctx);
            WorldColor.Enabled = false;
            HideHud(false);
            ctx.Log("campaign rendering tests finished");
        }

        static readonly System.Collections.Generic.List<Canvas> _hidden = new System.Collections.Generic.List<Canvas>();
        static void HideHud(bool hide)
        {
            if (hide) { foreach (var c in Object.FindObjectsByType<Canvas>()) if (c.enabled && c.isRootCanvas) { c.enabled = false; _hidden.Add(c); } }
            else { foreach (var c in _hidden) if (c != null) c.enabled = true; _hidden.Clear(); }
        }

        static void MakeCamera()
        {
            var go = new GameObject("TestCam");
            _cam = go.AddComponent<Camera>();
            _cam.enabled = false;
            var main = Camera.main;
            if (main != null) { _cam.CopyFrom(main); _cam.enabled = false; }
            PostFx.EnsureCameraPost(_cam);
        }

        static void Place(Vector3 pos, Vector3 target, float fov = 55f) => RenderTestUtil.Place(_cam, pos, target, fov);

        // ------------------------------------------------------------------ shaders: no new keywords, compile clean
        static IEnumerator ShaderChecks(DevPlayContext ctx)
        {
            foreach (var name in new[] { MaterialLibrary.LitShaderName, MaterialLibrary.SkyShaderName })
            {
                var sh = Shader.Find(name);
                ctx.Check(sh != null && sh.isSupported, name + " supported");
                if (sh == null) continue;
                bool bad = false;
                foreach (var k in sh.keywordSpace.keywordNames) if (k.Contains("GREY") || k.Contains("WORLD")) bad = true;
                ctx.Check(!bad, name + ": no world-color keywords (" + sh.keywordSpace.keywordCount + " keywords total)");
#if UNITY_EDITOR
                ctx.Check(!UnityEditor.ShaderUtil.ShaderHasError(sh), name + ": compiles without errors");
                int msgs = UnityEditor.ShaderUtil.GetShaderMessageCount(sh);
                ctx.Log(name + ": shader messages " + msgs);
#endif
            }
            yield return null;
        }

        // ------------------------------------------------------------------ baked materials carry _GreyImmune per key
        static IEnumerator MaterialChecks(DevPlayContext ctx)
        {
            int wrong = 0, total = 0, missing = 0;
            foreach (LevelTheme theme in System.Enum.GetValues(typeof(LevelTheme)))
                foreach (MatKey key in System.Enum.GetValues(typeof(MatKey)))
                {
                    var m = Resources.Load<Material>("Materials/" + theme + "_" + key);
                    if (m == null) { missing++; continue; }
                    total++;
                    bool immune = WorldColor.IsImmune(m);
                    if (immune != MaterialLibrary.IsGreyImmune(key)) { wrong++; if (wrong < 6) ctx.Log("immunity mismatch " + theme + "_" + key + " immune=" + immune); }
                }
            ctx.Check(missing == 0, "every theme x MatKey material baked (" + total + ", missing " + missing + ")");
            ctx.Check(wrong == 0, "baked _GreyImmune matches Palettes.IsGreyImmune for every material");
            ctx.Check(Resources.Load<Material>("Materials/Sky_Bank") != null && Resources.Load<Material>("Materials/Sky_Night") != null && Resources.Load<Material>("Materials/Sky_Tower") != null, "Bank/Night/Tower skies baked");
            ctx.Check(WorldColor.IsImmune(MaterialLibrary.Instance(Color.red, 0.3f)), "MaterialLibrary.Instance is grey-immune (interns)");
            ctx.Check(!WorldColor.IsImmune(MaterialLibrary.Colored(Color.red)), "MaterialLibrary.Colored is not immune");
            ctx.Check(WorldColor.IsImmune(MaterialLibrary.ImmuneColored(Color.red)), "MaterialLibrary.ImmuneColored is immune");
            yield return null;
        }

        // ------------------------------------------------------------------ L1 grey vs color, burst, wave
        static IEnumerator WorldColorL1(DevPlayContext ctx)
        {
            var main = Camera.main;
            if (main != null) { _cam.transform.SetPositionAndRotation(main.transform.position, main.transform.rotation); _cam.fieldOfView = main.fieldOfView; }
            var fog0 = RenderSettings.fogColor;
            WorldColor.Enabled = true;
            WorldColor.SetBase(1f);
            yield return ctx.Wait(0.2f);
            var full = ctx.Shot("wc_l1_base100", _cam);
            WorldColor.SetBase(0.40f);
            yield return ctx.Wait(0.2f);
            var grey = ctx.Shot("wc_l1_base040", _cam);
            float sFull = RenderTestUtil.MeanSaturation(full), sGrey = RenderTestUtil.MeanSaturation(grey);
            ctx.Log("L1 mean saturation: base 1.0 = " + sFull.ToString("0.000") + ", base 0.40 = " + sGrey.ToString("0.000"));
            // interns, puzzle parts and glow signs are grey-immune by design and the team is random per run: ratio ~0.63-0.80
            ctx.Check(sGrey > 0f && sGrey < sFull * 0.85f, "base 0.40 is visibly greyer than base 1.0");
            Color.RGBToHSV(RenderSettings.fogColor, out _, out float fogS, out _);
            Color.RGBToHSV(fog0, out _, out float fogS0, out _);
            ctx.Check(fogS <= fogS0 + 0.001f, "fog desaturated on the CPU (" + fogS0.ToString("0.00") + " -> " + fogS.ToString("0.00") + ")");
            if (full != null && grey != null) RenderTestUtil.SideBySide(grey, full, System.IO.Path.Combine(System.IO.Path.GetDirectoryName(full), "wc_l1_side_by_side_040_vs_100.png"));

            // burst in front of the camera
            var cm = CharacterManager.Instance;
            var a = cm != null ? cm.Active : null;
            Vector3 p = a != null ? a.transform.position + a.transform.forward * 3f : _cam.transform.position + _cam.transform.forward * 8f;
            if (Physics.Raycast(p + Vector3.up * 3f, Vector3.down, out var hit, 10f)) p = hit.point;
            ctx.Check(WorldColor.AddBurst(p), "AddBurst accepted");
            yield return ctx.Wait(0.6f);
            var mid = ctx.Shot("wc_burst_mid", _cam);
            ctx.Check(WorldColor.BurstAt(0).w > 1f && WorldColor.BurstAt(0).w < 11f, "burst mid-spread radius " + WorldColor.BurstAt(0).w.ToString("0.0"));
            yield return ctx.Wait(1.2f);
            var done = ctx.Shot("wc_burst_done", _cam);
            ctx.Check(Mathf.Abs(WorldColor.BurstAt(0).w - 11f) < 0.01f, "burst reaches 11 m");
            ctx.Check(RenderTestUtil.MeanSaturation(done) > sGrey, "burst adds color");
            ctx.Check(WorldColor.SaturationAt(p) >= 1f && WorldColor.SaturationAt(p + Vector3.right * 30f) < 0.5f, "SaturationAt inside/outside the burst");
            ctx.Check(!WorldColor.AddBurst(p + Vector3.right * 0.5f), "duplicate burst within 1.5 m ignored");

            // wave
            WorldColor.ClearBursts();
            WorldColor.Wave(p, 3f, 0.4f, 1f, 60f);
            yield return ctx.Wait(1.3f);
            ctx.Check(WorldColor.WaveRunning, "wave running");
            ctx.Shot("wc_wave_mid", _cam);
            yield return ctx.Wait(2.2f);
            ctx.Check(!WorldColor.WaveRunning && Mathf.Abs(WorldColor.Base - 1f) < 0.001f, "wave ends at its target base");
            ctx.Shot("wc_wave_done", _cam);
            // settings off = full color
            WorldColor.SetBase(0.4f);
            var gs = GameSession.Instance;
            if (gs != null && gs.Settings != null)
            {
                gs.Settings.ColorShiftFx = false; GameEvents.RaiseSettingsChanged();
                ctx.Check(!WorldColor.EffectActive && WorldColor.Current == 1f, "COLOR SHIFT EFFECT off -> saturation 1");
                gs.Settings.ColorShiftFx = true; GameEvents.RaiseSettingsChanged();
                ctx.Check(WorldColor.EffectActive, "setting back on");
            }
            WorldColor.Enabled = false;
            yield return null;
        }

        static IEnumerator BurstLogic(DevPlayContext ctx)
        {
            WorldColor.Enabled = true;
            WorldColor.SetBase(0.4f);
            WorldColor.ClearBursts();
            int ok = 0;
            for (int i = 0; i < 10; i++) if (WorldColor.AddBurst(new Vector3(1000f + i * 10f, -900f, 0f), 11f, 0f, null, false)) ok++;
            ctx.Check(ok == 8 && WorldColor.BurstCount == 8, "max 8 bursts (" + ok + ")");
            ctx.Check(Mathf.Abs(WorldColor.OverflowBonus - 0.10f) < 0.001f, "2 overflow solves -> +0.10 base (" + WorldColor.OverflowBonus + ")");
            for (int i = 0; i < 10; i++) WorldColor.AddBurst(new Vector3(2000f + i * 10f, -900f, 0f), 11f, 0f, null, false);
            ctx.Check(Mathf.Abs(WorldColor.OverflowBonus - WorldColor.OverflowCap) < 0.001f, "overflow capped at +0.25");
            ctx.Check(Mathf.Abs(WorldColor.Current - 0.65f) < 0.001f, "Current = base + overflow (" + WorldColor.Current + ")");
            // a fresh level start (not from a checkpoint) clears the attempt's bursts
            var lm = LevelManager.Instance;
            if (lm != null && !lm.StartedFromCheckpoint)
            {
                GameEvents.RaiseLevelStarted();
                ctx.Check(WorldColor.BurstCount == 0 && WorldColor.OverflowBonus == 0f, "fresh level start clears bursts");
            }
            // auto-burst on PuzzleSolvedAt only while enabled
            WorldColor.Enabled = false;
            GameEvents.RaisePuzzleSolvedAt("TEST", new Vector3(500f, -900f, 0f));
            ctx.Check(WorldColor.BurstCount == 0, "no auto-burst while disabled");
            WorldColor.Enabled = true;
            GameEvents.RaisePuzzleSolvedAt("TEST", new Vector3(500f, -900f, 0f));
            ctx.Check(WorldColor.BurstCount == 1, "auto-burst on PuzzleSolvedAt while enabled");
            WorldColor.ClearBursts();
            WorldColor.Enabled = false;
            yield return null;
        }

        // ------------------------------------------------------------------ themes in the showcase room
        static GameObject _room;
        static IEnumerator BuildRoom(LevelTheme theme)
        {
            if (_room != null) Object.Destroy(_room);
            yield return null;
            _room = RenderingShowcaseTests.BuildShowcase(theme, out _, out _);
            _room.transform.position = Room;
            Physics.SyncTransforms();
            yield return null;
        }

        static IEnumerator Themes(DevPlayContext ctx)
        {
            foreach (var theme in new[] { LevelTheme.Bank, LevelTheme.Night, LevelTheme.Tower })
            {
                yield return BuildRoom(theme);
                yield return ctx.Wait(0.3f);
                string tn = theme.ToString().ToLowerInvariant();
                Place(Room + new Vector3(0.6f, 3.9f, -10.2f), Room + new Vector3(0.2f, 1.3f, 1.5f), 55f);
                var g = ctx.Shot("theme_" + tn + "_game", _cam);
                Place(Room + new Vector3(-21f, 13f, -25f), Room + new Vector3(3f, 0.5f, 2f), 52f);
                var w = ctx.Shot("theme_" + tn + "_wide", _cam);
                float lg = RenderTestUtil.MeanLuma(g);
                ctx.Check(RenderTestUtil.MagentaFraction(g) < 0.001f && RenderTestUtil.MagentaFraction(w) < 0.001f, tn + ": no magenta");
                ctx.Check(lg > 0.05f && lg < 0.93f, tn + ": exposure sane (" + lg.ToString("0.00") + ")");
                if (theme == LevelTheme.Bank)
                {
                    // the new keys on display
                    var keys = new[] { MatKey.Marble, MatKey.Gold, MatKey.Velvet, MatKey.GhostGlow, MatKey.BlackGlass, MatKey.Moon };
                    var shelf = new GameObject("KeyShelf").transform; shelf.SetParent(_room.transform, false);
                    for (int i = 0; i < keys.Length; i++)
                    {
                        var go = GameObject.CreatePrimitive(i % 2 == 0 ? PrimitiveType.Cube : PrimitiveType.Sphere);
                        go.transform.SetParent(shelf, false);
                        go.transform.localPosition = new Vector3(-3.75f + i * 1.5f, 0.75f, -3.2f);
                        go.GetComponent<Renderer>().sharedMaterial = MaterialLibrary.Get(keys[i], theme);
                    }
                    Place(Room + new Vector3(0f, 2.2f, -8.4f), Room + new Vector3(0f, 0.8f, -3.2f), 50f);
                    ctx.Shot("matkeys_marble_gold_velvet_ghost_glass_moon", _cam);
                    WorldColor.Enabled = true; WorldColor.SetBase(0.25f);
                    yield return null;
                    ctx.Shot("matkeys_grey_world_gold_moon_ghost_keep_color", _cam);
                    WorldColor.Enabled = false;
                }
            }
        }

        // ------------------------------------------------------------------ immune parts stay saturated in a grey world
        static IEnumerator Immunity(DevPlayContext ctx)
        {
            yield return BuildRoom(LevelTheme.Training);
            var row = new GameObject("ImmuneRow").transform; row.SetParent(_room.transform, false);
            var mats = new[] { MaterialLibrary.Get(MatKey.Gold), MaterialLibrary.Get(MatKey.PuzzleBody), MaterialLibrary.Instance(new Color(1f, 0.3f, 0.25f), 0.3f),
                               MaterialLibrary.Get(MatKey.Barrel), MaterialLibrary.Get(MatKey.Trim) };
            var names = new[] { "Gold", "PuzzleBody", "InternInstance", "Barrel(not immune)", "Trim(not immune)" };
            var objs = new GameObject[mats.Length];
            for (int i = 0; i < mats.Length; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.transform.SetParent(row, false);
                go.transform.localPosition = new Vector3(-3f + i * 1.5f, 0.6f, -4.5f);
                go.transform.localScale = Vector3.one * 1.1f;
                go.GetComponent<Renderer>().sharedMaterial = mats[i];
                objs[i] = go;
            }
            Place(Room + new Vector3(0f, 1.6f, -9.5f), Room + new Vector3(0f, 0.6f, -4.5f), 45f);
            WorldColor.Enabled = true;
            WorldColor.SetBase(0f);
            yield return ctx.Wait(0.2f);
            var shot = ctx.Shot("immune_row_base0", _cam);
            // grey = luma * (0.93, 0.96, 1.04) (cool grey, HSV sat ~0.11) + the theme's PostFx grade, so "grey" measures ~0.15-0.2;
            // the check is relative to the weakest immune part.
            var sats = new float[objs.Length];
            for (int i = 0; i < objs.Length; i++)
            {
                var vp = _cam.WorldToViewportPoint(objs[i].transform.position + Vector3.up * 0.2f);
                sats[i] = RenderTestUtil.RegionSaturation(shot, new Rect(vp.x - 0.015f, vp.y - 0.015f, 0.03f, 0.03f));
                ctx.Log("immunity " + names[i] + " saturation at base 0: " + sats[i].ToString("0.00"));
            }
            float minImmune = Mathf.Min(sats[0], Mathf.Min(sats[1], sats[2]));
            for (int i = 0; i < objs.Length; i++)
            {
                bool shouldKeep = i < 3;
                ctx.Check(shouldKeep ? sats[i] > 0.35f : (sats[i] < 0.25f && sats[i] < minImmune * 0.4f),
                    names[i] + (shouldKeep ? " stays saturated" : " goes grey") + " (" + sats[i].ToString("0.00") + ")");
            }
            WorldColor.SetBase(1f);
            WorldColor.Enabled = false;
        }

        // ------------------------------------------------------------------ flashlight in Night
        static IEnumerator Flashlight(DevPlayContext ctx)
        {
            yield return BuildRoom(LevelTheme.Night);
            var cm = CharacterManager.Instance;
            var a = cm != null ? cm.Active : null;
            ctx.Check(a != null, "active intern for the flashlight");
            if (a == null) yield break;
            Vector3 spot = Room + new Vector3(0f, 0.05f, -8.5f);   // ~5 m of open floor before the L1 pods
            for (int i = 0; i < 3; i++)
            {
                var c = cm.Get(i); if (c == null) continue;
                c.ExternalControl = true;
                // the showcase room is render-only (no colliders): pin the team so it does not fall out of the shot
                if (c.Body != null) { if (!c.Body.isKinematic) _pinned.Add(c.Body); c.Body.isKinematic = true; }
                var pos = c == a ? spot : spot + new Vector3(i == 0 ? -2.2f : 2.2f, 0f, -1.2f + i * 0.3f);
                if (c.Body != null) { c.Body.position = pos; c.Body.linearVelocity = Vector3.zero; }
                c.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(new Vector3(0.25f, 0f, 1f)));
                if (c.Body != null) c.Body.rotation = c.transform.rotation;
            }
            LevelLighting.SetOverride(0.25f, 0.45f, 0f);
            var rig = FlashlightRig.Ensure();
            yield return ctx.Wait(0.6f);
            Place(Room + new Vector3(0.4f, 4.6f, -14f), Room + new Vector3(0.6f, 0.2f, -4f), 55f);
            ctx.Log("flashlight intern at " + (a.transform.position - Room).ToString("0.0") + " (room-local), light at " + (rig.Light.transform.position - Room).ToString("0.0"));
            ctx.Check((a.transform.position - (spot)).sqrMagnitude < 1f, "active intern stays on the night room floor for the flashlight shot");
            ctx.Shot("flashlight_night", _cam);
            ctx.Check(rig.Target == a && rig.LightOn, "flashlight on the active intern");
            ctx.Check(rig.Light.type == LightType.Spot && rig.Light.shadows == LightShadows.None && Mathf.Abs(rig.Light.spotAngle - 50f) < 0.01f && Mathf.Abs(rig.Light.range - 14f) < 0.01f, "one shadowless 50 deg / 14 m spot");
            int spots = 0; foreach (var l in Object.FindObjectsByType<Light>()) if (l.name == "Flashlight") spots++;
            ctx.Check(spots == 1, "exactly one flashlight light (" + spots + ")");
            ctx.Check(LightRegistryBridge.Available, "LightRegistry found (puzzles)");
            if (LightRegistryBridge.Available)
            {
                // 6 m ahead, or just short of the first solid surface on the beam (LOS is the registry's job; test the cone itself)
                float clear = 6f;
                if (Physics.Raycast(rig.Origin, rig.Forward, out var hit, 6f, ~0, QueryTriggerInteraction.Ignore) && PuzzleUtil.CharacterFrom(hit.collider) == null)
                    clear = Mathf.Max(1.2f, hit.distance - 0.6f);
                ctx.Log("flashlight beam clear for " + clear.ToString("0.0") + " m");
                var ahead = rig.Origin + rig.Forward * clear;
                var behind = rig.Origin - rig.Forward * 4f;
                ctx.Check(LightRegistry.IsLit(ahead, a.transform), "LightRegistry: point ahead on the beam is lit by the cone");
                ctx.Check(!LightRegistry.IsLit(behind, a.transform) || LightRegistry.InPool(behind), "LightRegistry: point behind is not lit by the cone");
            }
            // SHIFT: fade out/in over 0.3 s, then it is on the new intern
            int next = -1; for (int i = 0; i < 3; i++) if (cm.CanSwitchTo(i)) { next = i; break; }
            if (next >= 0)
            {
                cm.SwitchTo(next, true);
                yield return ctx.Wait(0.05f);
                ctx.Log("flashlight fade 0.05 s after SHIFT: " + rig.Fade.ToString("0.00"));
                yield return ctx.Wait(0.5f);
                ctx.Check(rig.Target == cm.Active && rig.Fade > 0.99f && rig.LightOn, "flashlight moved to the new intern after the 0.3 s fade");
                Place(Room + new Vector3(0.4f, 4.6f, -14f), Room + new Vector3(0.6f, 0.2f, -4f), 55f);
                ctx.Shot("flashlight_night_after_shift_glowsticks", _cam);
            }
            LevelLighting.ClearOverride(0f);
            foreach (var body in _pinned) if (body != null) body.isKinematic = false;
            _pinned.Clear();
        }
        static readonly System.Collections.Generic.List<Rigidbody> _pinned = new System.Collections.Generic.List<Rigidbody>();

        static IEnumerator Override(DevPlayContext ctx)
        {
            var sun = RenderSettings.sun;
            float i0 = sun != null ? sun.intensity : 1f;
            var amb0 = RenderSettings.ambientSkyColor;
            LevelLighting.SetOverride(0.1f, 0.2f, 0.3f);
            yield return ctx.Wait(0.5f);
            ctx.Check(sun == null || Mathf.Abs(sun.intensity - i0 * 0.1f) < 0.02f, "SetOverride dims the sun");
            ctx.Check(RenderSettings.ambientSkyColor.maxColorComponent < amb0.maxColorComponent * 0.3f + 0.01f, "SetOverride dims the ambient");
            Place(Room + new Vector3(0.6f, 3.9f, -10.2f), Room + new Vector3(0.2f, 1.3f, 1.5f), 55f);
            ctx.Shot("override_blackout_night", _cam);
            LevelLighting.ClearOverride(0.2f);
            yield return ctx.Wait(0.4f);
            ctx.Check(sun == null || Mathf.Abs(sun.intensity - i0) < 0.02f, "ClearOverride restores the sun");
            FlashlightRig.Remove();
        }

        // ------------------------------------------------------------------ vision cones
        static IEnumerator VisionCones(DevPlayContext ctx)
        {
            yield return BuildRoom(LevelTheme.Bank);
            var states = new[] { VisionConeMesh.State.Idle, VisionConeMesh.State.Spotting, VisionConeMesh.State.Alarm };
            VisionConeMesh last = null;
            for (int i = 0; i < 3; i++)
            {
                var eye = new GameObject("Eye" + i).transform;
                eye.SetParent(_room.transform, false);
                eye.localPosition = new Vector3(-5f + i * 5f, 3.2f, 4f);
                eye.localRotation = Quaternion.Euler(35f, 180f, 0f);
                var cone = VisionConeMesh.Create(eye, Vector3.zero, null, 7f, 40f, i == 2 ? VisionConeMesh.Style.Both : VisionConeMesh.Style.FloorFan);
                cone.SetState(states[i]);
                last = cone;
            }
            yield return ctx.Wait(0.2f);
            Place(Room + new Vector3(0f, 9f, -10f), Room + new Vector3(0f, 0f, 0.5f), 55f);
            ctx.Shot("vision_cones_idle_spotting_alarm", _cam);
            ctx.Check(last != null && last.CurrentState == VisionConeMesh.State.Alarm, "cone state set");
            var mf = last != null ? last.GetComponentInChildren<MeshFilter>() : null;
            ctx.Check(mf != null && mf.sharedMesh != null && mf.sharedMesh.vertexCount > 10, "fan mesh built");
        }

        // ------------------------------------------------------------------ Fx sheet
        static IEnumerator FxSheet(DevPlayContext ctx)
        {
            yield return BuildRoom(LevelTheme.Training);
            Vector3 c = Room + new Vector3(0f, 0f, 1f);
            Place(Room + new Vector3(0f, 4.5f, -9.5f), c + Vector3.up * 1.2f, 55f);
            var team = new Color(1f, 0.35f, 0.2f);
            Fx.ColorBurst(c, team, 6f); yield return ctx.Wait(0.35f); ctx.Shot("fx_color_burst", _cam); yield return ctx.Wait(1.2f);
            Fx.CoinShower(c, 80); yield return ctx.Wait(0.6f); ctx.Shot("fx_coin_shower", _cam); yield return ctx.Wait(2f);
            for (int i = 0; i < 6; i++) Fx.PaintSplat(c + new Vector3(-3f + i * 1.2f, 0.01f, -1f + (i % 2)), Vector3.up, Color.HSVToRGB(i / 6f, 0.85f, 1f), 1.1f);
            Fx.PaintSplat(Room + new Vector3(-2f, 2f, 7.4f), Vector3.back, new Color(0.2f, 0.8f, 1f), 1.6f);
            ctx.Check(Fx.PaintSplatCount == 7, "7 paint splats live (" + Fx.PaintSplatCount + ")");
            var any = GameObject.Find("PaintSplat");
            ctx.Check(any != null && WorldColor.IsImmune(any.GetComponent<Renderer>().sharedMaterial), "paint splats are grey-immune");
            yield return ctx.Wait(0.3f); ctx.Shot("fx_paint_splats", _cam);
            Fx.ShockwaveRing(c, 6f); yield return ctx.Wait(0.22f); ctx.Shot("fx_shockwave_ring", _cam); yield return ctx.Wait(0.8f);
            Fx.PaperBurst(c, 40); yield return ctx.Wait(0.6f); ctx.Shot("fx_paper_burst", _cam); yield return ctx.Wait(2f);
            Fx.ConfettiFlare(c + Vector3.up * 0.5f, new Color(1f, 0.3f, 0.8f)); yield return ctx.Wait(0.25f); ctx.Shot("fx_confetti_flare", _cam); yield return ctx.Wait(1.5f);
            Fx.GlitchSwap(c + Vector3.left * 3f, c + Vector3.right * 3f); yield return ctx.Wait(0.12f); ctx.Shot("fx_glitch_swap", _cam);
            yield return ctx.Wait(0.5f);
            Fx.ClearPaintSplats();
            ctx.Check(Fx.PaintSplatCount == 0, "ClearPaintSplats");
        }
    }
}
