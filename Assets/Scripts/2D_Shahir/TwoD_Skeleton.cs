using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody2D))]
public class TwoD_Skeleton : TwoD_Enemy
{
    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private Rigidbody2D rb;

    [Tooltip("CapsuleCollider2D used as the ATTACK HITBOX (IsTrigger = true).")]
    [SerializeField] private CapsuleCollider2D attackTrigger;
    [SerializeField] private CapsuleCollider2D collider2D;

    [Header("Detection / Movement")]
    [SerializeField] private LayerMask playerLayer;
    [SerializeField] private float detectionRange = 8f;
    [SerializeField] private float runSpeed = 2.5f;
    [SerializeField] private float stopDistance = 0.6f;
    [SerializeField] private float eyeHeight = 0.4f;

    [Header("Attack Clip Timing")]
    [Tooltip("Total length (seconds) of the goblin attack clip.")]
    [SerializeField] private float attackClipSeconds = 0.67f;   // e.g., 40 frames @ 60 FPS ≈ 0.667 s
    [Tooltip("Total frames in the attack clip.")]
    [SerializeField] private int attackTotalFrames = 40;
    [Tooltip("Hitbox active start frame (inclusive).")]
    [SerializeField] private int activeStartFrame = 32;
    [Tooltip("Hitbox active end frame (inclusive).")]
    [SerializeField] private int activeEndFrame = 37;

    [Header("Attack Flow")]
    [Tooltip("Idle pause after each attack before chasing/attacking again.")]
    [SerializeField] private float attackRecovery = 0.25f;

    [Header("Hit Reaction")]
    [Tooltip("How long the goblin is stunned (no movement) after being hit.")]
    [SerializeField] private float hitStun = 0.25f;

    [Header("Health")]
    [SerializeField] private int health = 2;

    // --- Internal state ---
    private Transform player;
    private bool playerInRange;
    private bool collidingWithPlayer;

    private bool attacking;        // true while attack anim is playing
    private bool recovering;       // true during post-attack idle pause
    private bool stunned;          // true while in hit stun
    private bool attackRunning;    // guard for attack coroutine
    private bool isDead;

    private float baseScaleX = 1f;

    private Coroutine attackCo;
    private Coroutine hitCo;

    // Store and restore constraints so we can freeze X while stunned
    private RigidbodyConstraints2D originalConstraints;

    void Reset()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponentInChildren<Animator>();
        attackTrigger = GetComponentInChildren<CapsuleCollider2D>();
        collider2D = GetComponentInChildren<CapsuleCollider2D>();

