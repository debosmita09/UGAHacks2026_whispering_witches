using UnityEngine;
using UnityEngine.AI;

public class Nav : MonoBehaviour
{
    public NavMeshAgent Agent;
    
    // Drag your "SpawnPoint" object here in the Inspector
    public Transform targetDestination; 

    void Start()
    {
        Agent = GetComponent<NavMeshAgent>();
    }

    void Update()
    {
        if (targetDestination != null)
        {
            Agent.destination = targetDestination.position;
        }
    }

}