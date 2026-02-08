using UnityEngine;

public class EnemyView : MonoBehaviour
{
    [Header("References")]
    public Camera enemyCamera;
    public Transform player;

    [Header("Settings")]
    public float maxDistance = 20f;
    [Tooltip("Height offset to aim at player's chest instead of feet")]
    public float playerHeightOffset = 1.5f; 

    void Update()
    {

    }


}