        // auto-assign a trigger capsule on this object if found
        var caps = GetComponents<CapsuleCollider2D>();
        foreach (var c in caps) if (c.isTrigger) { attackTrigger = c; break; }
    }

    void Start()
    {
        if (!rb) rb = GetComponent<Rigidbody2D>();
        if (!animator) animator = GetComponentInChildren<Animator>();
        if (!attackTrigger) attackTrigger = GetComponentInChildren<CapsuleCollider2D>();
        if (!collider2D) collider2D = GetComponentInChildren<CapsuleCollider2D>();

        baseScaleX = Mathf.Abs(transform.localScale.x);

        // keep rotation frozen by default; remember original for restore
        if ((rb.constraints & RigidbodyConstraints2D.FreezeRotation) == 0)
            rb.constraints |= RigidbodyConstraints2D.FreezeRotation;
        originalConstraints = rb.constraints;

        if (attackTrigger)
        {
            attackTrigger.isTrigger = true;
            attackTrigger.enabled = false;
        }
        else
        {
            Debug.LogWarning("[Goblin] Assign a CapsuleCollider2D (trigger) as attackTrigger.", this);
        }
    }

    void Update()
    {
        if (isDead) return;

        // Vision first
        ScanForPlayer();

        // Hard-lock during attack, recovery, or hit stun
        if (attacking || recovering || stunned)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            animator.SetBool("isRunning", false);
            return;
        }

        // AI
        if (playerInRange && player != null)
        {
            float dx = player.position.x - transform.position.x;
            float distX = Mathf.Abs(dx);

            if (dx != 0f) SetFacing(Mathf.Sign(dx));

            if (collidingWithPlayer || distX <= stopDistance)
            {
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                animator.SetBool("isRunning", false);

                if (!attackRunning)
                    attackCo = StartCoroutine(AttackRoutine());
            }
            else
            {
                float dir = Mathf.Sign(dx);
                rb.linearVelocity = new Vector2(dir * runSpeed, rb.linearVelocity.y);
                animator.SetBool("isRunning", true);
            }
        }
        else
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            animator.SetBool("isRunning", false);
        }
    }

    // ---------- Vision ----------
    void ScanForPlayer()
    {
        Vector2 origin = (Vector2)transform.position + Vector2.up * eyeHeight;

        // Draw rays so you can see them (Game view Gizmos must be on)
        Debug.DrawRay(origin, Vector2.right * detectionRange, Color.red);
        Debug.DrawRay(origin, Vector2.left * detectionRange, Color.blue);

        RaycastHit2D hitR = Physics2D.Raycast(origin, Vector2.right, detectionRange, playerLayer);
        RaycastHit2D hitL = Physics2D.Raycast(origin, Vector2.left, detectionRange, playerLayer);

        if (hitR.collider)
        {
            playerInRange = true;
            player = hitR.collider.transform;
            return;
        }
        if (hitL.collider)
        {
            playerInRange = true;
            player = hitL.collider.transform;
            return;
        }

        playerInRange = false;
        player = null;
    }

    // ---------- Attack ----------
    IEnumerator AttackRoutine()
    {
        attackRunning = true;
        attacking = true;

        // Stop dead before starting the attack
        rb.linearVelocity = Vector2.zero;

        animator.ResetTrigger("Hit");
        animator.SetTrigger("Attack");

        // Enable capsule hitbox only during the specified active frames
        yield return HitboxWindowByFrames(activeStartFrame, activeEndFrame);

        // Wait remaining animation time (to the end of the clip)
        float endT = attackClipSeconds * Mathf.Clamp01((float)activeEndFrame / Mathf.Max(1, attackTotalFrames));
        if (attackClipSeconds > endT)
            yield return new WaitForSeconds(attackClipSeconds - endT);

        // Leave attack state
        attacking = false;

        // Recovery pause (idle; cannot move or start a new attack)
        recovering = true;
        rb.linearVelocity = Vector2.zero;
        animator.SetBool("isRunning", false);
        yield return new WaitForSeconds(attackRecovery);
        recovering = false;

        attackRunning = false;
        attackCo = null;
    }

    IEnumerator HitboxWindowByFrames(int startFrameInclusive, int endFrameInclusive)
    {
        if (!attackTrigger) yield break;

        int total = Mathf.Max(1, attackTotalFrames);
        startFrameInclusive = Mathf.Clamp(startFrameInclusive, 1, total);
        endFrameInclusive = Mathf.Clamp(endFrameInclusive, 1, total);
        if (endFrameInclusive < startFrameInclusive) yield break;

        float startT = attackClipSeconds * (startFrameInclusive / (float)total);
        float endT = attackClipSeconds * (endFrameInclusive / (float)total);
        float activeDur = Mathf.Max(0f, endT - startT);

        attackTrigger.enabled = false;
        if (startT > 0f) yield return new WaitForSeconds(startT);

        attackTrigger.enabled = true;
        yield return new WaitForSeconds(activeDur);

        attackTrigger.enabled = false;
    }

    // ---------- Collisions with player (to stop and attack) ----------
    void OnCollisionEnter2D(Collision2D collision)
    {
        if (IsPlayerLayer(collision.collider.gameObject.layer))
            collidingWithPlayer = true;
    }

    void OnCollisionExit2D(Collision2D collision)
    {
        if (IsPlayerLayer(collision.collider.gameObject.layer))
            collidingWithPlayer = false;
    }

    // ---------- Attack trigger: print layer name ----------
    void OnTriggerEnter2D(Collider2D other)
    {
        // Only care when our attackTrigger is currently active
        if (!attackTrigger || !attackTrigger.enabled) return;
        if (other == attackTrigger) return; // ignore self

        string layerName = LayerMask.LayerToName(other.gameObject.layer);
        Debug.Log($"[Goblin Attack] Hit object on layer: {layerName}", other.gameObject);

        var player = other.GetComponent<TwoD_Player>() ?? other.GetComponentInParent<TwoD_Player>();
        if (player != null)
        {
            player.Hit();
            return;
        }
    }

    // ---------- Taking damage (interrupt everything immediately) ----------
    public override void Hit()
    {
        if (isDead) return;

        // If lethal now or after decrement, die immediately
        if (health <= 0)
        {
            Die();
            if (gameManager != null)
            {
                gameManager.Destroy_Enemy();
            }
            return;
        }

        // Decrement HP
        health--;
        Debug.Log(health);

        // >>> Immediate hard stop & stun guard to avoid any attack/chase this frame <<<
        stunned = true;                               // set first to block Update() this frame
        if (attackCo != null) { StopCoroutine(attackCo); attackCo = null; }
        if (hitCo != null) { StopCoroutine(hitCo); hitCo = null; }

        attacking = false;
        recovering = false;
        attackRunning = false;

        if (attackTrigger) attackTrigger.enabled = false;

        // Zero motion and freeze X while stunned
        rb.linearVelocity = Vector2.zero;
        animator.SetBool("isRunning", false);
        animator.ResetTrigger("Attack");              // cancel any queued attack
        animator.ResetTrigger("Hit");                 // ensure clean re-trigger
        rb.constraints = originalConstraints | RigidbodyConstraints2D.FreezePositionX;

        // Play Hit immediately and start stun timer
        animator.SetTrigger("Hit");
        hitCo = StartCoroutine(HitStunRoutine());
    }

    IEnumerator HitStunRoutine()
    {
        // remain stunned for the configured duration
        yield return new WaitForSeconds(hitStun);

        // unfreeze X and clear stun
        rb.constraints = originalConstraints;
        stunned = false;
        hitCo = null;
    }

    void Die()
    {
        isDead = true;
        

        // stop everything
        stunned = true;
        if (attackCo != null) { StopCoroutine(attackCo); attackCo = null; }
        if (hitCo != null) { StopCoroutine(hitCo); hitCo = null; }
        attacking = false;
        recovering = false;
        attackRunning = false;
        if (attackTrigger) attackTrigger.enabled = false;

        rb.linearVelocity = Vector2.zero;
        animator.SetBool("isRunning", false);
        animator.ResetTrigger("Attack");
        animator.ResetTrigger("Hit");

        animator.Play("Skeleton_Death", 0, 0f);

        // Optional: disable further AI
        collider2D.enabled = false;
        enabled = false;
        
    }

    // ---------- Helpers ----------
    bool IsPlayerLayer(int layer) => (playerLayer.value & (1 << layer)) != 0;

    void SetFacing(float sign)
    {
        // sign: -1 = face left, +1 = face right
        int s = sign >= 0f ? 1 : -1;
        var ls = transform.localScale;
        // baseScaleX is set in Start
        transform.localScale = new Vector3(baseScaleX * s, ls.y, ls.z);
    }
}
