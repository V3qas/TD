using System;
using System.Text;
using UnityEngine;

namespace TD.Level
{
    public static class LevelMapSeedUtility
    {
        public const string SeedPrefix = "TDMS1:";

        public static string Encode(LevelMapDefinition definition)
        {
            string json = ToJson(definition, false);
            return SeedPrefix + Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
        }

        public static string ToJson(LevelMapDefinition definition, bool prettyPrint = true)
        {
            if (definition == null)
                return string.Empty;

            LevelMapDefinition normalizedDefinition = definition.CloneNormalized();
            return JsonUtility.ToJson(normalizedDefinition, prettyPrint);
        }

        /// <summary>
        /// Decodes a seed or JSON payload and returns the map definition <b>without</b> running
        /// <see cref="LevelMapDefinition.Normalize"/>. Use this when the caller wants to run
        /// <see cref="LevelMapValidator.Validate"/> against the authored data before any
        /// silent migration / cleanup happens (e.g. occupants placed on path cells would be
        /// dropped by Normalize and never seen by the validator).
        /// </summary>
        public static bool TryDecodeRaw(string seedOrJson, out LevelMapDefinition definition, out string error)
        {
            definition = null;
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(seedOrJson))
            {
                error = "Seed or JSON is empty.";
                return false;
            }

            string payload = seedOrJson.Trim();
            string json;

            try
            {
                if (payload.StartsWith(SeedPrefix, StringComparison.Ordinal))
                {
                    string encodedPayload = payload.Substring(SeedPrefix.Length);
                    json = Encoding.UTF8.GetString(Convert.FromBase64String(encodedPayload));
                }
                else if (payload.StartsWith("{", StringComparison.Ordinal))
                {
                    json = payload;
                }
                else
                {
                    json = Encoding.UTF8.GetString(Convert.FromBase64String(payload));
                }

                definition = JsonUtility.FromJson<LevelMapDefinition>(json);
            }
            catch (Exception exception)
            {
                error = $"Seed could not be read: {exception.Message}";
                return false;
            }

            if (definition == null)
            {
                error = "Seed contains no map data.";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Decodes, validates, and then normalizes a map. Implicit open-grid paths are allowed.
        /// </summary>
        public static bool TryDecode(string seedOrJson, out LevelMapDefinition definition, out string error)
        {
            return TryDecodeValidated(seedOrJson, false, out definition, out error);
        }

        /// <summary>
        /// Decodes and validates the authored data before normalization can clamp coordinates or
        /// remove conflicting entries. Use <paramref name="requireExplicitPath"/> for imported
        /// editor/custom maps that must contain an authored route.
        /// </summary>
        public static bool TryDecodeValidated(
            string seedOrJson,
            bool requireExplicitPath,
            out LevelMapDefinition definition,
            out string error)
        {
            if (!TryDecodeRaw(seedOrJson, out definition, out error))
                return false;

            if (!LevelMapValidator.Validate(definition, requireExplicitPath, out error))
            {
                definition = null;
                return false;
            }

            definition.Normalize();
            return true;
        }
    }

}
