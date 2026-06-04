using System;
using UnityEngine;

/// <summary>
/// ScriptableObject describing the visual theme of a map (background, ground
/// tile sprites/colors per <see cref="GroundType"/>). The actual gameplay rules
/// are unchanged - themes only affect rendering.
///
/// A null sprite means the renderer falls back to a tinted 1x1 quad.
/// </summary>
[CreateAssetMenu(menuName = "TD/Map Theme", fileName = "MapTheme")]
public class MapThemeDefinition : ScriptableObject
{
    [Serializable]
    public struct GroundVisual
    {
        public GroundType type;
        public Sprite sprite;
        public Color tint;
    }

    [Header("Identity")]
    public string themeId = "default";
    public string displayName = "Standard";

    [Header("Background")]
    public Sprite backgroundSprite;
    public Color backgroundColor = new Color(0.06f, 0.07f, 0.1f, 1f);

    [Header("Ground Visuals")]
    public GroundVisual[] groundVisuals = Array.Empty<GroundVisual>();

    public bool TryGetGroundVisual(GroundType type, out GroundVisual visual)
    {
        if (groundVisuals != null)
        {
            for (int i = 0; i < groundVisuals.Length; i++)
            {
                if (groundVisuals[i].type == type)
                {
                    visual = groundVisuals[i];
                    return true;
                }
            }
        }
        visual = default;
        return false;
    }
}
