using UnityEngine;

public class T_Tower : MonoBehaviour
{
    public GameObject bulletPrefab;   
    public float shootInterval = 1f;  
    public bool right_direction;         
    public float bulletSpeed = 5f;   
    public BoxCollider2D box_Collider = null;
    public Base_GameManager gameManager = null;

    private float shootTimer = 0f;

    void Start()
    {
        if (box_Collider == null)
        {
            box_Collider = GetComponent<BoxCollider2D>();
            if (box_Collider == null)
                Debug.LogWarning("BoxCollider2D not assigned or found!");
        }
    }

    void Update()
    {
        shootTimer += Time.deltaTime;

        // Fire after every interval
        if (shootTimer >= shootInterval)
        {
            Shoot();
            shootTimer = 0f;
        }
    }

    private void Shoot()
    {
        if (bulletPrefab == null)
        {
            Debug.LogWarning("No bullet prefab assigned to T_Tower!");
            return;
        }

        GameObject bullet = Instantiate(bulletPrefab, transform.position, Quaternion.identity);

        Bullet planeScript = bullet.GetComponent<Bullet>();
        if (planeScript != null)
        {
            planeScript.SetDirection(right_direction?1:-1);
            planeScript.speed = bulletSpeed;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Plane"))
        {
            Destroy(transform.gameObject);
            Destroy(other.gameObject);
            gameManager.Destroy_Enemy();
        }
    }
}
