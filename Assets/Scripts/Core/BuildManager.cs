using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using TD.Grid;
using TD.Level;
using TD.Towers;

namespace TD.Core
{
    public class BuildManager : MonoBehaviour
    {
        [SerializeField] private Camera mainCamera;
        [SerializeField] private GridManager gridManager;
        [SerializeField] private GameState gameState;

        [Header("Build")]
        [SerializeField] private TowerData towerToBuild;

        [Tooltip("Towers shown as build options in the in-game menu.")]
        [SerializeField] private List<TowerData> availableTowers = new List<TowerData>();

        [Tooltip("Fallback upgrade path used when a TowerData asset has no dedicated upgrade path.")]
        [SerializeField] private TowerUpgradeData towerUpgradeData;

        [Header("Ghost Preview")]
        [SerializeField] private Color validGhostColor = new Color(0.35f, 0.85f, 1f, 0.45f);
        [SerializeField] private Color invalidGhostColor = new Color(1f, 0.25f, 0.2f, 0.45f);
        [SerializeField] private Color validRangeColor = new Color(0.35f, 0.85f, 1f, 0.9f);
        [SerializeField] private Color invalidRangeColor = new Color(1f, 0.25f, 0.2f, 0.9f);

        public event Action<TowerData> OnBuildSelectionChanged;

        private readonly List<TowerData> availableTowerCache = new List<TowerData>();
        private GameObject ghostObject;
        private TowerData ghostTowerData;
        private RangeIndicator ghostRangeIndicator;
        private SpriteRenderer[] ghostRenderers = Array.Empty<SpriteRenderer>();

        public bool IsPlacingTower => towerToBuild != null;
        public TowerData SelectedTowerToBuild => towerToBuild;

        public IReadOnlyList<TowerData> AvailableTowers
        {
            get
            {
                availableTowerCache.Clear();

                foreach (TowerData availableTower in availableTowers)
                {
                    if (availableTower != null && !availableTowerCache.Contains(availableTower))
                        availableTowerCache.Add(availableTower);
                }

                if (availableTowerCache.Count == 0 && towerToBuild != null)
                    availableTowerCache.Add(towerToBuild);

                return availableTowerCache;
            }
        }

        private void Awake()
        {
            if (mainCamera == null)
                mainCamera = Camera.main;

            if (gameState == null)
                gameState = GameState.GetOrCreate();

            if (availableTowers.Count == 0 && towerToBuild != null)
                availableTowers.Add(towerToBuild);

            ClearSelectedTowerToBuild();
        }

        private void Update()
        {
            if (!GameplayLifecycle.CanRunCombat)
            {
                if (IsPlacingTower)
                    ClearSelectedTowerToBuild();

                return;
            }

            if (Mouse.current == null)
                return;

            if (IsPlacingTower)
                UpdateGhostPreview();

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                ClearSelectedTowerToBuild();
                return;
            }

            if (Mouse.current.rightButton.wasPressedThisFrame)
            {
                ClearSelectedTowerToBuild();
                return;
            }

            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                if (IsPointerOverUi())
                    return;

                if (!IsPlacingTower)
                    return;

