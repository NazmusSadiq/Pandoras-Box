using UnityEngine;
using System.Collections;

public class TwoD_Player : MonoBehaviour
{
    [Header("Locomotion")]
    public float walkSpeed = 2f;
    public float runSpeed = 5f;

    [Header("Jump / Ground")]
    public Transform groundCheck;
    public float groundCheckRadius = 0.25f;
    public float jumpForce = 8f;
    public LayerMask groundLayer;

    [Header("Attack / Combo (20-frame clips)")]
    public KeyCode attackKey = KeyCode.J;
    [Tooltip("Duration (seconds) of a 20-frame attack clip (e.g., 20 frames @ 60 FPS ≈ 0.333s)")]
    public float attackTotal = 0.33f;

    [Tooltip("Open/close window for chaining to next attack within each clip")]
    public float comboOpen = 0.10f;
    public float comboClose = 0.25f;

    [Header("Attack Collider (Trigger)")]
    [Tooltip("Assign the player's BoxCollider2D used as the attack hitbox (must be IsTrigger = true)")]
    public BoxCollider2D attackCollider;
    [Tooltip("Offset of the attack collider (relative to this object) when facing right; X flips automatically")]
    public Vector2 attackColliderOffset = new Vector2(0.6f, 0f);

    [Header("Hit / Death")]
    [Tooltip("How long the player is locked and playing the Hit animation.")]
    public float hitStun = 0.25f;
    [Tooltip("Player starting health.")]
    public int maxHealth = 5;

    Animator animator;
    Rigidbody2D rigidBody;
    SpriteRenderer spriteRenderer;

    bool isGrounded;

    // Combo state
    bool attacking;            // locks motion & jump during combo
    bool allowCombo;           // window open to accept next
    bool queuedNext;           // input pressed during window
    int comboStep;             // 0 = none, 1/2/3
    Coroutine comboCo;

    // cache original collider size/offset (so we can flip offset cleanly)
    Vector2 attackColliderOriginalSize;

    // --- NEW: hit / death state ---
    int health;
    bool stunned;              // true while Hit anim is playing
    bool isDead;
    Coroutine hitCo;
    RigidbodyConstraints2D originalConstraints;

    void Start()
    {
        animator = GetComponentInChildren<Animator>();
        rigidBody = GetComponentInChildren<Rigidbody2D>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        rigidBody.freezeRotation = true;

        if (attackCollider != null)
        {
            attackColliderOriginalSize = attackCollider.size;
            attackCollider.enabled = false; // off by default
            UpdateAttackColliderFacing();   // set initial offset
        }
        else
        {
            Debug.LogWarning("[TwoD_Player] Attack collider not assigned.");
        }

        // record original constraints (ensure rotation frozen)
        if ((rigidBody.constraints & RigidbodyConstraints2D.FreezeRotation) == 0)
            rigidBody.constraints |= RigidbodyConstraints2D.FreezeRotation;
        originalConstraints = rigidBody.constraints;

        health = Mathf.Max(1, maxHealth);
    }

    void Update()
    {
        if (isDead) return;

        // HARD-LOCK while stunned (Hit) or during attack combo
        if (stunned)
        {
            rigidBody.linearVelocity = new Vector2(0f, rigidBody.linearVelocity.y);
            // keep all locomotion bools off so no other anim plays
            animator.SetBool("isWalking", false);
            animator.SetBool("isRunning", false);
            animator.SetBool("isJumping", false);
            return;
        }

        HandleMovement();
        HandleJump();
        HandleAttack();
    }

    // ---------------- Movement ----------------
    void HandleMovement()
    {
        // Prevent movement while attacking
        if (attacking)
        {
            rigidBody.linearVelocity = new Vector2(0f, rigidBody.linearVelocity.y);
            animator.SetBool("isWalking", false);
            animator.SetBool("isRunning", false);
            return;
        }

        float x = Input.GetAxisRaw("Horizontal");
        float speedAbs = Mathf.Abs(x);

        bool runHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        bool isWalking = speedAbs > 0.01f && !runHeld;
        bool isRunning = speedAbs > 0.01f && runHeld;

        float speed = isRunning ? runSpeed : walkSpeed;

        var vel = rigidBody.linearVelocity;
        vel.x = x * speed;
        rigidBody.linearVelocity = vel;

        animator.SetBool("isWalking", isWalking);
        animator.SetBool("isRunning", isRunning);

        if (speedAbs > 0.01f)
        {
            bool faceLeft = x < 0f;
            if (spriteRenderer.flipX != faceLeft)
            {
                spriteRenderer.flipX = faceLeft;
                UpdateAttackColliderFacing();
            }
        }
    }

