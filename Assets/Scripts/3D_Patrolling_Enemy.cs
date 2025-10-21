using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(NavMeshAgent))]
public class ThreeD_Patrolling_Enemy : MonoBehaviour
{
    [Header("Patrol")]
    public Transform[] patrolPoints;
    private Vector3[] patrolPositions;
    public float moveSpeed = 1.5f;
    public float sprintMultiplier = 2.0f;
    public float waitTimeAtPoint = 2f;
    public float pointReachThreshold = 0.5f;
    private int currentPatrolIndex = 0;
    private float waitTimer = 0f;
    private bool waiting = false;

    [Header("Detection / Chase")]
    public float detectionRange = 10f;
    public float chaseLoseDistance = 15f;
    public float viewAngle = 75f;
    public float attackRange = 2f;
    public LayerMask obstructionMask = ~0;
    private Transform player;
    private ThreeD_Character playerCharacter;
    private bool isChasing = false;
    private bool inAttackRange = false;

    [Header("Attack / Combo (AI)")]
    int maxCombo = 4;
    public float attackRayDistance = 2f;
    public LayerMask attackHitMask = ~0;
    public float attackCooldown = 2f;
    public float attackDelay = 0.3f;
    public float enemyDamage = 10f;
    private float postHitAttackLockout = 0f;

    private bool isAttacking = false;
    private int comboIndex = 0;
    private float sprintAxis = 0f;
    private float attackCooldownTimer = 0f;
    private bool playerDeadDuringAttack = false;

    [Header("Blocking")]
    private bool blocking = false;
    public AudioClip blockingSound;
    float playerTurnSpeed = 5f;

    [Header("Health & Hit")]
    private bool isHit = false;
    private bool dead = false;
    public float health = 50.0f;
    public float maxHealth = 50.0f;
    float hitStunDuration = 0.5f;
    public AudioClip hitSound;
    public AudioClip deathSound;

    private NavMeshAgent navAgent;
    private Animator m_Animator;

    private float stuckTimer = 0f;
    public float stuckTimeout = 2.0f;
    public float stuckVelocityThreshold = 0.05f;

    void Start()
    {
        m_Animator = GetComponent<Animator>();
        navAgent = GetComponent<NavMeshAgent>();

        navAgent.updatePosition = true;
        navAgent.updateRotation = false;
        navAgent.speed = moveSpeed;
        navAgent.stoppingDistance = attackRange;
        navAgent.autoBraking = false;

        var pgo = GameObject.FindWithTag("Player");
        if (pgo != null)
        {
            player = pgo.transform;
            playerCharacter = pgo.GetComponent<ThreeD_Character>();
        }

        if (patrolPoints == null || patrolPoints.Length == 0)
        {
            Transform container = transform.Find("PatrolPoints");
            if (container == null)
            {
                GameObject found = GameObject.Find("PatrolPoints");
                if (found != null) container = found.transform;
            }

            if (container != null)
            {
                List<Transform> pts = new List<Transform>();
                foreach (Transform child in container)
                {
                    if (!child.gameObject.activeInHierarchy) continue;
                    pts.Add(child);
                }

                if (pts.Count > 0)
                    patrolPoints = pts.ToArray();
            }
        }

        if (patrolPoints != null && patrolPoints.Length > 0)
        {
            patrolPositions = new Vector3[patrolPoints.Length];
            for (int i = 0; i < patrolPoints.Length; i++)
            {
                if (patrolPoints[i] != null)
                    patrolPositions[i] = patrolPoints[i].position;
                else
                    patrolPositions[i] = transform.position;
            }
        }

        if (patrolPositions == null || patrolPositions.Length == 0)
        {
            waiting = true;
            waitTimer = Mathf.Infinity;
            if (navAgent != null) navAgent.isStopped = true;
        }
        else
        {
            if (navAgent != null)
            {
                navAgent.isStopped = false;
                navAgent.speed = moveSpeed;
                navAgent.SetDestination(patrolPositions[currentPatrolIndex]);
            }
        }

        if (chaseLoseDistance < detectionRange) chaseLoseDistance = detectionRange * 1.5f;

        // Initialize health
        health = maxHealth;
    }

