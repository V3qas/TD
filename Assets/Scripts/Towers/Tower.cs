using System.Collections.Generic;
using UnityEngine;

public class Tower : MonoBehaviour
{
    private TowerData data;
    private TowerUpgradeData upgradeData;
    private int currentUpgradeLevel;
    private float attackTimer;
    private int totalInvested;
    private float terrainRangeBonus;

    public TowerData Data => data;
    public int CurrentUpgradeLevel => currentUpgradeLevel;
    public float Damage => EffectiveDamage;
    public float AttackSpeed => EffectiveAttackSpeed;
    public float Range => EffectiveRange;

    // ── Effektive Stats (Basis + kumulierte Upgrade-Boni) ────────────────────

    private float EffectiveDamage
    {
        get
        {
            float total = data.damage;
            int cap = Mathf.Min(currentUpgradeLevel, upgradeData != null && upgradeData.levels != null ? upgradeData.levels.Count : 0);
            for (int i = 0; i < cap; i++)
                if (upgradeData.levels[i] != null)
                    total += upgradeData.levels[i].damageBonus;
            return total;
        }
    }

    private float EffectiveAttackSpeed
    {
        get
        {
            float total = data.attackSpeed;
            int cap = Mathf.Min(currentUpgradeLevel, upgradeData != null && upgradeData.levels != null ? upgradeData.levels.Count : 0);
            for (int i = 0; i < cap; i++)
                if (upgradeData.levels[i] != null)
                    total += upgradeData.levels[i].attackSpeedBonus;
            return Mathf.Max(0.01f, total); // verhindert Division durch 0
        }
    }

    private float EffectiveRange
    {
        get
        {
            float total = data.range + terrainRangeBonus;
            int cap = Mathf.Min(currentUpgradeLevel, upgradeData != null && upgradeData.levels != null ? upgradeData.levels.Count : 0);
            for (int i = 0; i < cap; i++)
                if (upgradeData.levels[i] != null)
                    total += upgradeData.levels[i].rangeBonus;
            return total;
        }
    }

    /// <summary>
    /// Adds a flat range bonus from the terrain the tower stands on (e.g. +1
    /// when placed on Elevated ground). Set once after placement.
    /// </summary>
    public void SetTerrainRangeBonus(float bonus)
    {
        terrainRangeBonus = bonus;
    }

    /// <summary>
    /// Gibt den aktuell gültigen BulletData zurück.
    /// Iteriert die Upgrade-Stufen von oben nach unten und nimmt das erste Override.
    /// </summary>
    private BulletData EffectiveBulletData
    {
        get
        {
            if (upgradeData != null && upgradeData.levels != null)
            {
                int cap = Mathf.Min(currentUpgradeLevel, upgradeData.levels.Count);
                for (int i = cap - 1; i >= 0; i--)
                {
                    if (upgradeData.levels[i] != null && upgradeData.levels[i].overrideBulletData != null)
                        return upgradeData.levels[i].overrideBulletData;
                }
            }
            return data.bulletData;
        }
    }

    // ── Initialisierung ──────────────────────────────────────────────────────

    /// <summary>
    /// Initialisiert den Turm. Muss direkt nach Instantiate aufgerufen werden.
    /// </summary>
    /// <param name="towerData">Pflicht: Basis-Werte des Turms.</param>
    /// <param name="towerUpgradeData">Optional: Upgrade-Pfad. Null = keine Upgrades möglich.</param>
    public void Initialize(TowerData towerData, TowerUpgradeData towerUpgradeData = null)
    {
        data = towerData;
        upgradeData = towerUpgradeData;
        currentUpgradeLevel = 0;
        attackTimer = 0f;
        totalInvested = towerData != null ? towerData.cost : 0;
    }

    // ── Upgrades ─────────────────────────────────────────────────────────────

    public bool CanUpgrade()
    {
        return upgradeData != null
            && upgradeData.levels != null
            && currentUpgradeLevel < upgradeData.levels.Count
            && upgradeData.levels[currentUpgradeLevel] != null;
    }

    /// <summary>Gibt die Kosten der nächsten Upgrade-Stufe zurück, oder -1 wenn kein Upgrade möglich.</summary>
    public int GetNextUpgradeCost()
    {
        if (!CanUpgrade())
            return -1;
        return upgradeData.levels[currentUpgradeLevel].cost;
    }