    // ---------------- Jump ----------------
    void HandleJump()
    {
        isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
        animator.SetBool("isJumping", !isGrounded);

        if (attacking) return; // no jumping while attacking

        if (isGrounded && Input.GetKeyDown(KeyCode.Space))
        {
            rigidBody.linearVelocity = new Vector2(rigidBody.linearVelocity.x, jumpForce);
            animator.SetBool("isJumping", true);
        }
    }

    // ---------------- Attack / Combo ----------------
    void HandleAttack()
    {
        if (stunned) return; // cannot attack while in hit animation

        bool attackPressed = Input.GetKeyDown(attackKey) || Input.GetMouseButtonDown(0);

        if (!attacking)
        {
            if (attackPressed)
                StartCombo();
        }
        else
        {
            if (attackPressed && allowCombo)
                queuedNext = true;
        }
    }

    void StartCombo()
    {
        if (comboCo != null) StopCoroutine(comboCo);
        comboCo = StartCoroutine(ComboRoutine());
    }

    IEnumerator ComboRoutine()
    {
        attacking = true;
        animator.SetBool("isAttacking", true);

        // --------- ATTACK 1 ---------
        comboStep = 1;
        animator.ResetTrigger("Attack2");
        animator.ResetTrigger("Attack3");
        animator.SetTrigger("Attack1");

        // Hitbox active frames: 15 -> 20 (out of 20)
        StartCoroutine(HitboxWindowByFrames(15, 20));

        yield return ChainWindow(attackTotal, comboOpen, comboClose);
        if (!queuedNext) { EndCombo(); yield break; }

        // --------- ATTACK 2 ---------
        queuedNext = false;
        comboStep = 2;
        animator.SetTrigger("Attack2");

        // Hitbox active frames: 7 -> 20
        StartCoroutine(HitboxWindowByFrames(7, 20));

        yield return ChainWindow(attackTotal, comboOpen, comboClose);
        if (!queuedNext) { EndCombo(); yield break; }

        // --------- ATTACK 3 ---------
        queuedNext = false;
        comboStep = 3;
        animator.SetTrigger("Attack3");

        // Hitbox active frames: 15 -> 20
        StartCoroutine(HitboxWindowByFrames(15, 20));

        yield return new WaitForSeconds(attackTotal);

        EndCombo();
    }

    IEnumerator ChainWindow(float total, float openAt, float closeAt)
    {
        allowCombo = false;

        // Wait until window opens
        if (openAt > 0f) yield return new WaitForSeconds(openAt);
        allowCombo = true;

        // Keep open
        float window = Mathf.Max(0f, closeAt - openAt);
        if (window > 0f) yield return new WaitForSeconds(window);
        allowCombo = false;

        // Finish animation
        float rest = Mathf.Max(0f, total - closeAt);
        if (rest > 0f) yield return new WaitForSeconds(rest);
    }

    IEnumerator HitboxWindowByFrames(int startFrameInclusive, int endFrameInclusive)
    {
        if (attackCollider == null) yield break;

        // guard
        startFrameInclusive = Mathf.Clamp(startFrameInclusive, 1, 20);
        endFrameInclusive = Mathf.Clamp(endFrameInclusive, 1, 20);
        if (endFrameInclusive < startFrameInclusive)
            yield break;

        float startT = attackTotal * (startFrameInclusive / 20f);
        float endT = attackTotal * (endFrameInclusive / 20f);
        float activeDur = Mathf.Max(0f, endT - startT);

        // Ensure collider is off before starting
        attackCollider.enabled = false;

        // Wait until start frame
        if (startT > 0f) yield return new WaitForSeconds(startT);

        // Active frames
        attackCollider.enabled = true;
        yield return new WaitForSeconds(activeDur);

        // End
        attackCollider.enabled = false;
    }

    void EndCombo()
    {
        attacking = false;
        allowCombo = false;
        queuedNext = false;
        comboStep = 0;
        if (attackCollider != null) attackCollider.enabled = false;
        animator.SetBool("isAttacking", false);
        comboCo = null;
    }

