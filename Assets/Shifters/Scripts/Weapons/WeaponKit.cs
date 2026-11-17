// =====================================================================================
//  SHIFTERS - WeaponKit.cs   [owned by the WEAPONS agent]
//  Runtime/edit-mode factories for weapon things placed in levels (Core/Kit.cs conventions).
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public static class WeaponKit
    {
        /// <summary>A floating weapon pickup ("[E] Pick up ..."). pos is the GROUND point (local to parent);
        /// the model hovers WeaponPickup.HoverHeight above it. data null => random weapon.</summary>
        public static WeaponPickup Pickup(Transform parent, Vector3 pos, WeaponData data)
        {
            if (data == null) data = WeaponGenerator.Generate(new System.Random(Mathf.RoundToInt(pos.x * 31f + pos.z * 17f) + 7));
            var go = Kit.Make("WeaponPickup_" + data.Type, parent, pos);
            var p = go.AddComponent<WeaponPickup>();
            p.Data = data;
            go.transform.position += Vector3.up * p.HoverHeight;

            // interaction volume (trigger on Ignore Raycast so camera/ground probes skip it)
            go.layer = 2;
            var trigger = go.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 0.7f;

            var visual = Kit.Make("Visual", go.transform, Vector3.zero);
            var modelRoot = Kit.Make("Model", visual.transform, Vector3.zero);
            modelRoot.transform.localScale = Vector3.one * 1.5f;
            GameObject model = null;
            try { model = WeaponModelBuilder.Build(data, modelRoot.transform); }
            catch (System.Exception e) { Debug.LogException(e); }
            if (model == null) { model = new GameObject("Weapon_" + data.Type); model.transform.SetParent(modelRoot.transform, false); }
            if (model.GetComponentInChildren<Renderer>() == null) WeaponFallbackModel.Build(data, model.transform);
            foreach (var c in model.GetComponentsInChildren<Collider>()) Kit.SafeDestroy(c);
            // center the gun on the spin axis
            var muzzle = WeaponHolder.FindDeep(model.transform, "Muzzle");
            if (muzzle != null) model.transform.localPosition = new Vector3(0f, 0f, -muzzle.localPosition.z * 0.5f);

            var ring = Kit.Make("Ring", go.transform, new Vector3(0f, -p.HoverHeight + 0.04f, 0f));
            var ringMesh = ProcMesh.Cached("pickup_ring_0.55_0.04", () => ProcMesh.Torus(0.55f, 0.04f, 24, 6));
            ProcMesh.AddRenderer(ring, ringMesh, MaterialLibrary.Colored(data.Tint, 0.2f, 2.5f), false);
            var disc = ProcMesh.Cached("pickup_disc_0.5", () => ProcMesh.Cylinder(0.5f, 0.02f, 24));
            ProcMesh.Part("Disc", ring.transform, disc, MaterialLibrary.Colored(UIColors.Hex(0x2A2F38), 0.4f), new Vector3(0f, -0.015f, 0f), null, null, false);
            var beacon = ProcMesh.Cached("pickup_beacon", () => ProcMesh.Cone(0.12f, 0.2f, 8));
            ProcMesh.Part("Arrow", visual.transform, beacon, MaterialLibrary.Colored(data.Tint, 0.2f, 2f), new Vector3(0f, 0.55f, 0f), Quaternion.Euler(180f, 0f, 0f), null, false);

            p.Visual = visual.transform;
            p.Ring = ring.transform;
            return p;
        }

        /// <summary>W5 TOOL LOCKER (IInteractable): E puts the team's gun inside and hands out the tool; E again swaps back.
        /// pos = floor point (local to parent). rot = the VIEWER's look rotation like EnvKit signs: the open front faces
        /// rot*back (pass EnvKit.FacingViewerAt(lockerPos, approachPos) or WallMountFacing(wallNormal)). Footprint 1.0 x 0.7 m,
        /// 2.5 m tall incl. the sign. Solid (blocks movement).</summary>
        public static ToolLocker ToolLocker(Transform parent, Vector3 pos, Quaternion? rot, ExperimentalVariant tool)
        {
            var kind = WeaponStyle.KindOf(tool);
            var go = Kit.Make("ToolLocker_" + tool, parent, pos, rot ?? Quaternion.identity);
            var t = go.transform;
            const float w = 1.0f, h = 2.1f, d = 0.7f, wall = 0.06f;
            // shell: back, sides, top, floor (open front toward local -Z)
            Kit.SolidBox(t, "Back", new Vector3(0f, h * 0.5f, d * 0.5f - wall * 0.5f), new Vector3(w, h, wall), MatKey.MetalDark);
            Kit.SolidBox(t, "SideL", new Vector3(-w * 0.5f + wall * 0.5f, h * 0.5f, 0f), new Vector3(wall, h, d), MatKey.PuzzleBody);
            Kit.SolidBox(t, "SideR", new Vector3(w * 0.5f - wall * 0.5f, h * 0.5f, 0f), new Vector3(wall, h, d), MatKey.PuzzleBody);
            Kit.SolidBox(t, "Top", new Vector3(0f, h - wall * 0.5f, 0f), new Vector3(w, wall, d), MatKey.PuzzleBody);
            Kit.SolidBox(t, "Base", new Vector3(0f, 0.15f, 0f), new Vector3(w, 0.3f, d), MatKey.PuzzleBody);
            // vents + trim
            for (int i = 0; i < 3; i++) Kit.BoxVisual(t, "Vent" + i, new Vector3(-w * 0.5f - 0.005f, h - 0.35f - i * 0.09f, 0f), new Vector3(0.012f, 0.03f, d * 0.6f), MatKey.Black, 0.005f, null, false);
            Kit.BoxVisual(t, "Trim", new Vector3(0f, 0.32f, -d * 0.5f), new Vector3(w, 0.05f, 0.03f), MatKey.PuzzleAccent, 0.01f, null, false);
            var glow = Kit.BoxVisual(t, "Glow", new Vector3(0f, 1.15f, d * 0.5f - wall - 0.012f), new Vector3(w - 0.2f, 1.3f, 0.02f), MatKey.Glow, 0.01f, null, false);
            // the open door (visual only)
            var hinge = Kit.Make("DoorHinge", t, new Vector3(-w * 0.5f, 0f, -d * 0.5f));
            Kit.BoxVisual(hinge.transform, "Door", new Vector3(w * 0.5f, h * 0.5f, 0f), new Vector3(w, h - 0.02f, 0.04f), MatKey.PuzzleBody, 0.02f);
            Kit.BoxVisual(hinge.transform, "Handle", new Vector3(w * 0.88f, 1.1f, -0.04f), new Vector3(0.04f, 0.22f, 0.04f), MatKey.PuzzleAccent, 0.01f, null, false);
            var model = Kit.Make("ToolModel", t, new Vector3(0f, 1.15f, 0f));
            // sign over the locker, readable from the front
            try { EnvKit.Sign(t, new Vector3(0f, h + 0.28f, -d * 0.5f + 0.05f), Quaternion.identity, "TOOL LOCKER: " + WeaponStyle.Label(kind), new Vector2(Mathf.Max(1.6f, 0.13f * (14 + WeaponStyle.Label(kind).Length)), 0.42f), SignStyle.Info); }
            catch (System.Exception e) { Debug.LogException(e); }
            // floor marker
            var ring = Kit.Make("FloorRing", t, new Vector3(0f, 0.02f, -d * 0.5f - 0.6f));
            ProcMesh.AddRenderer(ring, ProcMesh.Cached("locker_ring_0.5", () => ProcMesh.Torus(0.5f, 0.03f, 24, 4)), MaterialLibrary.Unlit(WeaponStyle.Tint(kind), 1.2f), false);

            var locker = go.AddComponent<ToolLocker>();
            locker.Tool = tool;
            locker.ModelRoot = model.transform;
            locker.Glow = glow;
            locker.Door = hinge.transform;
            locker.enabled = false; locker.enabled = true; // refresh with the references set
            return locker;
        }

        /// <summary>CH13 ARMORY: a row of pedestals (1.6 m apart along local X, centred on pos), one tool each, with a header
        /// sign. Same rules as lockers; the team holds one tool at a time. rot = the viewer's look rotation (front = rot*back).</summary>
        public static WeaponArmory Armory(Transform parent, Vector3 pos, Quaternion? rot, ExperimentalVariant[] tools)
        {
            var go = Kit.Make("Armory", parent, pos, rot ?? Quaternion.identity);
            var armory = go.AddComponent<WeaponArmory>();
            int n = tools != null ? tools.Length : 0;
            var slots = new ArmorySlot[n];
            const float spacing = 1.6f;
            float x0 = -(n - 1) * spacing * 0.5f;
            Kit.BoxVisual(go.transform, "Backboard", new Vector3(0f, 1.4f, 0.7f), new Vector3(Mathf.Max(2f, n * spacing + 0.4f), 2.8f, 0.1f), MatKey.MetalDark, 0.03f);
            try { EnvKit.Sign(go.transform, new Vector3(0f, 3.15f, 0.6f), Quaternion.identity, "ARMORY - ONE TOOL PER TEAM", new Vector2(Mathf.Max(3.2f, n * spacing * 0.8f), 0.5f), SignStyle.Warning); }
            catch (System.Exception e) { Debug.LogException(e); }
            for (int i = 0; i < n; i++)
            {
                var kind = WeaponStyle.KindOf(tools[i]);
                var sgo = Kit.Make("Slot_" + tools[i], go.transform, new Vector3(x0 + i * spacing, 0f, 0f));
                var st = sgo.transform;
                Kit.SolidBox(st, "Pedestal", new Vector3(0f, 0.4f, 0f), new Vector3(0.7f, 0.8f, 0.7f), MatKey.PuzzleBody);
                var glow = Kit.BoxVisual(st, "Glow", new Vector3(0f, 0.81f, 0f), new Vector3(0.6f, 0.02f, 0.6f), MatKey.Glow, 0.005f, null, false);
                var model = Kit.Make("ToolModel", st, new Vector3(0f, 1.15f, 0f));
                try { EnvKit.Sign(st, new Vector3(0f, 0.45f, -0.37f), Quaternion.identity, WeaponStyle.Label(kind), new Vector2(0.66f, 0.26f), SignStyle.Info); }
                catch (System.Exception e) { Debug.LogException(e); }
                var slot = sgo.AddComponent<ArmorySlot>();
                slot.Tool = tools[i];
                slot.ModelRoot = model.transform;
                slot.Glow = glow;
                slot.Armory = armory;
                slot.enabled = false; slot.enabled = true;
                slots[i] = slot;
            }
            armory.Slots = slots;
            return armory;
        }

        /// <summary>A pickup with a random weapon from a seed (deterministic).</summary>
        public static WeaponPickup RandomPickup(Transform parent, Vector3 pos, int seed)
            => Pickup(parent, pos, WeaponGenerator.Generate(new System.Random(seed)));
    }
}
