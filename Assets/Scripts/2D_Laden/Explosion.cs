using UnityEngine;

public class Explosion : MonoBehaviour
{
    public float lifetime = 1.25f; 

    void Start()
    {
        Destroy(gameObject, lifetime);
    }
}