using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using TD.Level;

namespace TD.Tests.EditMode
{
    public class CustomMapStorageTests
    {
        private string testDirectory;
        private string filePath;
        private CustomMapStore store;

        [SetUp]
        public void SetUp()
        {
            testDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/CustomMapTests-" + Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(testDirectory);
            filePath = Path.Combine(testDirectory, "customMaps.json");
            store = new CustomMapStore(filePath);
        }

        [TearDown]
        public void TearDown()
        {
            string allowedRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp")) + Path.DirectorySeparatorChar;
            Assert.That(testDirectory.StartsWith(allowedRoot, StringComparison.OrdinalIgnoreCase), Is.True);
            if (Directory.Exists(testDirectory))
                Directory.Delete(testDirectory, true);
        }

        [Test]
        public void Save_ReplacesExistingFileAndReloadsBothMaps()
        {
            Assert.That(store.Save(CreateSeed(3), out _, out string firstError), Is.True, firstError);
            Assert.That(store.Save(CreateSeed(4), out _, out string secondError), Is.True, secondError);
            Assert.That(store.TryLoad(out CustomMapCollection loaded, out _), Is.True);
            Assert.That(loaded.maps.Count, Is.EqualTo(2));
            Assert.That(Directory.GetFiles(testDirectory, "*.tmp"), Is.Empty);
        }

        [Test, Platform("Win")]
        public void Save_WhenReplacementIsLocked_ReturnsFailureAndPreservesPreviousSave()
        {
            Assert.That(store.Save(CreateSeed(3), out _, out _), Is.True);
            string previous = File.ReadAllText(filePath);
            using (new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                Assert.That(store.Save(CreateSeed(4), out CustomMapEntry entry, out string error), Is.False);
                Assert.That(entry, Is.Null);
                Assert.That(error, Is.Not.Empty);
                Assert.That(File.ReadAllText(filePath), Is.EqualTo(previous));
            }
            Assert.That(store.TryLoad(out CustomMapCollection loaded, out _), Is.True);
            Assert.That(loaded.maps.Count, Is.EqualTo(1));
            Assert.That(Directory.GetFiles(testDirectory, "*.tmp"), Is.Empty);
        }

        [Test]
        public void Save_DamagedExistingFileIsNotOverwritten()
        {
            File.WriteAllText(filePath, "broken json");
            Assert.That(store.Save(CreateSeed(3), out _, out string error), Is.False);
            Assert.That(error, Is.Not.Empty);
            Assert.That(File.ReadAllText(filePath), Is.EqualTo("broken json"));
        }

        [Test]
        public void Load_MigratesLegacyDataBeforeRemovingLegacyKey()
        {
            string legacy = JsonUtility.ToJson(new CustomMapCollection
            {
                maps = new List<CustomMapEntry> { new CustomMapEntry { label = "Legacy", seed = CreateSeed(3) } }
            });
            bool cleared = false;
            store = new CustomMapStore(filePath, () => legacy, () =>
            {
                Assert.That(File.Exists(filePath), Is.True);
                cleared = true;
            });
            Assert.That(store.TryLoad(out CustomMapCollection loaded, out _), Is.True);
            Assert.That(cleared, Is.True);
            Assert.That(loaded.maps[0].label, Is.EqualTo("Legacy"));
        }

        [Test]
        public void Load_FailedMigrationKeepsLegacyData()
        {
            Directory.CreateDirectory(filePath);
            bool cleared = false;
            store = new CustomMapStore(filePath, () => JsonUtility.ToJson(new CustomMapCollection()), () => cleared = true);
            Assert.That(store.TryLoad(out _, out string error), Is.False);
            Assert.That(cleared, Is.False);
            Assert.That(error, Is.Not.Empty);
        }

        [Test]
        public void Load_ExistingFileTakesPrecedenceOverLegacyData()
        {
            Assert.That(store.Save(CreateSeed(3), out _, out _), Is.True);
            store = new CustomMapStore(filePath, () => throw new Exception("Legacy data must not be read."));
            Assert.That(store.TryLoad(out CustomMapCollection loaded, out _), Is.True);
            Assert.That(loaded.maps.Count, Is.EqualTo(1));
        }

        private static string CreateSeed(int width)
        {
            LevelMapAuthoringState state = new LevelMapAuthoringState();
            state.CreateNewMap(width, 3, true);
            return LevelMapSeedUtility.Encode(state.BuildDefinition());
        }
    }
}
