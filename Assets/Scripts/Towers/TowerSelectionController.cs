using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class TowerSelectionController : MonoBehaviour
{
    [SerializeField] private Camera mainCamera;
    [SerializeField] private InGameHudController hudController;
    [SerializeField] private BuildManager buildManager;
    [SerializeField] private Color selectedRangeColor = new Color(1f, 1f, 1f, 0.9f);

    private RangeIndicator selectedTowerRangeIndicator;

    public void Configure(Camera cameraReference, InGameHudController hudReference, BuildManager buildManagerReference)
    {
        mainCamera = cameraReference;
        hudController = hudReference;
        buildManager = buildManagerReference;
    }

    private void Awake()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        if (hudController == null)
            hudController = FindAnyObjectByType<InGameHudController>();

        if (buildManager == null)
            buildManager = FindAnyObjectByType<BuildManager>();
    }

    private void Update()
    {
        if (buildManager != null && buildManager.IsPlacingTower)
        {
            HideRangeIndicator();
            return;
        }

        if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
            return;

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        TrySelectTowerAtMousePosition();
    }

    private void TrySelectTowerAtMousePosition()
    {
        if (mainCamera == null || hudController == null)
            return;

        Vector2 mouseScreenPosition = Mouse.current.position.ReadValue();
        Vector3 worldPosition = mainCamera.ScreenToWorldPoint(mouseScreenPosition);
        Vector2 point = new Vector2(worldPosition.x, worldPosition.y);

        Collider2D hit = Physics2D.OverlapPoint(point);
        Tower tower = hit != null ? hit.GetComponentInParent<Tower>() : null;

        if (tower != null)
        {
            hudController.ShowTower(tower);
            GetOrCreateRangeIndicator().Show(tower.transform.position, tower.Range, selectedRangeColor);
        }
        else
        {
            hudController.ClearContext();
            HideRangeIndicator();
        }
    }

    private RangeIndicator GetOrCreateRangeIndicator()
    {
        if (selectedTowerRangeIndicator != null)
            return selectedTowerRangeIndicator;

        GameObject rangeObject = new GameObject("SelectedTowerRangeIndicator");
        selectedTowerRangeIndicator = rangeObject.AddComponent<RangeIndicator>();
        return selectedTowerRangeIndicator;
    }

    private void HideRangeIndicator()
    {
        if (selectedTowerRangeIndicator != null)
            selectedTowerRangeIndicator.Hide();
    }

    public void DeselectTower()
    {
        HideRangeIndicator();
    }

    public void RefreshTowerRange(Tower tower)
    {
        if (tower == null)
        {
            HideRangeIndicator();
            return;
        }

        GetOrCreateRangeIndicator().Show(tower.transform.position, tower.Range, selectedRangeColor);
    }
}
