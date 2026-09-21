using TD.Core;

namespace TD.Towers
{
    public static class TowerUpgradeService
    {
        public static bool TryPurchase(Tower tower, GameState gameState)
        {
            if (tower == null || gameState == null || !gameState.IsPlaying || !GameplayLifecycle.CanRunCombat)
                return false;

            int cost = tower.GetNextUpgradeCost();
            if (cost < 0 || !gameState.TrySpendMoney(cost))
                return false;
            if (tower.TryUpgrade())
                return true;

            gameState.AddMoney(cost);
            return false;
        }
    }
}
