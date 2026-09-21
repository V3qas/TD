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
        /// Convenience wrapper around <see cref="TryDecodeRaw"/> that additionally normalizes
        /// the decoded definition. Suitable for runtime callers that just want a ready-to-use
        /// map and do not run the validator themselves.
        /// </summary>
        public static bool TryDecode(string seedOrJson, out LevelMapDefinition definition, out string error)
        {
            if (!TryDecodeRaw(seedOrJson, out definition, out error))
                return false;

            definition.Normalize();
            return true;
        }
    }

}
