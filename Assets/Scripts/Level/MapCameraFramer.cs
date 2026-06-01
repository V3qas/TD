using UnityEngine;

public struct MapCameraFrame
{
    public bool IsValid;
    public Rect CameraRect;
    public float OrthographicSize;
    public float Aspect;
    public Vector2 Center;
    public Vector2 MinCenter;
    public Vector2 MaxCenter;

    public bool CanPan => IsValid && (MaxCenter.x > MinCenter.x || MaxCenter.y > MinCenter.y);

    public Vector3 ClampPosition(Vector3 position)
    {
        if (!IsValid)
            return position;

        return new Vector3(
            Mathf.Clamp(position.x, MinCenter.x, MaxCenter.x),
            Mathf.Clamp(position.y, MinCenter.y, MaxCenter.y),
            position.z);
    }
}

public static class MapCameraFramer
{
    public static MapCameraFrame Frame(
        Camera targetCamera,
        LevelMapDefinition definition,
        float cellSize,
        float reservedRightUiWidth,
        Canvas targetCanvas,
        float paddingCells,
        float overzoomFactor = 1f)
    {
        if (targetCamera == null || definition == null || !targetCamera.orthographic)
            return default;

        float screenWidth = Mathf.Max(1f, Screen.width);
        float screenHeight = Mathf.Max(1f, Screen.height);
        float reservedRightPixels = Mathf.Clamp(reservedRightUiWidth * GetCanvasScaleFactor(targetCanvas), 0f, screenWidth - 1f);
        float viewportWidthPixels = Mathf.Max(1f, screenWidth - reservedRightPixels);
        float viewportWidth = viewportWidthPixels / screenWidth;

        targetCamera.rect = new Rect(0f, 0f, viewportWidth, 1f);

        float safeCellSize = Mathf.Max(0.01f, cellSize);
        float mapWidth = Mathf.Max(safeCellSize, definition.width * safeCellSize);
        float mapHeight = Mathf.Max(safeCellSize, definition.height * safeCellSize);
        float padding = Mathf.Max(0f, paddingCells) * safeCellSize;
        float aspect = viewportWidthPixels / screenHeight;
        float clampedOverzoomFactor = Mathf.Clamp(overzoomFactor, 1f, 1.25f);

        float fitOrthographicSize = Mathf.Max(
            (mapHeight + padding * 2f) * 0.5f,
            (mapWidth + padding * 2f) / (2f * aspect));
        float orthographicSize = fitOrthographicSize / clampedOverzoomFactor;

        Vector2 center = new Vector2(
            definition.width * safeCellSize * 0.5f,
            definition.height * safeCellSize * 0.5f);

        float halfVisibleHeight = orthographicSize;
        float halfVisibleWidth = orthographicSize * aspect;
        Vector2 boundsMin = center - new Vector2(mapWidth * 0.5f + padding, mapHeight * 0.5f + padding);
        Vector2 boundsMax = center + new Vector2(mapWidth * 0.5f + padding, mapHeight * 0.5f + padding);
        Vector2 minCenter = new Vector2(boundsMin.x + halfVisibleWidth, boundsMin.y + halfVisibleHeight);
        Vector2 maxCenter = new Vector2(boundsMax.x - halfVisibleWidth, boundsMax.y - halfVisibleHeight);

        if (minCenter.x > maxCenter.x)
            minCenter.x = maxCenter.x = center.x;

        if (minCenter.y > maxCenter.y)
            minCenter.y = maxCenter.y = center.y;

        MapCameraFrame frame = new MapCameraFrame
        {
            IsValid = true,
            CameraRect = targetCamera.rect,
            OrthographicSize = orthographicSize,
            Aspect = aspect,
            Center = center,
            MinCenter = minCenter,
            MaxCenter = maxCenter
        };

        targetCamera.orthographicSize = orthographicSize;
        targetCamera.transform.position = frame.ClampPosition(new Vector3(
            center.x,
            center.y,
            targetCamera.transform.position.z));

        return frame;
    }

    private static float GetCanvasScaleFactor(Canvas targetCanvas)
    {
        return targetCanvas != null ? Mathf.Max(0.01f, targetCanvas.scaleFactor) : 1f;
    }
}