    private void Update()
    {
        // Check if enemy is dead
        if (dead)
        {
            // Only update animator for death animations
            UpdateAnimator();
            return;
        }

        // Check if player is dead
        if (IsPlayerDead())
        {
            // If we're not in the middle of an attack, stop everything immediately
            if (!isAttacking && (isChasing || inAttackRange))
            {
                Debug.Log("[Enemy] Player is dead, stopping all combat and returning to patrol");
                StopAllCombat();
                ResumePatrol();
            }
            // If we are attacking, set a flag but let the attack finish
            else if (isAttacking && !playerDeadDuringAttack)
            {
                Debug.Log("[Enemy] Player died during attack, will finish current attack then stop");
                playerDeadDuringAttack = true;
            }

            // IMPORTANT: Even if player is dead, we still need to update the combo state machine
            // to detect when the attack animation finishes and reset isAttacking
            if (isAttacking)
            {
                UpdateComboStateMachine();
            }

            // Still update animator and patrol even if player is dead
            HandleMovementBehavior();
            UpdateAnimator();
            return;
        }

        if (!isHit)
            DetectPlayer();
        if (postHitAttackLockout > 0f)
            postHitAttackLockout -= Time.deltaTime;
        HandleMovementBehavior();
        HandleAICombat();
        HandleBlockInput_AI();
        UpdateComboStateMachine();
        UpdateAnimator();
    }

    // Method to check if player is dead
    private bool IsPlayerDead()
    {
        return playerCharacter != null && playerCharacter.IsDead();
    }

    // Method to stop all combat immediately
    private void StopAllCombat()
    {
        StopAllCoroutines();
        isAttacking = false;
        isChasing = false;
        inAttackRange = false;
        blocking = false;
        playerDeadDuringAttack = false;

        if (m_Animator != null)
        {
            m_Animator.SetBool("Attack", false);
            m_Animator.SetBool("Block", false);
        }

        if (navAgent != null)
        {
            navAgent.isStopped = false;
        }
    }

    #region Health & Hit System


    private void HandleDeath()
    {
        dead = true;

        // Stop all combat and movement
        StopAllCombat();

        if (navAgent != null)
        {
            navAgent.isStopped = true;
        }

        // Set animator parameters
        if (m_Animator != null)
        {
            m_Animator.SetBool("Attack", false);
            m_Animator.SetBool("Block", false);
            m_Animator.SetBool("Hit", false);
            m_Animator.SetBool("Dead", true);
        }

        // Play death sound if available
        if (deathSound != null)
        {
            AudioSource.PlayClipAtPoint(deathSound, transform.position);
        }
              
    }

