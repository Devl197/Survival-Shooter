using UnityEngine;

[CreateAssetMenu(menuName = "AI/Enemy Path Update Settings")]
public class EnemyPathUpdateSettings : ScriptableObject
{
    [Tooltip("Enabled: request a destination periodically. Disabled: request every frame. Can be changed during Play Mode.")]
    public bool periodicUpdates;

    [Tooltip("Seconds of game time between destination requests. Minimum 0.01 seconds.")]
    public float updateInterval = 0.2f;

    public float Interval
    {
        get
        {
            return float.IsNaN(updateInterval) || float.IsInfinity(updateInterval)
                ? 0.2f : Mathf.Max(0.01f, updateInterval);
        }
    }

    void OnValidate()
    {
        updateInterval = Interval;
    }
}
