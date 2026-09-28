using NUnit.Framework;
using TD.Menu;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using TD.UI;

namespace TD.Tests.EditMode.Editor
{
    public class MenuAudioConfigurationTests
    {
        private const string MusicPath = "Assets/Art/Sound/Music/Title_Main.mp3";
        private static readonly string[] LevelMusicPaths =
        {
            "Assets/Art/Sound/Music/Level1.mp3",
            "Assets/Art/Sound/Music/Level2.mp3"
        };

        [Test]
        public void BootScene_AssignsTitleMusic()
        {
            WithScene("Assets/Scenes/Boot.unity", scene =>
            {
                TitleScreenController controller = FindComponent<TitleScreenController>(scene);
                Assert.That(controller, Is.Not.Null);
                AssertTitleMusic(controller);
            });
        }

        [Test]
        public void MenuScene_AssignsSameTitleMusic()
        {
            WithScene("Assets/Scenes/Menu.unity", scene =>
            {
                MainMenuController controller = FindComponent<MainMenuController>(scene);
                Assert.That(controller, Is.Not.Null);
                AssertTitleMusic(controller);
                AssertLevelMusic(controller);
            });
        }

        [Test]
        public void GameplayScene_AssignsTitleMusicToRuntimeMapEditor()
        {
            WithScene("Assets/Scenes/Gameplay.unity", scene =>
            {
                RuntimeMapEditorController controller = FindComponent<RuntimeMapEditorController>(scene);
                Assert.That(controller, Is.Not.Null);
                AssertTitleMusic(controller);
            });
        }

        private static void AssertTitleMusic(Component controller)
        {
            AudioClip expectedClip = AssetDatabase.LoadAssetAtPath<AudioClip>(MusicPath);
            Assert.That(expectedClip, Is.Not.Null, "Title music asset is missing.");

            SerializedProperty titleMusic = new SerializedObject(controller).FindProperty("titleMusic");
            Assert.That(titleMusic, Is.Not.Null);
            Assert.That(titleMusic.objectReferenceValue, Is.EqualTo(expectedClip));
        }

        private static void AssertLevelMusic(MainMenuController controller)
        {
            SerializedProperty levelMusic = new SerializedObject(controller).FindProperty("levelMusic");
            Assert.That(levelMusic, Is.Not.Null);
            Assert.That(levelMusic.arraySize, Is.EqualTo(LevelMusicPaths.Length));

            for (int index = 0; index < LevelMusicPaths.Length; index++)
            {
                AudioClip expectedClip = AssetDatabase.LoadAssetAtPath<AudioClip>(LevelMusicPaths[index]);
                Assert.That(expectedClip, Is.Not.Null, $"Level music asset {index + 1} is missing.");
                Assert.That(levelMusic.GetArrayElementAtIndex(index).objectReferenceValue, Is.EqualTo(expectedClip));
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
