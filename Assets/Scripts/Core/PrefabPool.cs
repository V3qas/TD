using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// Zentraler Pool fuer beliebige Prefab-Instanzen. Pro Prefab wird intern ein
/// eigener <see cref="ObjectPool{T}"/> verwaltet. Eine zurueckgegebene Instanz
/// muss am Prefab erkannt werden koennen, deshalb merken wir uns die Quelle
/// per <see cref="PooledObject"/>-Komponente.
/// </summary>
public static class PrefabPool
{
    private const int DefaultCapacity = 16;
    private const int MaxSize = 512;

    private static readonly Dictionary<GameObject, ObjectPool<GameObject>> pools = new();
    private static Transform parent;

    /// <summary>Holt eine Instanz aus dem Pool (oder erzeugt sie). Aktiviert sie und positioniert sie.</summary>
    public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null)
        {
            Debug.LogError("PrefabPool.Spawn: prefab ist null.");
            return null;
        }

        ObjectPool<GameObject> pool = GetOrCreatePool(prefab);
        GameObject instance = pool.Get();
        instance.transform.SetPositionAndRotation(position, rotation);
        return instance;
    }

    /// <summary>Gibt eine Instanz zurueck in ihren Pool. Faellt auf Destroy zurueck, wenn die Instanz nicht gepoolt war.</summary>
    public static void Release(GameObject instance)
    {
        if (instance == null)
            return;

        if (instance.TryGetComponent(out PooledObject pooled) && pooled.SourcePrefab != null
            && pools.TryGetValue(pooled.SourcePrefab, out ObjectPool<GameObject> pool))
        {
            pool.Release(instance);
            return;
        }

        // Fallback: nicht gepoolt -> hart zerstoeren.
        Object.Destroy(instance);
    }

    /// <summary>Loescht alle Pools (z.B. beim Szenenwechsel).</summary>
    public static void Clear()
    {
        foreach (KeyValuePair<GameObject, ObjectPool<GameObject>> kvp in pools)
            kvp.Value.Dispose();
        pools.Clear();
        parent = null;
    }

    private static ObjectPool<GameObject> GetOrCreatePool(GameObject prefab)
    {
        if (pools.TryGetValue(prefab, out ObjectPool<GameObject> existing))
            return existing;

        ObjectPool<GameObject> created = new ObjectPool<GameObject>(
            createFunc: () =>
            {
                GameObject instance = Object.Instantiate(prefab, GetParent());
                PooledObject marker = instance.GetComponent<PooledObject>();
                if (marker == null)
                    marker = instance.AddComponent<PooledObject>();
                marker.SourcePrefab = prefab;
                return instance;
            },
            actionOnGet: instance =>
            {
                instance.SetActive(true);
            },
            actionOnRelease: instance =>
            {
                instance.SetActive(false);
            },
            actionOnDestroy: instance =>
            {
                if (instance != null)
                    Object.Destroy(instance);
            },
            collectionCheck: false,
            defaultCapacity: DefaultCapacity,
            maxSize: MaxSize);

        pools[prefab] = created;
        return created;
    }

    private static Transform GetParent()
    {
        if (parent != null)
            return parent;

        GameObject host = new GameObject("[PrefabPool]");
        parent = host.transform;
        return parent;
    }
}

/// <summary>Marker-Komponente, die jede gepoolte Instanz mit ihrem Quell-Prefab verknuepft.</summary>
public class PooledObject : MonoBehaviour
{
    public GameObject SourcePrefab;
}
