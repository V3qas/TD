using UnityEngine;

namespace TD.Menu
{
    internal static class DisplaySettings
    {
        // Unity persists Screen.SetResolution changes and restores them before
        // user code runs. Keep Screen as the only source of truth: duplicating
        // these values in custom PlayerPrefs causes stale startup overrides.
        internal static bool IsFullscreenMode(FullScreenMode mode)
        {
            return mode != FullScreenMode.Windowed;
        }

        internal static FullScreenMode GetMode(bool fullscreen)
        {
            return fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
        }

        internal static void Apply(Vector2Int resolution, bool fullscreen)
        {
            if (!Application.isPlaying || resolution.x <= 0 || resolution.y <= 0)
                return;

#if !UNITY_WEBGL
            Screen.SetResolution(resolution.x, resolution.y, GetMode(fullscreen));
#endif
        }
    }
}
