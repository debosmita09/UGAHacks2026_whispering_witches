using UnityEngine;

public class FireDamage : MonoBehaviour
{
    public GameObject Goblin;
    public Animator Anim;
    void Awake()
    {
        Anim = Goblin.GetComponent<Animator>(); 
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
          
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnTriggerEnter(Collider other)
    {
        // Check if the THING that entered is the Goblin
        if (other.CompareTag("Goblin"))
        {
            // If we are near Spot A -> Go to B
            Debug.Log("BANG!!!");
            Anim.SetBool("Hit",true);
            Invoke("reset",1.5f);
        }
    }

    public void reset()
    {
        Anim.SetBool("Hit",false);
    }
}
