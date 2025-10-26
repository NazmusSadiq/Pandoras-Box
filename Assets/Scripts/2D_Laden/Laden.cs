using System.Collections;
using UnityEngine;

public class Laden : MonoBehaviour
{
    public float moveSpeed = 5.0f;
    public float jumpHeight = 10.0f;
    public float colliderHeight = 1.18f;
    public bool onFloor = true;
    public bool dead = false;
    public BoxCollider2D footChecker = null;
    public CapsuleCollider2D capsuleCollider = null;
    public GameObject planePrefab;
    public Laden_GameManager gameManager = null;
    public float planeSpeed = 10f;
    public AudioSource hitSound = null;
    private Animator m_Animator;
    private Rigidbody2D rb;
    private float speed;
    private float originalColliderHeight;
    bool shooting, jumping, crawling = false;

    void Start()
    {
        m_Animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();

        // Store original collider height
        if (capsuleCollider != null)
            originalColliderHeight = capsuleCollider.size.y;
    }

    void Update()
    {
        HandleInput();

        m_Animator.SetBool("shooting", shooting);
        m_Animator.SetFloat("speed", speed);
        m_Animator.SetBool("jumping", jumping);
        m_Animator.SetBool("crawling", crawling);
        dead = gameManager.gameOver;
        Move();

        UpdateColliderHeight();
    }

    private void HandleInput()
    {
        // Jump → W or Up Arrow
        if ((Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow)) && canJumpOrCrawl())
        {
            Jump();
        }

        // Crawl → S or Down Arrow
        else if ((Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow)) && canJumpOrCrawl())
        {
            Crawl();
        }

        // Stop crawling when key is released
        if (Input.GetKeyUp(KeyCode.S) || Input.GetKeyUp(KeyCode.DownArrow))
        {
            crawling = false;
        }

        // Shoot → Left Mouse or Left Ctrl
        else if ((Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.LeftControl)) && canShoot())
        {
            Shoot();
        }

        // Stop shooting
        if (Input.GetMouseButtonUp(0) || Input.GetKeyUp(KeyCode.LeftControl))
        {
            shooting = false;
        }
    }

    public bool canMove() => !shooting && !crawling && !dead;
    public bool canShoot() => !(jumping || shooting || crawling || dead) && onFloor;
    public bool canJumpOrCrawl() => onFloor && !shooting && !dead;

    public void Jump()
    {
        jumping = true;
        crawling = false;
        onFloor = false;

        if (rb != null)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpHeight);
        }
    }

    public void Shoot()
    {
        if (shooting || dead) return;

        shooting = true;
        StartCoroutine(ShootWithDelay(0.25f));
    }

    private IEnumerator ShootWithDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (planePrefab != null)
        {
            GameObject newPlane = Instantiate(planePrefab, transform.position, Quaternion.identity);

            int dir = transform.localScale.x > 0 ? 1 : -1;

            Plane planeScript = newPlane.GetComponent<Plane>();
            if (planeScript != null)
            {
                planeScript.SetDirection(dir);
                planeScript.speed = planeSpeed;
            }
        }
        Invoke(nameof(StopShooting), 0.75f);
    }


    private void StopShooting()
    {
        shooting = false;
    }

    public void Crawl()
    {
        jumping = false;
        crawling = true;
    }

    public void Move()
    {
        if (canMove())
        {
            float move = Input.GetAxis("Horizontal");
            Vector3 velocity = Vector3.right * moveSpeed * move;
            transform.Translate(velocity * Time.deltaTime);

            if (move > 0)
                transform.localScale = new Vector3(1, 1, 1);
            else if (move < 0)
                transform.localScale = new Vector3(-1, 1, 1);

            speed = Mathf.Abs(move);
        }
    }

    private void UpdateColliderHeight()
    {
        if (capsuleCollider == null)
            return;

        Vector2 size = capsuleCollider.size;

        if (crawling)
        {
            size.y = colliderHeight / 2f;
            capsuleCollider.offset = new Vector2(capsuleCollider.offset.x, (colliderHeight / 20f));
        }
        else
        {
            size.y = originalColliderHeight;
            capsuleCollider.offset = new Vector2(capsuleCollider.offset.x, 0f); 
        }

        capsuleCollider.size = size;
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (footChecker != null && other.CompareTag("Ground"))
        {
            onFloor = true;
            jumping = false;
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (footChecker != null && other.CompareTag("Ground"))
        {
            onFloor = false;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Bullet"))
        {
            if (hitSound != null)
            {
                hitSound.Play();
            }
            gameManager.Reduce_Lives();
            Destroy(other.gameObject);
        }
    }
}
