using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shifters
{
    /// <summary>Runs WeaponTests then BotTests in one Play session (scene cleared in between).
    /// Run: devplay.sh &lt;clone&gt; we_all -devSetup Shifters.WeaponsEnemiesSuite.Run</summary>
    public static class WeaponsEnemiesSuite
    {
        public static IEnumerator Run(DevPlayContext ctx)
        {
            yield return WeaponTests.Run(ctx);
            Clear();
            yield return null;
            yield return null;
            yield return BotTests.Run(ctx);
        }

        static void Clear()
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
