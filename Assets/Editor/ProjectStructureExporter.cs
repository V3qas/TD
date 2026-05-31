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
        string fileContentsFolder = Path.Combine(exportRoot, "FileContents");
        string sceneSummariesFolder = Path.Combine(exportRoot, "SceneSummaries");
        string assetSummariesFolder = Path.Combine(exportRoot, "AssetSummaries");

        Directory.CreateDirectory(fileContentsFolder);
        Directory.CreateDirectory(sceneSummariesFolder);
        Directory.CreateDirectory(assetSummariesFolder);

        SceneSetup[] originalSceneSetup = EditorSceneManager.GetSceneManagerSetup();

        try
        {
            ExportAssetsTree(treePath);
            ExportAssetsFiles(fileContentsFolder);
            ExportSceneSummaries(sceneSummariesFolder);
            ExportScriptableObjectSummaries(assetSummariesFolder);
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

    private static void ExportAssetsFiles(string contentFolder)
    {
        foreach (string extension in IncludedExtensions)
        {
            ExportFilesByExtension(contentFolder, extension);
        }
    }

    private static void ExportFilesByExtension(string contentFolder, string extension)
    {
        IEnumerable<string> files = GetIncludedFiles()
            .Where(path => Path.GetExtension(path).Equals(extension, StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);

        foreach (string fullPath in files)
        {
            if (!File.Exists(fullPath))
                continue;

            string relativePath = GetRelativeAssetsPath(fullPath);
            string safeRelativeName = MakeSafeFileName(relativePath) + ".txt";
            string outputPath = Path.Combine(contentFolder, safeRelativeName);

            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"ASSET PATH: {relativePath}");
            sb.AppendLine($"FILE TYPE: {extension}");
            sb.AppendLine();

            try
            {
                string content = File.ReadAllText(fullPath, Encoding.UTF8);
                sb.AppendLine(content);
            }
            catch (Exception ex)
            {
                sb.AppendLine($"<Could not read file: {ex.Message}>");
            }

            File.WriteAllText(outputPath, sb.ToString(), Encoding.UTF8);
        }
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
        sb.AppendLine("- SceneSummaries/*");
        sb.AppendLine("- AssetSummaries/*");
        sb.AppendLine("- FileContents/*");
        sb.AppendLine();

        sb.AppendLine("Hinweis:");
        sb.AppendLine("- Exportiert werden nur ausgewählte relevante Ordner unter Assets/");
        sb.AppendLine("- .meta-Dateien werden ignoriert");
        sb.AppendLine("- Szenen werden nur temporär zum Lesen geöffnet");
        sb.AppendLine("- Die vorherige Szenenkonfiguration wird am Ende wiederhergestellt");

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