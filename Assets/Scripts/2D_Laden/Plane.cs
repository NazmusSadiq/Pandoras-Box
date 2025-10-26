using UnityEngine;

public class Plane : MonoBehaviour
{
    public float speed = 10f; 
    private int direction = 1;
    public BoxCollider2D box_Collider = null;
    public GameObject explosionPrefab;

    // Set the direction from outside
    void Start()
    {
        // Ensure collider is assigned
        
        if (box_Collider == null)
        {
            box_Collider = GetComponent<BoxCollider2D>();
            if (box_Collider == null)
                Debug.LogWarning("BoxCollider2D not assigned or found!");
        }
    }
    public void SetDirection(int dir)
    {
        direction = dir;
        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) * direction;
        transform.localScale = scale;
    }

    void Update()
    {
        transform.Translate(Vector3.right * speed * direction * Time.deltaTime);
        if (Mathf.Abs(transform.position.x) >= 9f)
        {
            Destroy(transform.gameObject);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Ground") || other.CompareTag("Tower"))
        {
            Instantiate(explosionPrefab, transform.position, Quaternion.identity);
        }
        if (other.CompareTag("Ground"))
        {
            Destroy(transform.gameObject);
        }
    }

}
