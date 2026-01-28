using UnityEngine;

public class Seesaw : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        GetComponent<Rigidbody>().maxAngularVelocity = 1000f;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
