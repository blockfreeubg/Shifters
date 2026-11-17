// =====================================================================================
//  SHIFTERS - SceneLoader.cs   [owned by the CORE_SESSION agent]
//  Starts async scene loads by name with clear errors instead of exceptions. In the Editor a
//  scene that exists under Assets/Shifters/Scenes but is NOT in Build Settings is still loaded
//  (with a warning) via EditorSceneManager.LoadSceneAsyncInPlayMode, so levels are playable
//  before the build pipeline registers them. Player builds require Build Settings.
// =====================================================================================
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shifters
{
    public static class SceneLoader
    {
        /// <summary>True if the scene can be loaded right now (Build Settings, or Editor fallback by path).</summary>
        public static bool CanLoad(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return false;
            if (Application.CanStreamedLevelBeLoaded(sceneName)) return true;
#if UNITY_EDITOR
            return System.IO.File.Exists(LevelCatalog.ScenePath(sceneName));
#else
            return false;
#endif
        }

        /// <summary>Begin loading. Returns null and fills <paramref name="error"/> if it can't.</summary>
        public static AsyncOperation Begin(string sceneName, out string error)
        {
            error = null;
            if (string.IsNullOrEmpty(sceneName)) { error = "no scene name given"; return null; }
            try
            {
                if (Application.CanStreamedLevelBeLoaded(sceneName))
                {
                    var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
                    if (op == null) error = "SceneManager.LoadSceneAsync returned null for '" + sceneName + "'";
                    return op;
                }
#if UNITY_EDITOR
                string path = LevelCatalog.ScenePath(sceneName);
                if (System.IO.File.Exists(path))
                {
                    Debug.LogWarning("[GameSession] Scene '" + sceneName + "' is not in Build Settings - loading it from '" + path +
                                     "' (Editor only). Add it to Build Settings before making a build.");
                    var op = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Single));
                    if (op == null) error = "EditorSceneManager could not load '" + path + "'";
                    return op;
                }
#endif
                error = "Scene '" + sceneName + "' is not in Build Settings (expected at " + LevelCatalog.ScenePath(sceneName) +
                        "). Build the scenes / add them to File > Build Profiles first.";
                return null;
            }
            catch (System.Exception e)
            {
                error = "loading '" + sceneName + "' threw: " + e.Message;
                return null;
            }
        }

        /// <summary>Reload the active scene (works for scenes outside Build Settings in the Editor).</summary>
        public static AsyncOperation ReloadActive(out string error)
        {
            error = null;
            var scene = SceneManager.GetActiveScene();
            try
            {
                if (scene.buildIndex >= 0) return SceneManager.LoadSceneAsync(scene.buildIndex, LoadSceneMode.Single);
#if UNITY_EDITOR
                if (!string.IsNullOrEmpty(scene.path))
                    return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(scene.path, new LoadSceneParameters(LoadSceneMode.Single));
#endif
                if (!string.IsNullOrEmpty(scene.name) && Application.CanStreamedLevelBeLoaded(scene.name))
                    return SceneManager.LoadSceneAsync(scene.name, LoadSceneMode.Single);
                error = "active scene '" + scene.name + "' is not in Build Settings and has no asset path - can't reload it";
                return null;
            }
            catch (System.Exception e)
            {
                error = "reloading '" + scene.name + "' threw: " + e.Message;
                return null;
            }
        }
    }
}
