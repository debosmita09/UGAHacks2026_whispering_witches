using UnityEngine;

public class TriggerCol : MonoBehaviour
{
    public bool Guarding = true;
    
    // Define the exact spots
    public Vector3 spotA = new Vector3(387.1f, 1.64f, 297.1f);
    public Vector3 spotB = new Vector3(392.4f, 1.64f, 354.6f);

    // CHANGED: Use OnTriggerEnter instead of OnCollisionEnter
    // CHANGED: Parameter is now (Collider other) instead of (Collision collision)
    private void OnTriggerEnter(Collider other)
    {
        // Check if the THING that entered is the Goblin
        if (other.CompareTag("Goblin") && Guarding)
        {
            // If we are near Spot A -> Go to B
            if (transform.position == spotB)
            {
                transform.position = spotA;
            }
            // If we are near Spot B -> Go to A
            else if (transform.position == spotA)
            {
                transform.position = spotB;
            }
        }
    }
}