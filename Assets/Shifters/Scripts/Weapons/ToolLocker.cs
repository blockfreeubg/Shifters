// =====================================================================================
//  SHIFTERS - ToolLocker.cs   [owned by the WEAPONS agent]
//  W5 TOOL LOCKER (WeaponKit.ToolLocker): an open-front office locker with the tool hovering
//  inside. E swaps the team's gun for the tool; E again swaps back (ToolSource). While the tool
//  is out the locker shows the stashed gun instead, with a "RETURN" glow.
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public class ToolLocker : ToolSource
    {
        [Tooltip("Spinning model root inside the locker.")] public Transform ModelRoot;
        [Tooltip("Back-panel glow (tool tint while IN, grey while OUT).")] public Renderer Glow;
        public Transform Door;

        GameObject _shown;
        float _spin;

        public override Vector3 InteractPoint => transform.TransformPoint(new Vector3(0f, 1.1f, -0.45f));

        protected override void OnEnable() { base.OnEnable(); Refresh(); }

        protected override void OnStateChanged() => Refresh();

        void Refresh()
        {
            if (ModelRoot == null) return;
            var data = IsOut ? (WeaponCustody.Original ?? WeaponCustody.LastRealGun) : ToolData;
            _shown = BuildModel(data, ModelRoot, IsOut ? 0.9f : 1.25f);
            if (Glow != null) Glow.sharedMaterial = MaterialLibrary.Unlit(IsOut ? UIColors.Hex(0x5A6270) : WeaponStyle.Tint(ToolKind), IsOut ? 0.6f : 1.3f);
            if (Door != null) Door.localRotation = Quaternion.Euler(0f, IsOut ? -105f : -70f, 0f);
        }

        protected override void Update()
        {
            base.Update();
            if (ModelRoot == null) return;
            _spin += Time.deltaTime * 70f;
            ModelRoot.localRotation = Quaternion.Euler(0f, _spin, 0f);
            ModelRoot.localPosition = new Vector3(0f, 1.15f + 0.05f * Mathf.Sin(Time.time * 2f), 0f);
        }
    }
}
