// =====================================================================================
//  SHIFTERS - BraveryTicker.cs   [owned by the ENEMIES agent]
//  Hidden runtime object (auto-created after the first scene load, survives loads) that runs
//  Bravery.Tick() at 4 Hz in scaled time.
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    [AddComponentMenu("")]
    public class BraveryTicker : MonoBehaviour
    {
        static BraveryTicker _instance;
        float _t;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _instance = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot() { Ensure(); }

        public static void Ensure()
        {
            if (_instance != null || !Application.isPlaying) return;
            var go = new GameObject("BraveryTicker") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<BraveryTicker>();
        }

        void Update()
        {
            _t -= Time.deltaTime;
            if (_t > 0f) return;
            _t = 1f / Bravery.TickHz;
            Bravery.Tick();
        }
    }
}
