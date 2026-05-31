using UnityEngine;

[CreateAssetMenu(fileName = "EnemyData", menuName = "TowerDefense/Enemy Data")]
public class EnemyData : ScriptableObject
{
    [Header("Info")]
    public string enemyName;

    [Header("Stats")]
    [Tooltip("Gesamte Lebenspunkte")]
    public float maxHealth = 100f;

    [Tooltip("Bewegungsgeschwindigkeit in Units/Sekunde")]
    public float speed = 2f;

    [Tooltip("Schutzschild – wird vor den HP abgezogen. Kein Rüstungsbonus auf Schild.")]
    public float shield = 0f;

    [Tooltip("Flache Schadensreduzierung pro Treffer (gilt nach Shield-Berechnung)")]
    public float armor = 0f;

    [Header("Reward")]
    [Tooltip("Gold, das der Spieler beim Töten erhält")]
    public int reward = 10;
}
