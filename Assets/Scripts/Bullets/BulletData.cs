using UnityEngine;

[CreateAssetMenu(fileName = "BulletData", menuName = "TowerDefense/Bullet Data")]
public class BulletData : ScriptableObject
{
    [Header("Info")]
    public string bulletName;

    [Header("Bewegung")]
    [Tooltip("Fluggeschwindigkeit in Units/Sekunde")]
    public float travelSpeed = 8f;

    [Header("Schaden")]
    [Tooltip("Multiplikator auf den Turm-Grundschaden (1 = 100 %, 1.5 = 150 %)")]
    public float damageMultiplier = 1f;

    [Header("Spezialeffekte")]
    [Tooltip("Trifft alle Gegner im Radius um den Aufprallpunkt (0 = kein Splash)")]
    public float splashRadius = 0f;

    [Tooltip("Durchdringt Gegner und fliegt weiter bis zum Ende der Reichweite")]
    public bool isPiercing = false;

    [Header("Slow-Effekt")]
    [Tooltip("Geschwindigkeitsfaktor nach dem Treffer (1 = kein Slow, 0.5 = halbe Geschwindigkeit)")]
    [Range(0.1f, 1f)]
    public float slowFactor = 1f;

    [Tooltip("Dauer des Slow in Sekunden (0 = kein Slow)")]
    public float slowDuration = 0f;

    [Header("Visuals")]
    [Tooltip("Prefab des Projektils – muss eine Bullet-Komponente haben")]
    public GameObject bulletPrefab;

    [Tooltip("Optionaler Animator für die Einschlag-Animation")]
    public RuntimeAnimatorController hitAnimator;
}
