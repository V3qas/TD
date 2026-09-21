using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace TD.Level
{
    public static class CustomMapStorage
    {
        private const string LegacyPlayerPrefsKey = "TD.CustomMaps.v1";

        private static CustomMapStore CreateStore()
        {
            return new CustomMapStore(
                Path.Combine(Application.persistentDataPath, "customMaps.json"),
                () => PlayerPrefs.GetString(LegacyPlayerPrefsKey, string.Empty),
                () =>
                {
                    PlayerPrefs.DeleteKey(LegacyPlayerPrefsKey);
                    PlayerPrefs.Save();
                });
        }

        public static List<CustomMapEntry> GetAll()
        {
            if (CreateStore().TryLoad(out CustomMapCollection collection, out string error))
                return new List<CustomMapEntry>(collection.maps);

            Debug.LogError($"CustomMapStorage: {error}");
            return new List<CustomMapEntry>();
        }

        public static bool Save(string seed, out CustomMapEntry savedEntry, out string error)
        {
            return CreateStore().Save(seed, out savedEntry, out error);
        }
    }

    internal sealed class CustomMapStore
    {
        private const int MaxCustomMaps = 100;
        private readonly string filePath;
        private readonly Func<string> readLegacy;
        private readonly Action clearLegacy;

        internal CustomMapStore(string filePath, Func<string> readLegacy = null, Action clearLegacy = null)
        {
            this.filePath = filePath;
            this.readLegacy = readLegacy;
            this.clearLegacy = clearLegacy;
        }

        internal bool Save(string seed, out CustomMapEntry savedEntry, out string error)
        {
            savedEntry = null;
            if (!LevelMapSeedUtility.TryDecodeRaw(seed, out LevelMapDefinition definition, out error)
                || !LevelMapValidator.Validate(definition, true, out error)
                || !TryLoad(out CustomMapCollection collection, out error))
                return false;

            string normalizedSeed = LevelMapSeedUtility.Encode(definition);
            CustomMapEntry entry = collection.maps.Find(map => map != null && map.seed == normalizedSeed);
            if (entry == null)
            {
                entry = new CustomMapEntry { label = $"Custom Map {collection.maps.Count + 1}", seed = normalizedSeed };
                collection.maps.Add(entry);
            }
            while (collection.maps.Count > MaxCustomMaps)
                collection.maps.RemoveAt(0);

            if (!TryWrite(collection, out error))
                return false;
            savedEntry = entry;
            return true;
        }

        internal bool TryLoad(out CustomMapCollection collection, out string error)
        {
            collection = null;
            error = string.Empty;
            try
            {
                // An existing file is authoritative, including when it is unreadable or
                // damaged. Never silently replace it with an empty or older collection.
                if (File.Exists(filePath))
                    return TryParse(File.ReadAllText(filePath), out collection, out error);

                string legacyJson = readLegacy?.Invoke();
                if (string.IsNullOrWhiteSpace(legacyJson))
                {
                    collection = new CustomMapCollection();
                    return true;
                }
                if (!TryParse(legacyJson, out collection, out error) || !TryWrite(collection, out error))
                    return false;

                clearLegacy?.Invoke();
                return true;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is System.Security.SecurityException)
            {
                error = $"Could not load custom maps: {exception.Message}";
                return false;
            }
        }

        private static bool TryParse(string json, out CustomMapCollection collection, out string error)
        {
            collection = null;
            error = string.Empty;
            try
            {
                if (!string.IsNullOrWhiteSpace(json))
                    collection = JsonUtility.FromJson<CustomMapCollection>(json);
                if (collection?.maps != null)
                    return true;
            }
            catch (ArgumentException)
            {
                // Keep the existing file intact; recovery is an explicit user action.
            }
            error = "The custom map file is damaged. Restore a backup before saving new maps.";
            return false;
        }

        private bool TryWrite(CustomMapCollection collection, out string error)
        {
            error = string.Empty;
            string temporaryPath = filePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                string directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);
                File.WriteAllText(temporaryPath, JsonUtility.ToJson(collection, true));
                if (File.Exists(filePath))
                    File.Replace(temporaryPath, filePath, null);
                else
                    File.Move(temporaryPath, filePath);
                return true;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is System.Security.SecurityException || exception is NotSupportedException)
            {
                error = $"Could not save custom maps: {exception.Message}";
                return false;
            }
            finally
            {
                try
                {
                    if (File.Exists(temporaryPath))
                        File.Delete(temporaryPath);
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
    }
}
