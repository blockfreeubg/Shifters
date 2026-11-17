// =====================================================================================
//  SHIFTERS - CharacterDisguise.cs   (VISUALS module, campaign wave)
//  Act II heist disguise: every intern wears a big fake handlebar mustache (CAMPAIGN 2.3).
//  Automatic in Bank-theme levels (the heist chapters), or forced with SetTeam(true/false).
//  Damage (a laser hit, a bonk) burns it off with a puff and the toast "MUSTACHE LOST".
//  Also the SCARED probe used by CharacterVisual (ShifterCharacter.IsScared, seams).
// =====================================================================================
using System.Collections.Generic;
using UnityEngine;

namespace Shifters
{
    public static class CharacterDisguise
    {
        /// <summary>null = automatic (on in Bank-theme levels), true/false = forced for every intern.</summary>
        public static bool? Override;
        /// <summary>Damage at or above this burns the mustache off (0 = any damage).</summary>
        public static float BurnDamage = 1f;
        /// <summary>Fake mustache color (dark brown, reads on every skin tone and in the grey world).</summary>
        public static readonly Color MustacheColor = new Color(0.16f, 0.1f, 0.07f);
        const string PartName = "DisguiseMustache";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Override = null; BurnDamage = 1f; }

        /// <summary>Should interns wear the disguise right now?</summary>
        public static bool Active
        {
            get
            {
                if (Override.HasValue) return Override.Value;
                var gs = GameSession.Instance;
                if (gs == null || gs.LevelIndex < 0) return false;
                var info = gs.CurrentLevel;
                return info != null && info.Theme == LevelTheme.Bank;
            }
        }

        /// <summary>Force the disguise on/off for every intern in the scene and the portraits (null = back to automatic).</summary>
        public static void SetTeam(bool? on)
        {
            Override = on;
            bool a = Active;
            var vis = Object.FindObjectsByType<CharacterVisual>();
            for (int i = 0; i < vis.Length; i++) vis[i].SetDisguise(a);
            var ps = PortraitStudio.Instance;
            if (ps != null) for (int i = 0; i < 3; i++) { var p = ps.GetPuppet(i); if (p != null) p.SetDisguise(a); }
        }

        public static bool HasMustache(CharacterRig rig) => rig != null && rig.Head != null && rig.Head.Find(PartName) != null;

        /// <summary>Add/remove the fake mustache on any intern rig (CharacterVisual or CharacterPuppet).</summary>
        public static void SetMustache(CharacterRig rig, bool on)
        {
            if (rig == null || rig.Head == null || rig.Face == null || rig is VillainRig) return;
            var existing = rig.Head.Find(PartName);
            if (on == (existing != null)) return;
            if (!on) { RemoveRenderer(rig, existing); existing.name = PartName + "_dead"; existing.SetParent(null, false); existing.gameObject.SetActive(false); Kit.SafeDestroy(existing.gameObject); return; }
            var face = rig.Face;
            var mouth = face.Mouth != null ? face.Mouth.localPosition : new Vector3(0f, rig.D.HeadCenterY, rig.D.HeadHalf.z);
            var eye = face.SocketL != null ? face.SocketL.localPosition : mouth + Vector3.up * 0.1f;
            float er = rig.D.EyeR, rx = rig.D.HeadHalf.x;
            float y = Mathf.Lerp(mouth.y, eye.y, 0.4f);
            var pos = new Vector3(mouth.x, y, mouth.z + er * 0.32f);
            string key = "disguise_mst_" + VisualMeshes.F(rx) + "_" + VisualMeshes.F(er);
            var mesh = ProcMesh.Cached(key, () =>
            {
                var b = new ProcMesh.Builder();
                float w = Mathf.Max(rx * 0.34f, er * 1.3f), th = Mathf.Max(er * 0.5f, w * 0.32f);
                for (int s = -1; s <= 1; s += 2)
                {
                    b.AddSphere(new Vector3(s * w * 0.55f, 0f, 0f), 1f, Quaternion.Euler(0f, -s * 12f, -s * 8f), Color.white, 10, 7, true, new Vector3(w * 0.62f, th * 0.5f, th * 0.5f));
                    b.AddSphere(new Vector3(s * w * 1.2f, th * 0.35f, -th * 0.25f), 1f, Quaternion.Euler(0f, -s * 25f, -s * 45f), Color.white, 8, 6, true, new Vector3(w * 0.34f, th * 0.28f, th * 0.28f));
                }
                return b.ToMesh("DisguiseMustache");
            });
            var go = ProcMesh.Part(PartName, rig.Head, mesh, MaterialLibrary.Colored(MustacheColor, 0.15f), pos, null, null, false);
            var r = go.GetComponent<Renderer>();
            if (r != null)
            {
                var list = new List<Renderer>(rig.Renderers); list.Add(r); rig.Renderers = list.ToArray();
                if (rig.IsGold) r.sharedMaterial = CharacterGold.DarkGold;
            }
        }

        static void RemoveRenderer(CharacterRig rig, Transform t)
        {
            var r = t.GetComponent<Renderer>(); if (r == null) return;
            var list = new List<Renderer>(rig.Renderers); list.Remove(r); rig.Renderers = list.ToArray();
        }

        /// <summary>Burn the mustache off (laser hit): puff + toast. Returns true if there was one.</summary>
        public static bool Burn(CharacterVisual v)
        {
            if (v == null || v.Rig == null || !HasMustache(v.Rig)) return false;
            var head = v.Rig.Head.Find(PartName);
            var pos = head != null ? head.position : v.HeadCenter;
            v.SetDisguise(false);
            if (Application.isPlaying)
            {
                Fx.Puff(pos, 0.45f, new Color(0.25f, 0.22f, 0.2f));
                GameEvents.RaiseToast("MUSTACHE LOST");
            }
            return true;
        }

        public static bool Burn(ShifterCharacter c) => c != null && Burn(c.GetComponent<CharacterVisual>());

        // ------------------------------------------------------------------ CharacterVisual hooks
        internal static void OnVisualBuilt(CharacterVisual v) { if (Active && v != null) v.SetDisguise(true); }

        internal static void OnDamaged(CharacterVisual v, float amount)
        {
            if (v != null && amount >= BurnDamage && amount > 0f) Burn(v);
        }

        // ------------------------------------------------------------------ SCARED probe (seams: ShifterCharacter.IsScared)
        /// <summary>ShifterCharacter.IsScared (seams), null-safe.</summary>
        public static bool ProbeScared(ShifterCharacter c) => c != null && c.IsScared;
    }
}
