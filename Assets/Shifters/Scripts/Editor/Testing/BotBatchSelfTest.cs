// =====================================================================================
//  SHIFTERS - BotBatchSelfTest.cs   [BOTS module - editor self-test]
//  Shifters.EditorTools.BotBatchSelfTest.Run : proves the multi-scene smoke runner end to end
//  without real level scenes. Creates two throwaway scenes in the folder
//  Assets/Shifters/Scenes/_BotSelfTest/ (a clean one and one with a NULL material that the audit
//  must flag), runs BotBatch.SmokeAllScenes on just those, and deletes them again on exit.
//  Expected output: "[SMOKE] SUMMARY BotSelfTest_Clean: PASS", "... BotSelfTest_Broken: FAIL", exit 2.
//  Never run on the real project; scenes are removed afterwards either way.
// =====================================================================================
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Shifters.EditorTools
{
    public static class BotBatchSelfTest
    {
        const string Folder = "Assets/Shifters/Scenes/_BotSelfTest";

        public static void Run()
        {
            Directory.CreateDirectory(Folder);
            MakeScene("BotSelfTest_Clean", false);
            MakeScene("BotSelfTest_Broken", true);
            AssetDatabase.Refresh();
            BotBatch.Finished = Cleanup;
            BotBatch.RunScenes(new[] { Folder + "/BotSelfTest_Clean.unity", Folder + "/BotSelfTest_Broken.unity" });
        }

        static void MakeScene(string name, bool broken)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            if (broken)
            {
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = "BrokenCube";
                cube.transform.position = new Vector3(0f, 1f, -5f);
                cube.GetComponent<MeshRenderer>().sharedMaterial = null;
            }
            EditorSceneManager.SaveScene(scene, Folder + "/" + name + ".unity");
        }

        static void Cleanup()
        {
            if (AssetDatabase.IsValidFolder(Folder)) AssetDatabase.DeleteAsset(Folder);
        }
    }
}
