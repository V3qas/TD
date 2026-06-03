using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

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
        Assert.AreEqual(definition.width, restored.width);
        Assert.AreEqual(definition.height, restored.height);
        Assert.AreEqual(definition.startCell, restored.startCell);
        Assert.AreEqual(definition.goalCell, restored.goalCell);
        Assert.AreEqual(definition.blockedCells.Count, restored.blockedCells.Count);
        Assert.AreEqual(definition.pathCells.Count, restored.pathCells.Count);
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
