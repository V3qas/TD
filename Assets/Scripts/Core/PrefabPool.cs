using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.SceneManagement;

public static class PrefabPool
{
    private const int DefaultCapacity = 16;
    private const int MaxSize = 512;

    private static readonly Dictionary<GameObject, ObjectPool<GameObject>> pools = new();
    private static Transform parent;

    static PrefabPool()
    {
        SceneManager.sceneUnloaded += HandleSceneUnloaded;
    }

    public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null)
        {
            Debug.LogError("PrefabPool.Spawn: Prefab is null.");
            return null;
        }

        ObjectPool<GameObject> pool = GetOrCreatePool(prefab);
        GameObject instance = pool.Get();
        instance.transform.SetPositionAndRotation(position, rotation);
        return instance;
    }

    /// <summary>Returns an instance to its pool. Falls back to Destroy when the instance was not pooled.</summary>
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

        Object.Destroy(instance);
    }

    public static void Clear()
    {
        foreach (KeyValuePair<GameObject, ObjectPool<GameObject>> kvp in pools)
            kvp.Value.Dispose();
        pools.Clear();
        parent = null;
    }

    private static void HandleSceneUnloaded(Scene scene)
    {
        Clear();
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

public class PooledObject : MonoBehaviour
{
    public GameObject SourcePrefab;
}
