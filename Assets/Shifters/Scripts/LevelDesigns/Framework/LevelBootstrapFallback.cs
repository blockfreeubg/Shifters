// =====================================================================================
//  SHIFTERS - LevelBootstrapFallback.cs   (BUILDER module - runtime level framework)
//  Minimal stand-in for LevelManager, added by the assembler ONLY when the LevelManager type does not
//  exist yet (Level module unpublished). Spawns the team at the start spawn points and points the camera
//  at the active character. Does nothing if a real LevelManager is present. Rebuild scenes once the
//  Level module is published to replace it with the real LevelManager.
// =====================================================================================
using System.Collections.Generic;
using UnityEngine;

namespace Shifters
{
    public class LevelBootstrapFallback : MonoBehaviour
    {
        public int LevelIndex;
        public Transform[] SpawnPoints = new Transform[0];

        void Start()
        {
            var lmType = KitCatalog.FindType("LevelManager");
            if (lmType != null && FindAnyObjectByType(lmType) != null) { enabled = false; return; }
            if (CharacterManager.Instance != null && CharacterManager.Instance.Team.Count > 0) return;

            var session = GameSession.Instance;
            session?.EnsureTeam();
            var team = session != null && session.Team != null ? session.Team : CharacterGenerator.GenerateTeam(12345);

            var poses = new List<Pose>(3);
            if (SpawnPoints != null) foreach (var t in SpawnPoints) if (t != null) poses.Add(new Pose(t.position + Vector3.up * 0.05f, t.rotation));
            if (poses.Count == 0) poses.Add(new Pose(transform.position, transform.rotation));

            var cmGo = new GameObject("CharacterManager");
            var cm = cmGo.AddComponent<CharacterManager>();
            if (FindAnyObjectByType<CharacterSwitcher>() == null) cmGo.AddComponent<CharacterSwitcher>();
            cm.SpawnTeam(team, poses, 0);

            var cam = ShiftersCamera.Instance;
            if (cam == null && Camera.main != null) cam = Camera.main.GetComponent<ShiftersCamera>() ?? Camera.main.gameObject.AddComponent<ShiftersCamera>();
            if (cam != null && cm.Active != null) cam.SetTarget(cm.Active, true);
            UIManager.Instance?.ShowGameplayHUD();
            Debug.Log("[LevelBootstrapFallback] spawned the team without a LevelManager (level index " + LevelIndex + ")");
        }
    }
}
