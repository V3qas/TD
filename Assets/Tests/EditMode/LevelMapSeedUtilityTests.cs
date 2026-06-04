using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using TD.Combat;
using TD.Level;

namespace TD.Tests.EditMode
{
    public class LevelMapSeedUtilityTests
    {
        [Test]
        public void Encode_ProducesPrefixedSeed()
        {
            LevelMapDefinition definition = BuildSimpleDefinition();

            string seed = LevelMapSeedUtility.Encode(definition);

            Assert.IsTrue(seed.StartsWith(LevelMapSeedUtility.SeedPrefix));
        }

        [Test]
        public void EncodeDecode_RoundTripPreservesContent()
        {
            LevelMapDefinition definition = BuildSimpleDefinition();
            string seed = LevelMapSeedUtility.Encode(definition);

            bool decoded = LevelMapSeedUtility.TryDecode(seed, out LevelMapDefinition restored, out string error);

            Assert.IsTrue(decoded, error);
            // Encode normalizes, so we compare against the normalized form of the input.
            LevelMapDefinition normalizedInput = definition.CloneNormalized();
            Assert.AreEqual(normalizedInput.width, restored.width);
            Assert.AreEqual(normalizedInput.height, restored.height);
            Assert.AreEqual(normalizedInput.startCell, restored.startCell);
            Assert.AreEqual(normalizedInput.goalCell, restored.goalCell);
            Assert.AreEqual(normalizedInput.blockedCells.Count, restored.blockedCells.Count, "blockedCells must always be empty in v3.");
            Assert.AreEqual(normalizedInput.pathCells.Count, restored.pathCells.Count);
            Assert.AreEqual(normalizedInput.occupants.Count, restored.occupants.Count, "Migrated rock occupant must round-trip.");
            Assert.AreEqual(normalizedInput.pathSequences.Count, restored.pathSequences.Count, "Path sequences must round-trip.");
        }

        [Test]
        public void TryDecode_ReturnsFalseForEmptyInput()
        {
            bool decoded = LevelMapSeedUtility.TryDecode("", out LevelMapDefinition definition, out string error);

            Assert.IsFalse(decoded);
            Assert.IsNull(definition);
            Assert.IsNotEmpty(error);
        }

        [Test]
        public void TryDecode_ReturnsFalseForInvalidPayload()
        {
            bool decoded = LevelMapSeedUtility.TryDecode("not-a-valid-seed!!!", out LevelMapDefinition definition, out string error);

            Assert.IsFalse(decoded);
            Assert.IsNull(definition);
            Assert.IsNotEmpty(error);
        }

        [Test]
        public void TryDecode_AcceptsPlainJson()
        {
            LevelMapDefinition definition = BuildSimpleDefinition();
            string json = LevelMapSeedUtility.ToJson(definition, false);

            bool decoded = LevelMapSeedUtility.TryDecode(json, out LevelMapDefinition restored, out string error);

            Assert.IsTrue(decoded, error);
            Assert.AreEqual(definition.width, restored.width);
        }

        [Test]
        public void TryDecodeRaw_PreservesOccupantsThatNormalizeWouldDrop()
        {
            // Build a malformed JSON: an occupant sits on a path cell. The normalizing path
            // (TryDecode) silently removes it; the raw path must keep it so the validator can
            // report the conflict to the caller.
            LevelMapDefinition definition = new LevelMapDefinition
            {
                width = 4,
                height = 1,
                startCell = new Vector2Int(0, 0),
                goalCell = new Vector2Int(3, 0),
                pathSequences = new List<PathSequence>
                {
                    new PathSequence(new List<Vector2Int>
                    {
                        new Vector2Int(0, 0), new Vector2Int(1, 0),
                        new Vector2Int(2, 0), new Vector2Int(3, 0)
                    })
                },
                occupants = new List<OccupantEntry>
                {
                    new OccupantEntry { cell = new Vector2Int(2, 0), type = OccupantType.Rock }
                }
            };
            string json = JsonUtility.ToJson(definition);

            bool rawDecoded = LevelMapSeedUtility.TryDecodeRaw(json, out LevelMapDefinition raw, out string rawError);
            bool normalizedDecoded = LevelMapSeedUtility.TryDecode(json, out LevelMapDefinition normalized, out string normalizedError);

            Assert.IsTrue(rawDecoded, rawError);
            Assert.IsTrue(normalizedDecoded, normalizedError);
            Assert.AreEqual(1, raw.occupants.Count, "Raw decode must preserve the conflicting occupant.");
            Assert.AreEqual(0, normalized.occupants.Count, "Normalize is expected to drop the path-occupant overlap.");

            bool valid = LevelMapValidator.Validate(raw, true, out string validationError);
            Assert.IsFalse(valid, "Validator must reject the raw definition with the path/occupant overlap.");
            Assert.IsNotEmpty(validationError);
        }

        private static LevelMapDefinition BuildSimpleDefinition()
        {
            return new LevelMapDefinition
            {
                width = 6,
                height = 4,
                startCell = new Vector2Int(0, 1),
                goalCell = new Vector2Int(5, 1),
                blockedCells = new List<Vector2Int> { new Vector2Int(2, 0) },
                pathCells = new List<Vector2Int>
                {
                    new Vector2Int(0, 1),
                    new Vector2Int(1, 1),
                    new Vector2Int(2, 1),
                    new Vector2Int(3, 1),
                    new Vector2Int(4, 1),
                    new Vector2Int(5, 1)
                }
            };
        }
    }
}