                TryBuildAtMousePosition();
            }
        }

        private bool IsPointerOverUi()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }

        public void SelectTowerToBuild(TowerData towerData)
        {
            if (towerData == null || (gameState != null && !gameState.IsPlaying))
                return;

            if (!availableTowers.Contains(towerData))
                availableTowers.Add(towerData);

            towerToBuild = towerData;
            CreateGhostPreview();
            OnBuildSelectionChanged?.Invoke(towerToBuild);
        }

        public void ClearSelectedTowerToBuild()
        {
            towerToBuild = null;
            DestroyGhostPreview();

            if (ghostRangeIndicator != null)
                ghostRangeIndicator.Hide();

            OnBuildSelectionChanged?.Invoke(null);
        }

        public bool CanAfford(TowerData towerData)
        {
            if (towerData == null || (gameState != null && !gameState.IsPlaying))
                return false;

            return gameState == null || gameState.Money >= towerData.cost;
        }

        private void TryBuildAtMousePosition()
        {
            if (mainCamera == null)
            {
                Debug.LogError("BuildManager: Main Camera is missing.");
                return;
            }

            if (gridManager == null)
            {
                Debug.LogError("BuildManager: GridManager is missing.");
                return;
            }

            Vector2 mouseScreenPosition = Mouse.current.position.ReadValue();
            Vector3 worldPosition = mainCamera.ScreenToWorldPoint(mouseScreenPosition);
            worldPosition.z = 0f;

            Vector2Int cellPosition = gridManager.WorldToCell(worldPosition);
            TryBuildAtCell(cellPosition);
        }

        public bool TryBuildAtCell(Vector2Int cellPosition)
        {
            if (!GameplayLifecycle.CanRunCombat || gridManager == null || gameState == null || !gameState.IsPlaying
                || towerToBuild == null || towerToBuild.towerPrefab == null)
                return false;

            if (!gridManager.CanBuildAt(cellPosition))
                return false;

            if (gameState != null && !gameState.TrySpendMoney(towerToBuild.cost))
            {
                Debug.Log("BuildManager: Not enough money for this tower.");
                return false;
            }

            if (!gridManager.TryOccupyCell(cellPosition))
            {
                gameState?.AddMoney(towerToBuild.cost);
                return false;
            }

            if (!PlaceTower(cellPosition))
            {
                gridManager.ClearOccupiedCell(cellPosition);
                gameState?.AddMoney(towerToBuild.cost);
                return false;
            }
            return true;
        }

        private bool PlaceTower(Vector2Int cellPosition)
        {
            if (towerToBuild.towerPrefab == null)
            {
                Debug.LogError($"BuildManager: TowerData '{towerToBuild.towerName}' has no prefab.");
                return false;
            }

            Vector3 worldPos = gridManager.CellToWorld(cellPosition);
            GameObject towerObject = Instantiate(towerToBuild.towerPrefab, worldPos, Quaternion.identity, transform);
            EnsureTowerCanBeSelected(towerObject);

            Tower tower = towerObject.GetComponent<Tower>();
            if (tower != null)
            {
                TowerUpgradeData selectedUpgradeData = towerToBuild.upgradeData != null
                    ? towerToBuild.upgradeData
                    : towerUpgradeData;
                tower.Initialize(towerToBuild, selectedUpgradeData);
                if (gridManager.GetGroundType(cellPosition) == GroundType.Elevated)
                    tower.SetTerrainRangeBonus(1f);
                return true;
            }
            else
            {
                Debug.LogWarning($"BuildManager: Tower prefab '{towerToBuild.towerPrefab.name}' has no Tower component.");
                towerObject.SetActive(false);
                Destroy(towerObject);
                return false;
            }
        }

        private void UpdateGhostPreview()
        {
            if (towerToBuild == null || mainCamera == null || gridManager == null)
                return;

            if (ghostObject == null || ghostTowerData != towerToBuild)
                CreateGhostPreview();

            Vector2Int cellPosition = GetMouseCellPosition();
            Vector3 worldPosition = gridManager.CellToWorld(cellPosition);
            bool canPlace = gridManager.CanBuildAt(cellPosition) && CanAfford(towerToBuild);

            // Mirror the runtime range bonus that BuildAtCell applies after
            // placement, so the ghost preview and range indicator already show
            // the boosted range while the player hovers Elevated terrain.
            float terrainRangeBonus = gridManager.GetGroundType(cellPosition) == GroundType.Elevated ? 1f : 0f;
            float ghostRange = towerToBuild.GetTargetingRange(terrainRangeBonus);

            if (ghostObject != null)
            {
                ghostObject.transform.position = worldPosition;
                ApplyGhostColor(canPlace ? validGhostColor : invalidGhostColor);
            }

            GetOrCreateGhostRangeIndicator().Show(worldPosition, ghostRange, canPlace ? validRangeColor : invalidRangeColor);
        }

        private Vector2Int GetMouseCellPosition()
        {
            Vector2 mouseScreenPosition = Mouse.current.position.ReadValue();
            Vector3 worldPosition = mainCamera.ScreenToWorldPoint(mouseScreenPosition);
            worldPosition.z = 0f;
            return gridManager.WorldToCell(worldPosition);
        }

        private void CreateGhostPreview()
        {
            DestroyGhostPreview();

            if (towerToBuild == null || towerToBuild.towerPrefab == null)
                return;

            ghostObject = Instantiate(towerToBuild.towerPrefab, transform);
            ghostObject.name = $"Ghost_{towerToBuild.towerName}";
            ghostTowerData = towerToBuild;

            foreach (MonoBehaviour behaviour in ghostObject.GetComponentsInChildren<MonoBehaviour>())
                behaviour.enabled = false;

            foreach (Collider2D collider in ghostObject.GetComponentsInChildren<Collider2D>())
                collider.enabled = false;

            foreach (Rigidbody2D body in ghostObject.GetComponentsInChildren<Rigidbody2D>())
                body.simulated = false;

            ghostRenderers = ghostObject.GetComponentsInChildren<SpriteRenderer>();
            ApplyGhostColor(validGhostColor);
        }

        private void DestroyGhostPreview()
        {
            if (ghostObject != null)
                Destroy(ghostObject);

            ghostObject = null;
            ghostTowerData = null;
            ghostRenderers = Array.Empty<SpriteRenderer>();
        }

        private void ApplyGhostColor(Color color)
        {
            if (ghostObject == null)
                return;

            foreach (SpriteRenderer spriteRenderer in ghostRenderers)
                spriteRenderer.color = color;
        }

        private RangeIndicator GetOrCreateGhostRangeIndicator()
        {
            if (ghostRangeIndicator != null)
                return ghostRangeIndicator;

            GameObject rangeObject = new GameObject("BuildGhostRangeIndicator");
            rangeObject.transform.SetParent(transform, false);
            ghostRangeIndicator = rangeObject.AddComponent<RangeIndicator>();
            return ghostRangeIndicator;
        }

        private void EnsureTowerCanBeSelected(GameObject towerObject)
        {
            if (towerObject.GetComponentInChildren<Collider2D>() != null)
                return;

            towerObject.AddComponent<BoxCollider2D>();
        }

        public void SellTower(Tower tower)
        {
            if (tower == null || gridManager == null || (gameState != null && !gameState.IsPlaying))
                return;

            int sellValue = tower.GetSellValue();
            Vector2Int cell = gridManager.WorldToCell(tower.transform.position);
            gridManager.ClearOccupiedCell(cell);
            gameState?.AddMoney(sellValue);
            Destroy(tower.gameObject);
        }

        public void ClearAllPlacedTowers()
        {
            Tower[] towers = FindObjectsByType<Tower>(FindObjectsInactive.Exclude);
            foreach (Tower tower in towers)
            {
                if (tower != null)
                {
                    Vector2Int cell = gridManager != null ? gridManager.WorldToCell(tower.transform.position) : default;
                    if (gridManager != null)
                        gridManager.ClearOccupiedCell(cell);

                    tower.gameObject.SetActive(false);
                    Destroy(tower.gameObject);
                }
            }
        }
    }
}
