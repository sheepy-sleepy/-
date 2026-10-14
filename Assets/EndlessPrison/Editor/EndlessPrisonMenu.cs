using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace EndlessPrison.EditorTools
{
    /// <summary>
    /// Unity 上方選單多一個「無盡牢獄」，一鍵建立第一區場景並開始遊玩。
    /// </summary>
    public static class EndlessPrisonMenu
    {
        const string ScenePath = "Assets/EndlessPrison/Scenes/FirstArea.unity";

        [MenuItem("無盡牢獄/開啟第一區並遊玩")]
        static void OpenAndPlay()
        {
            if (!OpenOrCreate()) return;
            EditorApplication.EnterPlaymode();
        }

        [MenuItem("無盡牢獄/只開啟第一區場景")]
        static void OpenOnly()
        {
            OpenOrCreate();
        }

        static bool OpenOrCreate()
        {
            if (EditorApplication.isPlaying)
            {
                EditorApplication.ExitPlaymode();
                return false;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
            else
            {
                if (!AssetDatabase.IsValidFolder("Assets/EndlessPrison/Scenes"))
                    AssetDatabase.CreateFolder("Assets/EndlessPrison", "Scenes");

                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var go = new GameObject("GameManager");
                go.AddComponent<GameManager>();
                EditorSceneManager.SaveScene(scene, ScenePath);
                Debug.Log("[無盡牢獄] 已建立場景：" + ScenePath);
            }

            // 加進 Build Settings，之後打包也會包含這個場景
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!scenes.Exists(s => s.path == ScenePath))
            {
                scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }
            return true;
        }
    }
}
