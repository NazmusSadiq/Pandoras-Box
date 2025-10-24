using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(NavMeshAgent))]
public class ThreeD_Enemy : MonoBehaviour
{
    [Header("Patrol")]
    public Transform[] patrolPoints;
    protected Vector3[] patrolPositions;
    public float moveSpeed = 1.5f;
    public float sprintMultiplier = 2.0f;
    public float waitTimeAtPoint = 2f;
    public float pointReachThreshold = 0.5f;
    protected int currentPatrolIndex = 0;
    protected float waitTimer = 0f;
    protected bool waiting = false;

    [Header("Detection / Chase")]
    public float detectionRange = 10f;
    public float chaseLoseDistance = 15f;
    public float viewAngle = 75f;
    public float attackRange = 2f;
    public LayerMask obstructionMask = ~0;
    protected Transform player;
    protected ThreeD_Character playerCharacter;
    protected bool isChasing = false;
    protected bool inAttackRange = false;
    protected bool isAware = false;

    [Header("Attack / Combo (AI)")]
    protected int maxCombo = 4;
    public float attackRayDistance = 2f;
    public LayerMask attackHitMask = ~0;
    public float attackCooldown = 2f;
    public float attackDelay = 0.3f;
    public float enemyDamage = 10f;
    protected float postHitAttackLockout = 0f;

    protected bool isAttacking = false;
    protected int comboIndex = 0;
    protected float sprintAxis = 0f;
    protected float attackCooldownTimer = 0f;
    protected bool playerDeadDuringAttack = false;

    [Header("Blocking")]
    protected bool blocking = false;
    public AudioClip blockingSound;
    protected float playerTurnSpeed = 5f;
    protected bool canPushback = true;
    protected float blockChance = 0.25f;

    [Header("Health & Hit")]
    protected bool isHit = false;
    protected bool dead = false;
    public float health = 50.0f;
    public float maxHealth = 50.0f;
    protected float hitStunDuration = 0.5f;
    public AudioClip hitSound;
    public AudioClip deathSound;

    protected NavMeshAgent navAgent;
    protected Animator m_Animator;

    protected float stuckTimer = 0f;
    public float stuckTimeout = 2.0f;
    public float stuckVelocityThreshold = 0.05f;

    public bool IsAware => isAware;

    protected bool IsPlayerDead()
    {
        return playerCharacter != null && playerCharacter.IsDead();
    }

    protected void StopAllCombat()
    {
        StopAllCoroutines();
        isAttacking = false;
        isChasing = false;
        inAttackRange = false;
        blocking = false;
        playerDeadDuringAttack = false;
        isAware = false;

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

    protected void HandleDeath()
    {
        dead = true;
        isAware = false; 

        StopAllCombat();

        if (navAgent != null)
        {
            navAgent.isStopped = true;
        }

        if (m_Animator != null)
        {
            m_Animator.SetBool("Attack", false);
            m_Animator.SetBool("Block", false);
            m_Animator.SetBool("Hit", false);
            m_Animator.SetBool("Dead", true);
        }

        if (deathSound != null)
        {
            AudioSource.PlayClipAtPoint(deathSound, transform.position);
        }
    }

    protected IEnumerator DestroyAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        Destroy(gameObject);
    }

    #endregion

    #region Movement / Patrol / Chase

