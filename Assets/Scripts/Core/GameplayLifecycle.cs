using TD.Bullets;
using TD.Combat;
using TD.Enemies;
using TD.Level;

namespace TD.Core
{
    public static class GameplayLifecycle
    {
        public static bool CanRunCombat => (GameState.Instance == null || GameState.Instance.IsPlaying)
            && (!GameSession.IsMapEditorSession || GameSession.IsEditorTestRun);

        public static void StopCombat()
        {
            Bullet.ReleaseAll();
            Destructible.ClearMarkedTargets();
        }

        public static void ReturnToEditor(EnemySpawner spawner, BuildManager builder, LevelLoader loader)
        {
            GameSession.EndTestRun();
            GameSession.BeginMapEditorMode();
            spawner?.StopSpawning(true);
            StopCombat();
            builder?.ClearSelectedTowerToBuild();
            builder?.ClearAllPlacedTowers();
            loader?.ClearLoadedLevel();
        }
    }
}
