// =====================================================================================
//  SHIFTERS - L6ChapterDirector.cs   [CH03 - fight start + annex reactions]
//  * Begins THE DOWNSIZER once the active intern is inside the arena (z >= FightStartZ) and no cutscene
//    is playing (the ch03.intro ScriptTrigger plays C03 first when the story is on).
//  * Opens the annex whiteboard plan on the C04 beat "c04.whiteboard".
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public class L6ChapterDirector : MonoBehaviour
    {
        public L6DownsizerBoss Boss;
        public float FightStartZ = 11.2f;
        public TMPro.TextMeshPro Whiteboard;
        [TextArea] public string WhiteboardPlan = "THE PLAN\n1 WALK IN\n2 SNEAK DOWN\n3 PASS HIS EXAM\n4 OPEN THE VAULT\n5 ?";
        bool _cutscene;
        float _clear;

        void OnEnable() { GameEvents.CutsceneChanged += OnCutscene; GameEvents.StoryBeat += OnBeat; }
        void OnDisable() { GameEvents.CutsceneChanged -= OnCutscene; GameEvents.StoryBeat -= OnBeat; }
        void OnCutscene(bool on) { _cutscene = on; _clear = 0f; }
        void OnBeat(string id) { if (id == "c04.whiteboard" && Whiteboard != null) Whiteboard.text = WhiteboardPlan; }

        void Update()
        {
            if (Boss == null || Boss.State != BossState.Dormant) return;
            var a = BossTeam.Active;
            if (a == null || !a.IsAlive || a.Feet.z < FightStartZ) { _clear = 0f; return; }
            if (_cutscene) { _clear = 0f; return; }
            _clear += Time.deltaTime;
            if (_clear >= 0.6f) Boss.Begin();
        }
    }
}
