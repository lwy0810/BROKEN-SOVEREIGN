using System.Collections.Generic;
using BrokenSovereign.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BrokenSovereign.Editor
{
    /// <summary>
    /// 타이틀 씬 생성 (10/26 인트로): Assets/Scenes/Title.unity = 카메라 + TitleScreen.
    /// 빌드 씬 순서를 [Title, World]로 맞춥니다 (나머지 씬은 그 뒤에 유지). 실행 후 Title 씬에서 Play.
    /// </summary>
    public static class TitleSceneTools
    {
        private const string TITLE_SCENE = "Assets/Scenes/Title.unity";
        private const string WORLD_SCENE = "Assets/Scenes/Levels/World.unity";

        [MenuItem("BrokenSovereign/Scenes/타이틀 씬 생성")]
        private static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            cameraObject.AddComponent<AudioListener>();

            new GameObject("TitleScreen").AddComponent<TitleScreen>();

            EditorSceneManager.SaveScene(scene, TITLE_SCENE);
            SetBuildScenes();
            Debug.Log($"TitleSceneTools: 타이틀 씬 생성 ({TITLE_SCENE}), 빌드 순서 Title → World. Play로 확인하세요.");
        }

        [MenuItem("BrokenSovereign/Scenes/빌드 씬 순서 맞추기 (Title → World)")]
        private static void SetBuildScenes()
        {
            var scenes = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(TITLE_SCENE, true),
                new EditorBuildSettingsScene(WORLD_SCENE, true),
            };

            foreach (EditorBuildSettingsScene existing in EditorBuildSettings.scenes)
            {
                if (existing.path != TITLE_SCENE && existing.path != WORLD_SCENE)
                {
                    scenes.Add(new EditorBuildSettingsScene(existing.path, false));
                }
            }

            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