    /// <summary>Führt das nächste Upgrade durch. Gibt false zurück, wenn kein Upgrade verfügbar.</summary>
    public bool TryUpgrade()
    {
        if (!CanUpgrade())
            return false;

        totalInvested += upgradeData.levels[currentUpgradeLevel].cost;
        currentUpgradeLevel++;
        return true;
    }

    /// <summary>Gibt den Verkaufswert zurück (50% des investierten Goldes).</summary>
    public int GetSellValue()
    {
        return Mathf.RoundToInt(totalInvested * 0.5f);
    }

    // ── Kampflogik ───────────────────────────────────────────────────────────

    private void Update()
    {
        if (data == null)
            return;

        attackTimer -= Time.deltaTime;

        if (attackTimer <= 0f)
        {
            IDamageable target = FindNearestTarget();
            if (target != null)
            {
                Shoot(target);
                attackTimer = 1f / EffectiveAttackSpeed;
            }
        }
    }

    /// <summary>
    /// Targeting priority:
    ///  1. Marked Destructibles in range — the player has explicitly told the
    ///     towers to break these, so they take precedence over enemies.
    ///  2. Nearest enemy in range.
    ///  3. Unmarked Destructibles in range — fallback so towers don't idle
    ///     when the only valid targets are blocking blocks.
    /// </summary>
    private IDamageable FindNearestTarget()
    {
        Vector3 position = transform.position;
        float rangeSqr = EffectiveRange * EffectiveRange;

        IReadOnlyList<Destructible> markedTargets = Destructible.MarkedTargets;
        Destructible nearestMarked = null;
        float nearestMarkedSqr = rangeSqr;
        for (int i = 0; i < markedTargets.Count; i++)
        {
            Destructible marked = markedTargets[i];
            if (marked == null || marked.IsDead) continue;
            float sqr = (position - marked.WorldPosition).sqrMagnitude;
            if (sqr <= nearestMarkedSqr)
            {
                nearestMarkedSqr = sqr;
                nearestMarked = marked;
            }
        }
        if (nearestMarked != null)
            return nearestMarked;

        IReadOnlyList<Enemy> enemies = Enemy.ActiveEnemies;
        Enemy nearest = null;
        float nearestSqrDist = rangeSqr;

        for (int i = 0; i < enemies.Count; i++)
        {
            Enemy enemy = enemies[i];
            if (enemy == null || enemy.IsDead)
                continue;

            float sqrDist = (position - enemy.transform.position).sqrMagnitude;
            if (sqrDist <= nearestSqrDist)
            {
                nearestSqrDist = sqrDist;
                nearest = enemy;
            }
        }

        if (nearest != null)
            return nearest;

        // Fallback: any Destructible in range, marked or not.
        Destructible nearestAny = null;
        float nearestAnySqr = rangeSqr;
        IReadOnlyList<Destructible> all = Destructible.ActiveTargets;
        for (int i = 0; i < all.Count; i++)
        {
            Destructible d = all[i];
            if (d == null || d.IsDead) continue;
            float sqr = (position - d.WorldPosition).sqrMagnitude;
            if (sqr <= nearestAnySqr)
            {
                nearestAnySqr = sqr;
                nearestAny = d;
            }
        }
        return nearestAny;
    }

    private void Shoot(IDamageable target)
    {
        BulletData bulletData = EffectiveBulletData;

        if (bulletData == null)
        {
            Debug.LogWarning($"Tower '{data.towerName}': No BulletData assigned.");
            return;
        }

        if (bulletData.bulletPrefab == null)
        {
            Debug.LogWarning($"Tower '{data.towerName}': BulletData '{bulletData.bulletName}' has no prefab.");
            return;
        }

        GameObject bulletObject = PrefabPool.Spawn(bulletData.bulletPrefab, transform.position, Quaternion.identity);
        Bullet bullet = bulletObject.GetComponent<Bullet>();

        if (bullet != null)
            bullet.Initialize(bulletData, EffectiveDamage, target);
        else
        {
            Debug.LogWarning($"Tower '{data.towerName}': Bullet prefab has no Bullet component.");
            PrefabPool.Release(bulletObject);
        }
    }
}
