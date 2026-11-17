using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shifters
{
    /// <summary>EnemyTests then the legacy BotTests in one Play session.
    /// Run: devplay.sh &lt;clone&gt; enemies_all -devSetup Shifters.EnemiesSuite.Run -devSeconds 200</summary>
    public static class EnemiesSuite
    {
        public static IEnumerator Run(DevPlayContext ctx)
        {
            yield return EnemyTests.Run(ctx);
            Clear();
            yield return null;
            yield return null;
            yield return BotTests.Run(ctx);
        }

        public static void Clear()
        {
            WeaponPickup.FallbackSpawn = null;
            var cam = Camera.main;
            foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (cam != null && go == cam.gameObject) continue;
                if (go.GetComponent<Light>() != null && go.GetComponent<Light>().type == LightType.Directional) continue;
                Object.Destroy(go);
            }
        }
    }
}
