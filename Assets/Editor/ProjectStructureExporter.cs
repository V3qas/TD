using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TD.Editor
{
    public static class AssetsStructureExporter
    {
        private const string ExportFolderName = "AssetsExport";

        private static readonly string[] IncludedFolders =
        {
            "Assets/Scripts",
            "Assets/Scenes",
            "Assets/ScriptableObjects",
            "Assets/Prefabs"
        };

        private static readonly string[] IncludedExtensions =
        {
            ".cs",
            ".unity",
            ".asset",
            ".prefab",
            ".json",
            ".txt"
        };

        [MenuItem("Tools/Export/Export Assets Folder To TXT")]
        public static void ExportAssetsFolderToTxt()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
            string exportRoot = Path.Combine(projectRoot, ExportFolderName);

            if (Directory.Exists(exportRoot))
            {
                Directory.Delete(exportRoot, true);
            }

            Directory.CreateDirectory(exportRoot);

            string summaryPath = Path.Combine(exportRoot, "assets_summary.txt");
            string treePath = Path.Combine(exportRoot, "assets_tree.txt");
            string projectInfoPath = Path.Combine(exportRoot, "project_info.txt");
            string sceneSummariesFolder = Path.Combine(exportRoot, "SceneSummaries");
            string assetSummariesFolder = Path.Combine(exportRoot, "AssetSummaries");
            string prefabSummariesFolder = Path.Combine(exportRoot, "PrefabSummaries");

            Directory.CreateDirectory(sceneSummariesFolder);
            Directory.CreateDirectory(assetSummariesFolder);
            Directory.CreateDirectory(prefabSummariesFolder);

            SceneSetup[] originalSceneSetup = EditorSceneManager.GetSceneManagerSetup();

            try
            {
                ExportAssetsTree(treePath);
                ExportSceneSummaries(sceneSummariesFolder);
                ExportScriptableObjectSummaries(assetSummariesFolder);
                ExportPrefabSummaries(prefabSummariesFolder);
                ExportProjectInfo(projectInfoPath);
                ExportSummary(summaryPath);

                AssetDatabase.Refresh();
                Debug.Log($"Assets-Export erstellt: {exportRoot}");
            }
            finally
            {
                EditorSceneManager.RestoreSceneManagerSetup(originalSceneSetup);
            }
        }

        private static void ExportAssetsTree(string outputPath)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("ASSETS TREE");
            sb.AppendLine();

            foreach (string relativeFolder in IncludedFolders)
            {
                string fullFolderPath = Path.Combine(
                    Directory.GetParent(Application.dataPath)!.FullName,
                    relativeFolder.Replace('/', Path.DirectorySeparatorChar)
                );

                if (!Directory.Exists(fullFolderPath))
                    continue;

                AppendDirectoryTree(fullFolderPath, sb, 0, relativeFolder);
                sb.AppendLine();
            }

            File.WriteAllText(outputPath, sb.ToString(), Encoding.UTF8);
        }

        private static void AppendDirectoryTree(string directoryPath, StringBuilder sb, int depth, string relativePathOverride = null)
        {
            string indent = new string(' ', depth * 2);
            string displayName = relativePathOverride ?? Path.GetFileName(directoryPath);

            sb.AppendLine($"{indent}[Folder] {displayName}");

            string[] subDirectories = Directory.GetDirectories(directoryPath);
            Array.Sort(subDirectories, StringComparer.OrdinalIgnoreCase);

            foreach (string subDirectory in subDirectories)
            {
                string relativePath = GetRelativeAssetsPath(subDirectory);
                AppendDirectoryTree(subDirectory, sb, depth + 1, relativePath);
            }

            string[] files = Directory.GetFiles(directoryPath);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);

            foreach (string file in files)
            {
                string extension = Path.GetExtension(file);

                if (extension.Equals(".meta", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!IncludedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
                    continue;

                sb.AppendLine($"{indent}  [File] {Path.GetFileName(file)}");
            }
        }

        private static void ExportPrefabSummaries(string prefabSummariesFolder)
        {
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" });

            foreach (string guid in prefabGuids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);

                if (prefab == null)
                    continue;

                StringBuilder sb = new StringBuilder();
                sb.AppendLine("PREFAB SUMMARY");
                sb.AppendLine($"Name: {prefab.name}");
                sb.AppendLine($"Path: {assetPath}");
                sb.AppendLine();

                AppendGameObjectSummary(sb, prefab, 0);

                string outputPath = Path.Combine(
                    prefabSummariesFolder,
                    $"{MakeSafeFileName(prefab.name)}_summary.txt"
                );

                File.WriteAllText(outputPath, sb.ToString(), Encoding.UTF8);
            }
        }

        private static void ExportProjectInfo(string outputPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
            StringBuilder sb = new StringBuilder();

            sb.AppendLine("PROJECT INFO");
            sb.AppendLine();
            sb.AppendLine($"Unity Version: {Application.unityVersion}");
            sb.AppendLine($"Product Name: {Application.productName}");
            sb.AppendLine($"Company Name: {Application.companyName}");
            sb.AppendLine();

            sb.AppendLine("Build Settings Scenes (in order):");
            EditorBuildSettingsScene[] buildScenes = EditorBuildSettings.scenes;
            for (int i = 0; i < buildScenes.Length; i++)
            {
                EditorBuildSettingsScene s = buildScenes[i];
                sb.AppendLine($"  [{i}] enabled={s.enabled} {s.path}");
            }
            sb.AppendLine();

            sb.AppendLine("Tags:");
            foreach (string tag in UnityEditorInternal.InternalEditorUtility.tags)
            {
                sb.AppendLine($"  - {tag}");
            }
            sb.AppendLine();

            sb.AppendLine("Layers:");
            for (int i = 0; i < 32; i++)
            {
                string name = LayerMask.LayerToName(i);
                if (!string.IsNullOrEmpty(name))
                    sb.AppendLine($"  [{i}] {name}");
            }
            sb.AppendLine();

            sb.AppendLine("Sorting Layers:");
            foreach (UnityEngine.SortingLayer layer in UnityEngine.SortingLayer.layers)
            {
                sb.AppendLine($"  [{layer.id}] {layer.name} (value={layer.value})");
            }
            sb.AppendLine();

            sb.AppendLine("Assembly Definitions (.asmdef):");
            string[] asmdefGuids = AssetDatabase.FindAssets("t:AssemblyDefinitionAsset", new[] { "Assets" });
            foreach (string guid in asmdefGuids)
            {
                string asmdefPath = AssetDatabase.GUIDToAssetPath(guid);
                sb.AppendLine($"  {asmdefPath}");
                try
                {
                    string json = File.ReadAllText(Path.Combine(projectRoot, asmdefPath), Encoding.UTF8);
                    foreach (string line in json.Split('\n'))
                    {
                        sb.AppendLine($"    {line.TrimEnd('\r')}");
                    }
                }
                catch (Exception ex)
                {
                    sb.AppendLine($"    <unreadable: {ex.Message}>");
                }
            }
            sb.AppendLine();

            string manifestPath = Path.Combine(projectRoot, "Packages", "manifest.json");
            if (File.Exists(manifestPath))
            {
                sb.AppendLine("Packages/manifest.json:");
                try
                {
                    foreach (string line in File.ReadAllLines(manifestPath, Encoding.UTF8))
                    {
                        sb.AppendLine($"  {line}");
                    }
                }
                catch (Exception ex)
                {
                    sb.AppendLine($"  <unreadable: {ex.Message}>");
                }
                sb.AppendLine();
            }

            File.WriteAllText(outputPath, sb.ToString(), Encoding.UTF8);
        }

        private static void ExportSceneSummaries(string sceneSummariesFolder)
        {
            string[] sceneGuids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" });

            foreach (string guid in sceneGuids)
            {
                string scenePath = AssetDatabase.GUIDToAssetPath(guid);
                Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

                StringBuilder sb = new StringBuilder();
                sb.AppendLine($"SCENE SUMMARY");
                sb.AppendLine($"Name: {scene.name}");
                sb.AppendLine($"Path: {scenePath}");
                sb.AppendLine();

                GameObject[] rootObjects = scene.GetRootGameObjects();
                foreach (GameObject rootObject in rootObjects)
                {
                    AppendGameObjectSummary(sb, rootObject, 0);
                    sb.AppendLine();
                }

                string outputPath = Path.Combine(
                    sceneSummariesFolder,
                    $"{MakeSafeFileName(scene.name)}_summary.txt"
                );

                File.WriteAllText(outputPath, sb.ToString(), Encoding.UTF8);
            }
        }

        private static void AppendGameObjectSummary(StringBuilder sb, GameObject gameObject, int depth)
        {
            string indent = new string(' ', depth * 2);
            Transform t = gameObject.transform;

            sb.AppendLine($"{indent}GameObject: {gameObject.name}");
            sb.AppendLine($"{indent}  ActiveSelf: {gameObject.activeSelf}");
            sb.AppendLine($"{indent}  ActiveInHierarchy: {gameObject.activeInHierarchy}");
            sb.AppendLine($"{indent}  Tag: {gameObject.tag}");
            sb.AppendLine($"{indent}  Layer: {LayerMask.LayerToName(gameObject.layer)} ({gameObject.layer})");
            sb.AppendLine($"{indent}  LocalPosition: {t.localPosition}");
            sb.AppendLine($"{indent}  LocalRotationEuler: {t.localRotation.eulerAngles}");
            sb.AppendLine($"{indent}  LocalScale: {t.localScale}");

            Component[] components = gameObject.GetComponents<Component>();
            sb.AppendLine($"{indent}  Components:");

            foreach (Component component in components)
            {
                if (component == null)
                {
                    sb.AppendLine($"{indent}    Missing Script");
                    continue;
                }

                Type componentType = component.GetType();
                sb.AppendLine($"{indent}    {componentType.Name}");

                AppendSerializedFields(sb, component, indent + "      ");
            }

            for (int i = 0; i < t.childCount; i++)
            {
                AppendGameObjectSummary(sb, t.GetChild(i).gameObject, depth + 1);
            }
        }

        private static void AppendSerializedFields(StringBuilder sb, object target, string indent)
        {
            Type type = target.GetType();
            FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            foreach (FieldInfo field in fields)
            {
                bool isPublic = field.IsPublic;
                bool hasSerializeField = field.GetCustomAttribute<SerializeField>() != null;
                bool isSerialized = isPublic || hasSerializeField;

                if (!isSerialized)
                    continue;

                if (field.IsDefined(typeof(NonSerializedAttribute), true))
                    continue;

                object value;
                try
                {
                    value = field.GetValue(target);
                }
                catch
                {
                    sb.AppendLine($"{indent}{field.Name}: <unreadable>");
                    continue;
                }

                sb.AppendLine($"{indent}{field.Name}: {FormatValue(value)}");
            }
        }

        private static void ExportScriptableObjectSummaries(string assetSummariesFolder)
        {
            string[] guids = AssetDatabase.FindAssets("t:ScriptableObject", new[] { "Assets/ScriptableObjects" });

            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                ScriptableObject so = AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath);

                if (so == null)
                    continue;

                StringBuilder sb = new StringBuilder();
                sb.AppendLine("SCRIPTABLE OBJECT SUMMARY");
                sb.AppendLine($"Name: {so.name}");
                sb.AppendLine($"Path: {assetPath}");
                sb.AppendLine($"Type: {so.GetType().FullName}");
                sb.AppendLine();

                AppendSerializedFields(sb, so, "");

                string outputPath = Path.Combine(
                    assetSummariesFolder,
                    $"{MakeSafeFileName(so.name)}_summary.txt"
                );

                File.WriteAllText(outputPath, sb.ToString(), Encoding.UTF8);
            }
        }

        private static void ExportSummary(string outputPath)
        {
            List<string> allFiles = GetIncludedFiles().ToList();

            int csCount = 0;
            int sceneCount = 0;
            int assetCount = 0;
            int prefabCount = 0;
            int jsonCount = 0;
            int txtCount = 0;

            HashSet<string> allFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string includedFolder in IncludedFolders)
            {
                string fullFolderPath = Path.Combine(
                    Directory.GetParent(Application.dataPath)!.FullName,
                    includedFolder.Replace('/', Path.DirectorySeparatorChar)
                );

                if (!Directory.Exists(fullFolderPath))
                    continue;

                allFolders.Add(fullFolderPath);

                foreach (string subDir in Directory.GetDirectories(fullFolderPath, "*", SearchOption.AllDirectories))
                {
                    allFolders.Add(subDir);
                }
            }

            foreach (string file in allFiles)
            {
                string extension = Path.GetExtension(file).ToLowerInvariant();

                switch (extension)
                {
                    case ".cs":
                        csCount++;
                        break;
                    case ".unity":
                        sceneCount++;
                        break;
                    case ".asset":
                        assetCount++;
                        break;
                    case ".prefab":
                        prefabCount++;
                        break;
                    case ".json":
                        jsonCount++;
                        break;
                    case ".txt":
                        txtCount++;
                        break;
                }
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("ASSETS SUMMARY");
            sb.AppendLine();
            sb.AppendLine("Included Folders:");
            foreach (string folder in IncludedFolders)
            {
                sb.AppendLine($"- {folder}");
            }

            sb.AppendLine();
            sb.AppendLine($"Folders: {allFolders.Count}");
            sb.AppendLine($"C# Scripts (.cs): {csCount}");
            sb.AppendLine($"Scenes (.unity): {sceneCount}");
            sb.AppendLine($"Assets (.asset): {assetCount}");
            sb.AppendLine($"Prefabs (.prefab): {prefabCount}");
            sb.AppendLine($"JSON (.json): {jsonCount}");
            sb.AppendLine($"Text files (.txt): {txtCount}");
            sb.AppendLine();

            sb.AppendLine("Generated Outputs:");
            sb.AppendLine("- assets_tree.txt");
            sb.AppendLine("- project_info.txt");
            sb.AppendLine("- SceneSummaries/*");
            sb.AppendLine("- AssetSummaries/*");
            sb.AppendLine("- PrefabSummaries/*");
            sb.AppendLine();

            sb.AppendLine("Hinweis:");
            sb.AppendLine("- Exportiert werden nur ausgewählte relevante Ordner unter Assets/");
            sb.AppendLine("- .meta-Dateien werden ignoriert");
            sb.AppendLine("- Szenen werden nur temporär zum Lesen geöffnet");
            sb.AppendLine("- Die vorherige Szenenkonfiguration wird am Ende wiederhergestellt");
            sb.AppendLine("- Roh-Dateiinhalte (.cs/.unity/.asset/.prefab) werden NICHT mehr exportiert,");
            sb.AppendLine("  da sie 1:1 im Repo liegen. Stattdessen Summaries fuer Szenen/SOs/Prefabs.");

            File.WriteAllText(outputPath, sb.ToString(), Encoding.UTF8);
        }

        private static IEnumerable<string> GetIncludedFiles()
        {
            List<string> results = new List<string>();

            foreach (string includedFolder in IncludedFolders)
            {
                string fullFolderPath = Path.Combine(
                    Directory.GetParent(Application.dataPath)!.FullName,
                    includedFolder.Replace('/', Path.DirectorySeparatorChar)
                );

                if (!Directory.Exists(fullFolderPath))
                    continue;

                string[] files = Directory.GetFiles(fullFolderPath, "*", SearchOption.AllDirectories);

                foreach (string file in files)
                {
                    string extension = Path.GetExtension(file);

                    if (extension.Equals(".meta", StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (!IncludedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
                        continue;

                    results.Add(file);
                }
            }

            return results.Distinct(StringComparer.OrdinalIgnoreCase);
        }

        private static string FormatValue(object value)
        {
            if (value == null)
                return "null";

            if (value is string s)
                return $"\"{s}\"";

            if (value is UnityEngine.Object unityObject)
            {
                if (unityObject == null)
                    return "null";

                string assetPath = AssetDatabase.GetAssetPath(unityObject);

                if (!string.IsNullOrEmpty(assetPath))
                {
                    return $"{unityObject.name} [{unityObject.GetType().Name}] -> {assetPath}";
                }

                if (unityObject is Component component)
                {
                    return $"{component.gameObject.name} [{component.GetType().Name}]";
                }

                if (unityObject is GameObject go)
                {
                    return $"{go.name} [GameObject]";
                }

                return $"{unityObject.name} [{unityObject.GetType().Name}]";
            }

            if (value is IEnumerable enumerable && value is not string)
            {
                List<string> items = new List<string>();

                foreach (object item in enumerable)
                {
                    items.Add(FormatValue(item));
                }

                return $"[{string.Join(", ", items)}]";
            }

            return value.ToString();
        }

        private static string GetRelativeAssetsPath(string fullPath)
        {
            string normalizedAssetsPath = Application.dataPath.Replace('\\', '/');
            string normalizedFullPath = fullPath.Replace('\\', '/');

            if (normalizedFullPath.StartsWith(normalizedAssetsPath, StringComparison.OrdinalIgnoreCase))
            {
                return "Assets" + normalizedFullPath.Substring(normalizedAssetsPath.Length);
            }

            return fullPath;
        }

        private static string MakeSafeFileName(string input)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                input = input.Replace(c, '_');
            }

            input = input.Replace('/', '_').Replace('\\', '_').Replace(':', '_');
            return input;
        }
    }
}