    private IEnumerator DestroyAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        Destroy(gameObject);
    }

    #endregion

    #region Movement / Patrol / Chase

    private void HandleMovementBehavior()
    {
        if (dead || isHit)
        {
            if (navAgent != null) navAgent.isStopped = true;
            sprintAxis = 0f;
            return;
        }

        // Check if player is dead
        if (IsPlayerDead() && !isAttacking)
        {
            if (isChasing || inAttackRange)
            {
                Debug.Log("[Enemy] Player is dead, stopping combat");
                StopAllCombat();
                ResumePatrol();
            }
            return;
        }

        if (inAttackRange)
        {
            if (navAgent != null) navAgent.isStopped = true;
            sprintAxis = 0f;

            if (player != null)
            {
                Vector3 dirToPlayer = (player.position - transform.position);
                dirToPlayer.y = 0f;
                if (dirToPlayer.sqrMagnitude > 0.0001f) RotateTowards(dirToPlayer.normalized);
            }
            return;
        }

        if (isChasing && player != null)
        {
            // Check if player is dead during chase
            if (IsPlayerDead() && !isAttacking)
            {
                Debug.Log("[Enemy] Player died during chase, returning to patrol");
                isChasing = false;
                inAttackRange = false;
                ResumePatrol();
                return;
            }

            float dist = Vector3.Distance(transform.position, player.position);

            if (dist > chaseLoseDistance)
            {
                isChasing = false;
                inAttackRange = false;
                ResumePatrol();
                return;
            }

            bool inFOV = IsInViewAngle(player);
            bool los = HasLineOfSight(player);
            if (!inFOV && !los)
            {
                isChasing = false;
                inAttackRange = false;
                ResumePatrol();
                return;
            }

            if (dist <= attackRange)
            {
                inAttackRange = true;
                sprintAxis = 0f;
                if (navAgent != null) navAgent.isStopped = true;

                Vector3 dirToPlayer = (player.position - transform.position);
                dirToPlayer.y = 0f;
                if (dirToPlayer.sqrMagnitude > 0.0001f) RotateTowards(dirToPlayer.normalized);
                return;
            }

            sprintAxis = 1f;
            if (navAgent != null)
            {
                navAgent.isStopped = false;
                navAgent.speed = moveSpeed * sprintMultiplier;
                navAgent.SetDestination(player.position);
            }

            Vector3 chaseDir = (player.position - transform.position);
            chaseDir.y = 0f;
            if (chaseDir.sqrMagnitude > 0.0001f) RotateTowards(chaseDir.normalized);

            stuckTimer = 0f;
            return;
        }

        PatrolTick();
    }

    private void PatrolTick()
    {
        if (patrolPositions == null || patrolPositions.Length == 0)
            return;

        Vector3 targetPos = patrolPositions[currentPatrolIndex];
        Vector3 toPoint = targetPos - transform.position;
        toPoint.y = 0f;
        float dist = toPoint.magnitude;

        if (dist <= pointReachThreshold && !waiting)
        {
            waiting = true;
            waitTimer = 0f;
            if (navAgent != null) navAgent.isStopped = true;
            sprintAxis = 0f;
            return;
        }

        if (waiting)
        {
            if (navAgent != null) navAgent.isStopped = true;

            waitTimer += Time.deltaTime;
            if (waitTimer >= waitTimeAtPoint)
            {
                waiting = false;
                waitTimer = 0f;
                currentPatrolIndex = (currentPatrolIndex + 1) % patrolPositions.Length;
                if (navAgent != null)
                {
                    navAgent.isStopped = false;
                    navAgent.speed = moveSpeed;
                    navAgent.SetDestination(patrolPositions[currentPatrolIndex]);
                }
            }
            sprintAxis = 0f;
            stuckTimer = 0f;
            return;
        }

        sprintAxis = 0f;
        if (navAgent != null)
        {
            navAgent.isStopped = false;
            navAgent.speed = moveSpeed;
            Vector3 dest = patrolPositions[currentPatrolIndex];

            if (Vector3.Distance(navAgent.destination, dest) > 0.05f)
            {
                navAgent.SetDestination(dest);
            }
        }

        if (navAgent != null)
        {
            Vector3 vel = navAgent.velocity;
            vel.y = 0f;
            if (vel.sqrMagnitude > 0.001f)
                RotateTowards(vel.normalized);
        }

        if (navAgent != null)
        {
            float planarSpeed = new Vector3(navAgent.velocity.x, 0f, navAgent.velocity.z).magnitude;

            if (planarSpeed < stuckVelocityThreshold && !waiting && !isChasing && !inAttackRange)
                stuckTimer += Time.deltaTime;
            else
                stuckTimer = 0f;

            if (stuckTimer > stuckTimeout)
            {
                stuckTimer = 0f;
                if (navAgent != null)
                {
                    navAgent.ResetPath();
                    navAgent.SetDestination(patrolPositions[currentPatrolIndex]);
                    navAgent.isStopped = false;
                }
                currentPatrolIndex = (currentPatrolIndex + 1) % patrolPositions.Length;
                if (navAgent != null)
                {
                    navAgent.SetDestination(patrolPositions[currentPatrolIndex]);
                }
            }
        }
    }

    private void ResumePatrol()
    {
        if (patrolPositions == null || patrolPositions.Length == 0) return;

        // Ensure all combat states are reset
        StopAllCombat();

        if (navAgent != null)
        {
            navAgent.isStopped = false;
            navAgent.speed = moveSpeed;
            navAgent.SetDestination(patrolPositions[currentPatrolIndex]);
        }

        Debug.Log("[Enemy] Returning to patrol");
    }

    #endregion

    #region Detection / LOS / FOV

    private void DetectPlayer()
    {
        if (player == null) return;
        if (isHit) return;

        // Don't detect if player is dead
        if (IsPlayerDead()) return;

        float dist = Vector3.Distance(transform.position, player.position);

        if (dist <= detectionRange && IsInViewAngle(player) && HasLineOfSight(player))
        {
            isChasing = true;
            inAttackRange = false;
            if (navAgent != null)
            {
                navAgent.isStopped = false;
                navAgent.speed = moveSpeed * sprintMultiplier;
                navAgent.SetDestination(player.position);
            }
        }
    }

    private bool IsInViewAngle(Transform target)
    {
        Vector3 toTarget = (target.position - transform.position);
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude <= 0.0001f) return true;
        float halfAngle = viewAngle * 0.5f;
        float angle = Vector3.Angle(transform.forward, toTarget.normalized);
        return angle <= halfAngle;
    }

    private bool HasLineOfSight(Transform target)
    {
        Vector3 origin = transform.position + Vector3.up * 1f;
        Vector3 dir = (target.position + Vector3.up * 1f) - origin;
        float dist = dir.magnitude;
        dir.Normalize();

        if (Physics.Raycast(origin, dir, out RaycastHit hit, dist, obstructionMask))
        {
            if (hit.collider != null && (hit.collider.transform.IsChildOf(target) || hit.collider.gameObject.CompareTag("Player")))
                return true;
            return false;
        }

        return true;
    }

    #endregion

    #region AI Combat System

    private void HandleAICombat()
    {
        if (dead || isHit || blocking) return;

        // Don't attack if recently hit
        if (postHitAttackLockout > 0f)
            return;

        // Don't attack if player is dead
        if (IsPlayerDead())
        {
            if (!isAttacking && inAttackRange)
            {
                StopAllCombat();
                ResumePatrol();
            }
            return;
        }

        // Update attack cooldown timer
        if (attackCooldownTimer > 0f)
        {
            attackCooldownTimer -= Time.deltaTime;
        }

        // Start attacking when in range, not already attacking, and cooldown is over
        if (inAttackRange && !isAttacking && attackCooldownTimer <= 0f)
        {
            StartAttack(1);
        }
    }


    private void StartAttack(int index)
    {
        if (index < 1 || index > maxCombo) return;
        if (isHit) return;

        // Don't start attack if player is dead
        if (IsPlayerDead())
        {
            StopAllCombat();
            ResumePatrol();
            return;
        }

        comboIndex = index;
        isAttacking = true;
        playerDeadDuringAttack = false;


        if (m_Animator != null)
        {
            string stateName = "Attack" + index;
            m_Animator.Play(stateName, 0, 0f);
            m_Animator.SetBool("Attack", true);
        }

        // Start delayed raycast to match sword swing timing
        StartCoroutine(DelayedAttackRaycast());
    }

    private IEnumerator DelayedAttackRaycast()
    {
        yield return new WaitForSeconds(attackDelay);

        // Check if player died during the delay
        if (IsPlayerDead())
        {
            Debug.Log("[Enemy] Player died during attack delay, cancelling attack");
            // Don't stop the attack animation, just skip the raycast
            yield break;
        }

        if (isAttacking) // Only do raycast if still attacking (wasn't interrupted)
        {
            DoAttackRaycast();
        }
    }

    private void DoAttackRaycast()
    {
        if (playerCharacter == null) return;

        // Don't attack if player is dead
        if (IsPlayerDead())
        {
            Debug.Log("[Enemy] Cannot attack dead player");
            // Don't stop the attack animation, just skip the damage
            return;
        }

        Vector3 origin = transform.position + Vector3.up * 1f;
        Vector3 dir = transform.forward;

        if (Physics.Raycast(origin, dir, out RaycastHit hit, attackRayDistance, attackHitMask))
        {
            GameObject hitObject = hit.collider.gameObject;

            if (hitObject.CompareTag("Player"))
            {
                // Check if player is blocking - only hit if NOT blocking
                if (!playerCharacter.IsBlocking())
                {
                    playerCharacter.GetHit(enemyDamage);
                }
                else
                {
                    PlayBlockingSound();
                    MakePlayerFaceEnemy();
                }
            }
        }

        Debug.DrawRay(origin, dir * attackRayDistance, Color.yellow, 0.5f);
    }

    public void PlayBlockingSound()
    {
        if (blockingSound != null && player != null)
        {
            AudioSource.PlayClipAtPoint(blockingSound, player.position);
        }
    }

    private void MakePlayerFaceEnemy()
    {
        if (player == null) return;
        StartCoroutine(RotatePlayerToFaceEnemy());
    }

    private IEnumerator RotatePlayerToFaceEnemy()
    {
        if (player == null) yield break;

        Vector3 directionToEnemy = (transform.position - player.position).normalized;
        directionToEnemy.y = 0f; 

        if (directionToEnemy.sqrMagnitude <= 0.0001f) yield break;

        Quaternion targetRotation = Quaternion.LookRotation(directionToEnemy);
        float rotationProgress = 0f;

        while (rotationProgress < 1f)
        {
            rotationProgress += Time.deltaTime * playerTurnSpeed;
            player.rotation = Quaternion.Slerp(player.rotation, targetRotation, rotationProgress);
            yield return null;
        }

        player.rotation = targetRotation;
    }

    private void UpdateComboStateMachine()
    {
        if (!isAttacking) return;
        if (m_Animator == null) return;

        AnimatorStateInfo state = m_Animator.GetCurrentAnimatorStateInfo(0);
        float normalizedTime = state.normalizedTime;

        // Check if current attack animation has finished
        if (normalizedTime >= 1.0f)
        {
            // If player died during this attack, stop the combo and return to patrol
            if (playerDeadDuringAttack || IsPlayerDead())
            {
                EndCombo();
                ResumePatrol();
                return;
            }

            // If we have more attacks in the combo, go to next one
            if (comboIndex < maxCombo)
            {
                comboIndex++;
                StartAttack(comboIndex);
            }
            else
            {
                EndCombo();
            }
        }
    }

    private void EndCombo()
    {
        isAttacking = false;
        comboIndex = 0;
        playerDeadDuringAttack = false;

        // Start cooldown after finishing combo
        attackCooldownTimer = attackCooldown;

        if (m_Animator != null)
        {
            m_Animator.SetBool("Attack", false);
        }

    }

    #endregion

    #region Blocking (AI hooks)

    private void HandleBlockInput_AI()
    {
        if (isHit || dead) return;

        // Simple blocking logic - block when not attacking and in attack range
        if (!isAttacking && inAttackRange && attackCooldownTimer <= 0f)
        {
            blocking = true;
        }
        else
        {
            blocking = false;
        }

        if (m_Animator != null) m_Animator.SetBool("Block", blocking);
    }

    #endregion

    #region Animator

    private void UpdateAnimator()
    {
        if (dead)
        {
            // Only update the dead state
            if (m_Animator != null)
            {
                m_Animator.SetBool("Dead", true);
            }
            return;
        }

        Vector3 navVel = navAgent != null ? navAgent.velocity : Vector3.zero;
        float forwardSpeed = new Vector3(navVel.x, 0f, navVel.z).magnitude;

        float smoothTime = 0.1f;
        if (m_Animator != null)
        {
            m_Animator.SetFloat("Speed", forwardSpeed, smoothTime, Time.deltaTime);
            m_Animator.SetBool("Attack", isAttacking);
            m_Animator.SetFloat("Sprint", sprintAxis);
            m_Animator.SetBool("Block", blocking);
            m_Animator.SetBool("Hit", isHit);
            m_Animator.SetBool("Dead", dead);
        }
    }

    #endregion

    #region Hit / Stun

    public void GetHit(float damage)
    {
        if (isHit || dead) return;

        health -= damage;

        if (health <= 0)
        {
            health = 0;
            HandleDeath();
            return;
        }

        StartCoroutine(HitRoutine());
        postHitAttackLockout = 2f;
    }

    private IEnumerator HitRoutine()
    {
        isHit = true;

        // If enemy wasn't aware of player before being hit, become aware
        if (!isChasing && !inAttackRange && player != null)
        {
            Debug.Log("[Enemy] Hit by player, becoming aware!");
            isChasing = true;
            inAttackRange = false;

            if (navAgent != null)
            {
                navAgent.isStopped = false;
                navAgent.speed = moveSpeed * sprintMultiplier;
                navAgent.SetDestination(player.position);
            }
        }

        isAttacking = false;

        if (m_Animator != null)
        {
            m_Animator.SetBool("Attack", false);
            m_Animator.SetBool("Hit", true);
        }

        if (navAgent != null) navAgent.isStopped = true;

        // Play hit sound if available
        if (hitSound != null)
        {
            AudioSource.PlayClipAtPoint(hitSound, transform.position);
        }

        yield return new WaitForSeconds(hitStunDuration);

        isHit = false;

        if (m_Animator != null) m_Animator.SetBool("Hit", false);

        // After hit recovery, continue chasing if player is still valid
        if (!dead && isChasing && player != null && !IsPlayerDead() &&
            IsInViewAngle(player) && HasLineOfSight(player) &&
            Vector3.Distance(transform.position, player.position) <= chaseLoseDistance)
        {
            inAttackRange = false;
            if (navAgent != null)
            {
                navAgent.isStopped = false;
                navAgent.speed = moveSpeed * sprintMultiplier;
                navAgent.SetDestination(player.position);
            }
        }
        else if (!dead)
        {
            ResumePatrol();
        }
    }

    #endregion

    #region Utilities

    private void RotateTowards(Vector3 dir)
    {
        if (dir.sqrMagnitude <= 0.0001f) return;
        Quaternion targetRot = Quaternion.LookRotation(dir, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 8f);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        if (patrolPositions != null)
        {
            for (int i = 0; i < patrolPositions.Length; i++)
            {
                Gizmos.DrawSphere(patrolPositions[i], 0.2f);
                if (i < patrolPositions.Length - 1)
                    Gizmos.DrawLine(patrolPositions[i], patrolPositions[i + 1]);
            }
            if (patrolPositions.Length > 1)
                Gizmos.DrawLine(patrolPositions[patrolPositions.Length - 1], patrolPositions[0]);
        }
        else if (patrolPoints != null)
        {
            for (int i = 0; i < patrolPoints.Length; i++)
            {
                if (patrolPoints[i] != null)
                    Gizmos.DrawSphere(patrolPoints[i].position, 0.15f);
            }
        }

        if (patrolPositions != null && patrolPositions.Length > 0)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(patrolPositions[currentPatrolIndex], pointReachThreshold);
        }

        Gizmos.color = new Color(1f, 0.5f, 0f, 0.15f);
        Vector3 fwd = transform.forward;
        Quaternion leftQ = Quaternion.Euler(0f, -viewAngle * 0.5f, 0f);
        Quaternion rightQ = Quaternion.Euler(0f, viewAngle * 0.5f, 0f);
        Gizmos.DrawRay(transform.position + Vector3.up * 0.25f, leftQ * fwd * detectionRange);
        Gizmos.DrawRay(transform.position + Vector3.up * 0.25f, rightQ * fwd * detectionRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
    #endregion

    public bool IsBlocking()
    {
        if (dead) return false;

        blocking = UnityEngine.Random.value > 0.75f;

        if (blocking)
        {
            postHitAttackLockout = 1f;
            StartCoroutine(ResetBlockingAfterDelay(0.2f));
        }

        return blocking;
    }

    private IEnumerator ResetBlockingAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        blocking = false;
    }

    public bool IsDead()
    {
        return dead;
    }
}