    void UpdateAttackColliderFacing()
    {
        if (attackCollider == null) return;

        // flip offset based on facing (flipX mirrors sprite only, so we move the collider)
        float dir = spriteRenderer != null && spriteRenderer.flipX ? -1f : 1f;

        // Move collider to be "in front" of the player depending on facing
        attackCollider.offset = new Vector2(attackColliderOffset.x * dir, attackColliderOffset.y);
        attackCollider.size = attackColliderOriginalSize; // unchanged; here in case you want to tweak per direction
    }

    // ---------------- Trigger handling (print the layer name) ----------------
    void OnTriggerEnter2D(Collider2D other)
    {
        // Only respond if our attack collider is the one enabled & set as trigger
        if (attackCollider == null || !attackCollider.enabled) return;
        if (other == attackCollider) return;

        string layerName = LayerMask.LayerToName(other.gameObject.layer);
        Debug.Log($"[Attack Hit] Entered object layer: {layerName}", other.gameObject);

        var goblin = other.GetComponent<TwoD_Goblin>() ?? other.GetComponentInParent<TwoD_Goblin>();
        if (goblin != null)
        {
            goblin.Hit();
            return;
        }
    }

    // ---------------- HIT API (play Hit instantly, lock input/animations, instant Death) ----------------
    public void Hit()
    {
        if (isDead) return;

        // If lethal now or after decrement, die immediately
        if (health <= 0)
        {
            Die();
            return;
        }

        // Apply damage
        health--;

        // --- HARD STOP and prevent anything else this frame ---
        stunned = true; // set first so Update() early-outs right away

        // cancel ongoing attacks / inputs
        if (comboCo != null) { StopCoroutine(comboCo); comboCo = null; }
        attacking = false;
        allowCombo = false;
        queuedNext = false;
        comboStep = 0;

        // disable hitbox if active
        if (attackCollider) attackCollider.enabled = false;

        // zero motion & freeze X during stun
        rigidBody.linearVelocity = Vector2.zero;
        animator.SetBool("isWalking", false);
        animator.SetBool("isRunning", false);
        animator.SetBool("isJumping", false);

        // clear triggers that could re-route the state machine and re-trigger Hit cleanly
        animator.ResetTrigger("Attack1");
        animator.ResetTrigger("Attack2");
        animator.ResetTrigger("Attack3");
        animator.ResetTrigger("Hit");

        // freeze X so we don't slide
        rigidBody.constraints = originalConstraints | RigidbodyConstraints2D.FreezePositionX;

        // Force Hit to play immediately via Any State → Hit (Has Exit Time OFF)
        animator.SetTrigger("Hit");

        // start stun timer
        if (hitCo != null) StopCoroutine(hitCo);
        hitCo = StartCoroutine(HitStunRoutine());
    }

    IEnumerator HitStunRoutine()
    {
        yield return new WaitForSeconds(hitStun);

        // restore movement
        rigidBody.constraints = originalConstraints;
        stunned = false;
        hitCo = null;
    }

    void Die()
    {
        isDead = true;

        // stop everything
        stunned = true;
        if (comboCo != null) { StopCoroutine(comboCo); comboCo = null; }
        attacking = false;
        allowCombo = false;
        queuedNext = false;
        if (attackCollider) attackCollider.enabled = false;

        rigidBody.linearVelocity = Vector2.zero;
        animator.SetBool("isWalking", false);
        animator.SetBool("isRunning", false);
        animator.SetBool("isJumping", false);

        // clear triggers and jump to death clip instantly
        animator.ResetTrigger("Attack1");
        animator.ResetTrigger("Attack2");
        animator.ResetTrigger("Attack3");
        animator.ResetTrigger("Hit");
        animator.Play("Player_Dead", 0, 0f);

        // optional: disable controls script after death
        enabled = false;
    }

    // ---------------- Gizmos ----------------
    void OnDrawGizmosSelected()
    {
        if (!groundCheck) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);

        // visualize intended attack collider offset
        if (attackCollider != null)
        {
            Gizmos.color = Color.red;
            float dir = (spriteRenderer != null && spriteRenderer.flipX) ? -1f : 1f;
            Vector3 center = transform.position + new Vector3(attackColliderOffset.x * dir, attackColliderOffset.y, 0f);
            Gizmos.DrawWireCube(center, attackCollider.size);
        }
    }
}
