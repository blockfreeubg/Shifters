// =====================================================================================
//  SHIFTERS - ArmorySlot.cs   [owned by the WEAPONS agent]
//  One pedestal of the CH13 ARMORY (WeaponKit.Armory): a tool hovering over a lit pedestal.
//  Same exchange rules as a ToolLocker (ToolSource); picking another slot returns this tool.
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public class ArmorySlot : ToolSource
    {
        public Transform ModelRoot;
        public Renderer Glow;
        public WeaponArmory Armory;

        float _spin;

        public override Vector3 InteractPoint => transform.position + Vector3.up * 1.0f;

        protected override void OnEnable() { base.OnEnable(); Refresh(); }
        protected override void OnStateChanged() { Refresh(); if (Armory != null) Armory.NotifyChanged(this); }

        void Refresh()
        {
            if (ModelRoot == null) return;
            BuildModel(IsOut ? null : ToolData, ModelRoot, 1.2f);
            if (Glow != null) Glow.sharedMaterial = MaterialLibrary.Unlit(IsOut ? UIColors.Hex(0x5A6270) : WeaponStyle.Tint(ToolKind), IsOut ? 0.5f : 1.4f);
        }

        protected override void Update()
        {
            base.Update();
            if (ModelRoot == null) return;
            _spin += Time.deltaTime * 60f;
            ModelRoot.localRotation = Quaternion.Euler(0f, _spin, 0f);
            ModelRoot.localPosition = new Vector3(0f, 1.15f + 0.05f * Mathf.Sin(Time.time * 2f + transform.position.x), 0f);
        }
    }
}
