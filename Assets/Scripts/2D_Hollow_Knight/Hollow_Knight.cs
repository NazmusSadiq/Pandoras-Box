using System.Collections;
using UnityEngine;

public class Hollow_Knight : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5.0f;
    public float jumpHeight = 10.0f;

    [Header("Physics / Colliders")]
    public bool onFloor = true;
    public bool dead = false;
    public BoxCollider2D footChecker = null;
    public CapsuleCollider2D capsuleCollider = null;

    [Header("Attack (Nail-like)")]
    public float attackRange = 1.0f;
    public float attackVerticalOffset = 0.4f;
    public AudioSource hitSound = null;

    [Header("References")]
    public Hollow_Knight_GameManager gameManager = null;

    [Header("Damage Cooldown")]
    [Tooltip("Seconds of invulnerability after being hit")]
    public float damageCooldown = 1.0f; 
    private bool canBeHit = true;       

    private Animator m_Animator;
    private Rigidbody2D rb;
    private float speed;
    private float originalColliderHeight;
    private Vector3 originalLocalScale;

    private bool attacking = false;
    private bool jumping = false;

    void Start()
    {
        m_Animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();

        if (capsuleCollider != null)
            originalColliderHeight = capsuleCollider.size.y;

        originalLocalScale = transform.localScale;
    }

    void Update()
    {
        HandleInput();

        m_Animator.SetBool("shooting", attacking);
        m_Animator.SetFloat("speed", speed);
        m_Animator.SetBool("jumping", jumping || !onFloor);
        dead = (gameManager != null) && gameManager.gameOver;

        Move();
    }

    private void HandleInput()
    {
        if ((Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow)) && canJump())
        {
            Jump();
        }

        if ((Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.LeftControl)) && canAttack())
        {
            Attack();
        }
    }

    public bool canMove() =>  !dead;
    public bool canAttack() => !attacking && !dead;
    public bool canJump() => onFloor && !dead;

    public void Jump()
    {
        jumping = true;
        onFloor = false;

        if (rb != null)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpHeight);
        }
    }

    public void Attack()
    {
        attacking = true;
        m_Animator.SetTrigger("Attack");

        StartCoroutine(RaycastForDuration(0.5f));
        StartCoroutine(EndAttackAfterDelay(0.4f));
    }

    private IEnumerator RaycastForDuration(float duration)
    {
        float timeElapsed = 0f;

        while (timeElapsed < duration)
        {
            float dirX = Mathf.Sign(transform.localScale.x);
            Vector2 origin = (Vector2)transform.position + Vector2.up * attackVerticalOffset + new Vector2(0.1f * dirX, 0f);
            Vector2 direction = new Vector2(dirX, 0f);

            // Cast all hits and ignore self
            RaycastHit2D[] hits = Physics2D.RaycastAll(origin, direction, attackRange);
            Debug.DrawRay(origin, direction * attackRange, Color.red, 0.1f);

            foreach (var hit in hits)
            {
                if (hit.collider == null) continue;

                // ignore yourself (or child colliders)
                if (hit.collider.gameObject == this.gameObject) continue;
                if (hit.collider.transform.IsChildOf(this.transform)) continue;

                if (hit.collider.CompareTag("Enemy"))
                {
                    Destroy(hit.collider.gameObject);
                    if (gameManager != null) gameManager.Destroy_Enemy();
                    break; // stop after first enemy hit
                }
            }

            timeElapsed += Time.deltaTime;
            yield return null; // Wait until next frame
        }
    }

    private IEnumerator EndAttackAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        attacking = false;
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
        if (!canBeHit || dead)
            return;

        if (other.CompareTag("Enemy"))
        {
            if (hitSound != null)
                hitSound.Play();

            if (gameManager != null)
                gameManager.Reduce_Lives();

            StartCoroutine(DamageCooldownRoutine());
        }
    }

    private IEnumerator DamageCooldownRoutine()
    {
        canBeHit = false;
        Debug.Log("[Hollow_Knight] Player is invulnerable for " + damageCooldown + "s");
        yield return new WaitForSeconds(damageCooldown);
        canBeHit = true;
        Debug.Log("[Hollow_Knight] Player can be hit again");
    }
}
