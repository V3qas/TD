using UnityEngine;

[CreateAssetMenu(fileName = "TowerData", menuName = "TowerDefense/Tower Data")]
public class TowerData : ScriptableObject
{
    [Header("Info")]
    public string towerName;

    [Tooltip("Icon fuer die spaetere Anzeige im Ingame-Menue")]
    public Sprite icon;

    [Tooltip("Kosten beim Platzieren dieses Turms")]
    public int cost = 50;

    [Header("Kampf")]
    [Tooltip("Grundschaden pro Schuss")]
    public float damage = 20f;

    [Tooltip("Angriffe pro Sekunde")]
    public float attackSpeed = 1f;

    [Tooltip("Reichweite in Units")]
    public float range = 3f;

    [Header("Projektil")]
    [Tooltip("Bullet-Typ, den dieser Turm standardmäßig verschießt")]
    public BulletData bulletData;

    [Header("Prefabs")]
    [Tooltip("Prefab des Turms – muss eine Tower-Komponente haben")]
    public GameObject towerPrefab;
}
