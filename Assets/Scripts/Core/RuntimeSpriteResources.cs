using UnityEngine;

namespace TD.Core
{
    /// <summary>
    /// Owns the single runtime-created white sprite used by simple tinted world visuals.
    /// Unity owns the built-in white texture; only the shared sprite requires cleanup.
    /// </summary>
    internal static class RuntimeSpriteResources
    {
        private static Sprite whiteSprite;

        public static Sprite WhiteSprite
        {
            get
            {
                if (whiteSprite == null)
                {
                    Texture2D texture = Texture2D.whiteTexture;
                    float pixelsPerUnit = Mathf.Max(texture.width, texture.height);
                    whiteSprite = Sprite.Create(
                        texture,
                        new Rect(0f, 0f, texture.width, texture.height),
                        new Vector2(0.5f, 0.5f),
                        pixelsPerUnit);
                    whiteSprite.name = "RuntimeWhiteSprite";
                    whiteSprite.hideFlags = HideFlags.HideAndDontSave;
                }

                return whiteSprite;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            if (whiteSprite == null)
                return;

            if (Application.isPlaying)
                Object.Destroy(whiteSprite);
            else
                Object.DestroyImmediate(whiteSprite);

            whiteSprite = null;
        }
    }
}
