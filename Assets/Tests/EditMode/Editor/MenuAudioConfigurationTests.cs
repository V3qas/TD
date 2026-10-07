using NUnit.Framework;
using TD.Menu;
using TD.Level;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using TD.UI;

namespace TD.Tests.EditMode.Editor
{
    public class MenuAudioConfigurationTests
    {
        private static readonly string[] TitleMusicPaths =
        {
            "Assets/Art/Sound/Music/Title1.mp3",
            "Assets/Art/Sound/Music/Title2.mp3"
        };
        private static readonly string[] LevelMusicPaths =
        {
            "Assets/Art/Sound/Music/Fight1.mp3",
            "Assets/Art/Sound/Music/Fight2.mp3",
            "Assets/Art/Sound/Music/Fight3.mp3",
            "Assets/Art/Sound/Music/Fight4.mp3",
            "Assets/Art/Sound/Music/Fight5.mp3"
        };

        [Test]
        public void BootScene_AssignsTitleMusic()
        {
            WithScene("Assets/Scenes/Boot.unity", scene =>
            {
                TitleScreenController controller = FindComponent<TitleScreenController>(scene);
                Assert.That(controller, Is.Not.Null);
                AssertMusicClips(controller, "titleMusic", TitleMusicPaths);
            });
        }

        [Test]
        public void MenuScene_AssignsSameTitleMusic()
        {
            WithScene("Assets/Scenes/Menu.unity", scene =>
            {
                MainMenuController controller = FindComponent<MainMenuController>(scene);
                Assert.That(controller, Is.Not.Null);
                AssertMusicClips(controller, "titleMusic", TitleMusicPaths);
            });
        }

        [Test]
        public void GameplayScene_AssignsTitleMusicToRuntimeMapEditor()
        {
            WithScene("Assets/Scenes/Gameplay.unity", scene =>
            {
                RuntimeMapEditorController controller = FindComponent<RuntimeMapEditorController>(scene);
                Assert.That(controller, Is.Not.Null);
                AssertMusicClips(controller, "titleMusic", TitleMusicPaths);
                LevelLoader loader = FindComponent<LevelLoader>(scene);
                Assert.That(loader, Is.Not.Null);
                AssertMusicClips(loader, "levelMusic", LevelMusicPaths);
            });
        }

        private static void AssertMusicClips(Component controller, string propertyName, string[] paths)
        {
            SerializedProperty musicClips = new SerializedObject(controller).FindProperty(propertyName);
            Assert.That(musicClips, Is.Not.Null);
            Assert.That(musicClips.arraySize, Is.EqualTo(paths.Length));

            for (int index = 0; index < paths.Length; index++)
            {
                AudioClip expectedClip = AssetDatabase.LoadAssetAtPath<AudioClip>(paths[index]);
                Assert.That(expectedClip, Is.Not.Null, $"Music asset is missing: {paths[index]}");
                Assert.That(musicClips.GetArrayElementAtIndex(index).objectReferenceValue, Is.EqualTo(expectedClip));
            }
        }

        private static void WithScene(string path, System.Action<Scene> assertion)
        {
            Scene scene = SceneManager.GetSceneByPath(path);
            bool openedByTest = !scene.IsValid() || !scene.isLoaded;
            if (openedByTest)
                scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);

            try
            {
                assertion(scene);
            }
            finally
            {
                if (openedByTest && scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static T FindComponent<T>(Scene scene) where T : Component
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
            {
                T component = roots[index].GetComponentInChildren<T>(true);
                if (component != null)
                    return component;
            }

            return null;
        }
    }
}
