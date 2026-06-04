using System.Collections.Generic;
using System;
using UnityEngine;

/// <summary>
/// Block that can be marked by the player and then attacked by towers in range.
/// Drops gold on death.
///
/// Click-to-mark: a 2D collider on this GameObject reacts to OnMouseDown and
/// flips the marked flag. Marked destructibles are added to a static registry
/// that towers consult during target selection.
/// </summary>
public class Destructible : MonoBehaviour, IDamageable
{
    private static readonly List<Destructible> markedTargets = new List<Destructible>();
    public static IReadOnlyList<Destructible> MarkedTargets => markedTargets;

    private static readonly List<Destructible> activeTargets = new List<Destructible>();
    public static IReadOnlyList<Destructible> ActiveTargets => activeTargets;

    private float maxHealth;
    private float currentHealth;
    private int reward;
    private bool isMarked;
    private SpriteRenderer spriteRenderer;
    private HealthBar healthBar;
    private GridManager gridManager;
    private Vector2Int cellPosition;
    private bool hasCellPosition;
    private Action<Vector2Int, Vector3> onDestroyed;

    public bool IsMarked => isMarked;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRegistries()
    {
        markedTargets.Clear();
        activeTargets.Clear();
    }

    public static void ClearMarkedTargets()
    {
        for (int i = markedTargets.Count - 1; i >= 0; i--)
        {
            Destructible marked = markedTargets[i];
            if (marked != null)
            {
                marked.isMarked = false;
                marked.UpdateMarkedVisual();
            }
        }

        markedTargets.Clear();
    }

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public bool IsDead => currentHealth <= 0f;
    public Vector3 WorldPosition => transform.position;

    public void Initialize(int initialMaxHp, int rewardOnDeath, SpriteRenderer renderer, GridManager owningGridManager = null, Vector2Int? cell = null, Action<Vector2Int, Vector3> destroyedCallback = null)
    {
        maxHealth = Mathf.Max(1, initialMaxHp);
        currentHealth = maxHealth;
        reward = Mathf.Max(0, rewardOnDeath);
        spriteRenderer = renderer;
        isMarked = false;
        gridManager = owningGridManager;
        hasCellPosition = cell.HasValue;
        if (cell.HasValue)
            cellPosition = cell.Value;
        onDestroyed = destroyedCallback;
        UpdateMarkedVisual();

        if (!activeTargets.Contains(this))
            activeTargets.Add(this);

        if (healthBar == null)
            healthBar = HealthBar.AttachTo(transform);
        healthBar.Bind(this);
    }

    public void TakeDamage(float damage)
    {
        if (IsDead) return;
        currentHealth -= damage;
        if (currentHealth <= 0f)
        {
            currentHealth = 0f;
            Die();
        }
    }

    private void Die()
    {
        if (reward > 0)
        {
            GameState gameState = GameState.GetOrCreate();
            gameState?.AddMoney(reward);
        }
        Unmark();
        if (hasCellPosition)
        {
            gridManager?.ClearBlockedCell(cellPosition);
            onDestroyed?.Invoke(cellPosition, transform.position);
        }
        Destroy(gameObject);
    }

    public void ToggleMarked()
    {
        if (IsDead) return;
        if (isMarked) Unmark();
        else Mark();
    }

    public void Mark()
    {
        if (isMarked || IsDead) return;
        ClearMarkedTargets();
        isMarked = true;
        markedTargets.Add(this);
        UpdateMarkedVisual();
    }

    public void Unmark()
    {
        if (!isMarked) return;
        isMarked = false;
        markedTargets.Remove(this);
        UpdateMarkedVisual();
    }

    private void OnDisable()
    {
        // Defensive: if disabled before Die() (e.g. scene unload), drop registry entry.
        if (isMarked)
        {
            markedTargets.Remove(this);
            isMarked = false;
        }
        activeTargets.Remove(this);
    }

    private void UpdateMarkedVisual()
    {
        if (spriteRenderer == null) return;
        spriteRenderer.color = isMarked
            ? new Color(0.95f, 0.55f, 0.2f)   // marked = orange tint
            : new Color(0.55f, 0.42f, 0.28f); // idle = brown
    }
}
