using System.Collections;
using System.Reflection;
using NUnit.Framework;
using TD.Core;
using TD.Enemies;
using UnityEngine;
using UnityEngine.TestTools;

namespace TD.Tests.PlayMode
{
    public class EnemyFrameAnimatorTests
    {
        [UnityTest]
        public IEnumerator FirstUpdateAfterEnable_IgnoresSpawnTeleport()
        {
            GameSession.EndTestRun();
            GameSession.EndMapEditorMode();

            Texture2D texture = new Texture2D(2, 1);
            Sprite firstFrame = Sprite.Create(
                texture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f),
                1f);
            Sprite secondFrame = Sprite.Create(
                texture,
                new Rect(1f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f),
                1f);

            GameObject enemyObject = new GameObject("EnemyFrameAnimatorTest");
            enemyObject.SetActive(false);
            SpriteRenderer renderer = enemyObject.AddComponent<SpriteRenderer>();
            enemyObject.AddComponent<Enemy>();
            EnemyFrameAnimator animator = enemyObject.AddComponent<EnemyFrameAnimator>();
            SetField(animator, "targetRenderer", renderer);
            SetField(animator, "movementFrames", new[] { firstFrame, secondFrame });
            SetField(animator, "framesPerWorldUnit", 1f);

            enemyObject.transform.position = Vector3.zero;
            enemyObject.SetActive(true);
            enemyObject.transform.position = Vector3.right;

            yield return null;

            bool keptInitialFrame = renderer.sprite == firstFrame;

            Object.Destroy(enemyObject);
            Object.Destroy(firstFrame);
            Object.Destroy(secondFrame);
            Object.Destroy(texture);
            yield return null;

            Assert.That(keptInitialFrame, Is.True,
                "The pool's post-enable spawn teleport must not advance the movement animation.");
        }

        private static void SetField<T>(EnemyFrameAnimator animator, string fieldName, T value)
        {
            FieldInfo field = typeof(EnemyFrameAnimator).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field '{fieldName}'.");
            field.SetValue(animator, value);
        }
    }
}