    protected void HandleMovementBehavior()
    {
        if (dead || isHit)
        {
            if (navAgent != null) navAgent.isStopped = true;
            sprintAxis = 0f;
            return;
        }

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
            if (IsPlayerDead() && !isAttacking)
            {
                Debug.Log("[Enemy] Player died during chase, returning to patrol");
                isChasing = false;
                inAttackRange = false;
                isAware = false; // NEW: No longer aware
                ResumePatrol();
                return;
            }

            float dist = Vector3.Distance(transform.position, player.position);

            if (dist > chaseLoseDistance)
            {
                isChasing = false;
                inAttackRange = false;
                isAware = false; // NEW: Lost awareness
                ResumePatrol();
                return;
            }

            bool inFOV = IsInViewAngle(player);
            bool los = HasLineOfSight(player);
            if (!inFOV && !los)
            {
                isChasing = false;
                inAttackRange = false;
                isAware = false; // NEW: Lost awareness
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

    protected void PatrolTick()
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

    protected void ResumePatrol()
    {
        if (patrolPositions == null || patrolPositions.Length == 0) return;

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

    protected void DetectPlayer()
    {
        if (player == null) return;
        if (isHit) return;

        if (IsPlayerDead()) return;

        float dist = Vector3.Distance(transform.position, player.position);

        if (dist <= detectionRange && IsInViewAngle(player) && HasLineOfSight(player))
        {
            isChasing = true;
            inAttackRange = false;
            isAware = true; // NEW: Enemy becomes aware of player
            if (navAgent != null)
            {
                navAgent.isStopped = false;
                navAgent.speed = moveSpeed * sprintMultiplier;
                navAgent.SetDestination(player.position);
            }
        }
    }

    protected bool IsInViewAngle(Transform target)
    {
        Vector3 toTarget = (target.position - transform.position);
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude <= 0.0001f) return true;
        float halfAngle = viewAngle * 0.5f;
        float angle = Vector3.Angle(transform.forward, toTarget.normalized);
        return angle <= halfAngle;
    }

    protected bool HasLineOfSight(Transform target)
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

    virtual protected void HandleAICombat()
    {
        if (dead || isHit || blocking) return;

        if (postHitAttackLockout > 0f)
            return;

        if (IsPlayerDead())
        {
            if (!isAttacking && inAttackRange)
            {
                StopAllCombat();
                ResumePatrol();
            }
            return;
        }

        if (attackCooldownTimer > 0f)
        {
            attackCooldownTimer -= Time.deltaTime;
        }

        if (inAttackRange && !isAttacking && attackCooldownTimer <= 0f)
        {
            StartAttack(1);
        }
    }

    virtual protected void StartAttack(int index)
    {
        if (index < 1 || index > maxCombo) return;
        if (isHit) return;

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

        StartCoroutine(DelayedAttackRaycast());
    }

    virtual protected IEnumerator DelayedAttackRaycast()
    {
        yield return new WaitForSeconds(attackDelay);

        if (IsPlayerDead())
        {
            Debug.Log("[Enemy] Player died during attack delay, cancelling attack");
            yield break;
        }

        if (isAttacking)
        {
            DoAttackRaycast();
        }
    }

    virtual protected void DoAttackRaycast()
    {
        if (playerCharacter == null) return;

        if (IsPlayerDead())
        {
            Debug.Log("[Enemy] Cannot attack dead player");
            return;
        }

        Vector3 origin = transform.position + Vector3.up * 1f;
        Vector3 dir = transform.forward;

        if (Physics.Raycast(origin, dir, out RaycastHit hit, attackRayDistance, attackHitMask))
        {
            GameObject hitObject = hit.collider.gameObject;

            if (hitObject.CompareTag("Player"))
            {
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

    protected void MakePlayerFaceEnemy()
    {
        if (player == null) return;
        StartCoroutine(RotatePlayerToFaceEnemy());
    }

    protected IEnumerator RotatePlayerToFaceEnemy()
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

    protected void UpdateComboStateMachine()
    {
        if (!isAttacking) return;
        if (m_Animator == null) return;

        AnimatorStateInfo state = m_Animator.GetCurrentAnimatorStateInfo(0);
        float normalizedTime = state.normalizedTime;

        if (normalizedTime >= 1.0f)
        {
            if (playerDeadDuringAttack || IsPlayerDead())
            {
                EndCombo();
                ResumePatrol();
                return;
            }

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

    protected void EndCombo()
    {
        isAttacking = false;
        comboIndex = 0;
        playerDeadDuringAttack = false;

        attackCooldownTimer = attackCooldown;

        if (m_Animator != null)
        {
            m_Animator.SetBool("Attack", false);
        }
    }

    #endregion

    #region Animator

    virtual protected void UpdateAnimator()
    {
        if (dead)
        {
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
            m_Animator.SetBool("Aware", isAware);
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

    protected IEnumerator HitRoutine()
    {
        isHit = true;

        if (!isChasing && !inAttackRange && player != null)
        {
            Debug.Log("[Enemy] Hit by player, becoming aware!");
            isChasing = true;
            inAttackRange = false;
            isAware = true; 

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

        if (hitSound != null)
        {
            AudioSource.PlayClipAtPoint(hitSound, transform.position);
        }

        yield return new WaitForSeconds(hitStunDuration);

        isHit = false;

        if (m_Animator != null) m_Animator.SetBool("Hit", false);

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

    protected void RotateTowards(Vector3 dir)
    {
        if (dir.sqrMagnitude <= 0.0001f) return;
        Quaternion targetRot = Quaternion.LookRotation(dir, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 8f);
    }

    protected void OnDrawGizmosSelected()
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

        blocking = (UnityEngine.Random.value > (1-blockChance)) && isAware;

        if (blocking)
        {
            postHitAttackLockout = 1f;
            StartCoroutine(ResetBlockingAfterDelay(0.5f));
        }

        return blocking;
    }

    protected IEnumerator ResetBlockingAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        blocking = false;
    }

    public bool IsDead()
    {
        return dead;
    }

    public bool GetAwarenessState()
    {
        return isAware;
    }
}