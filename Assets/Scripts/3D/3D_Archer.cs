using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(NavMeshAgent))]
public class ThreeD_Archer : ThreeD_Enemy
{
    [Header("Archer Specific")]
    public float arrowProjectileSpeed = 15f;
    public float archerAttackRange = 8f; 
    public float timeBetweenShots = 2.5f;
    public GameObject arrowProjectilePrefab;
    public Transform arrowSpawnPoint;

    private bool isShooting = false;
    private float shotCooldownTimer = 0f;
    private float archerAttackDelay = 0.5f;
    public float arrowDeviationAngle = 5f;

    void Start()
    {
        m_Animator = GetComponent<Animator>();
        navAgent = GetComponent<NavMeshAgent>();
        blockChance = 0;

        navAgent.updatePosition = true;
        navAgent.updateRotation = false;
        navAgent.speed = moveSpeed;
        navAgent.stoppingDistance = archerAttackRange; 
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

        health = maxHealth;

        // Archer-specific initialization
        attackRange = archerAttackRange;

        if (arrowSpawnPoint == null)
        {
            Transform found = transform.Find("ArrowSpawnPoint");
            if (found != null)
                arrowSpawnPoint = found;
            else
                Debug.LogWarning("[Archer] ArrowSpawnPoint not found under Archer!");
        }
    }

    private void Update()
    {
        if (dead)
        {
            UpdateAnimator();
            return;
        }

        if (IsPlayerDead())
        {
            if (!isAttacking && (isChasing || inAttackRange))
            {
                Debug.Log("[Archer] Player is dead, stopping all combat and returning to patrol");
                StopAllCombat();
                ResumePatrol();
            }
            else if (isAttacking && !playerDeadDuringAttack)
            {
                Debug.Log("[Archer] Player died during attack, will finish current attack then stop");
                playerDeadDuringAttack = true;
            }

            if (isAttacking)
            {
                UpdateComboStateMachine();
            }

            HandleMovementBehavior();
            UpdateAnimator();
            return;
        }

        if (!isHit)
            DetectPlayer();
        if (postHitAttackLockout > 0f)
            postHitAttackLockout -= Time.deltaTime;

        if (shotCooldownTimer > 0f)
            shotCooldownTimer -= Time.deltaTime;

        HandleMovementBehavior();
        HandleArcherCombat();
        UpdateAnimator();
    }

    #region Archer Combat System

    private void HandleArcherCombat()
    {
        if (dead || isHit || blocking) return;

        if (postHitAttackLockout > 0f)
            return;

        if (IsPlayerDead())
        {
            if (inAttackRange)
            {
                StopAllCombat();
                ResumePatrol();
            }
            return;
        }

        if (inAttackRange && !isShooting && shotCooldownTimer <= 0f &&
            navAgent.velocity.magnitude < 0.1f)
        {
            StartShooting();
        }
    }

    private void StartShooting()
    {
        if (isHit) return;

        if (IsPlayerDead())
        {
            StopAllCombat();
            ResumePatrol();
            return;
        }

        isShooting = true;
        playerDeadDuringAttack = false;

        if (m_Animator != null)
        {
            m_Animator.SetBool("Attack", true);
        }

        StartCoroutine(DelayedArrowShot());
    }

    private IEnumerator DelayedArrowShot()
    {
        yield return new WaitForSeconds(archerAttackDelay);

        if (IsPlayerDead())
        {
            Debug.Log("[Archer] Player died during attack delay, cancelling shot");
            EndShooting();
            yield break;
        }

        if (isShooting)
        {
            FireArrow();
        }
    }

    private void FireArrow()
    {
        if (arrowProjectilePrefab == null)
        {
            Debug.LogWarning("[Archer] Arrow projectile prefab not set!");
            EndShooting();
            return;
        }

        if (arrowSpawnPoint == null)
        {
            Debug.LogWarning("[Archer] ArrowSpawnPoint not found! Assign or name it correctly under Archer.");
            EndShooting();
            return;
        }

        if (playerCharacter == null || IsPlayerDead())
        {
            EndShooting();
            return;
        }

        Vector3 dirToPlayer = (player.position - transform.position);
        dirToPlayer.y = 0f;
        if (dirToPlayer.sqrMagnitude > 0.0001f)
            RotateTowards(dirToPlayer.normalized);

        GameObject arrow = Instantiate(arrowProjectilePrefab, arrowSpawnPoint.position, arrowSpawnPoint.rotation, arrowSpawnPoint);

        Rigidbody rb = arrow.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
        }

        ArrowProjectile ap = arrow.GetComponent<ArrowProjectile>();
        if (ap != null) ap.damage = enemyDamage;

        EndShooting();

        StartCoroutine(LaunchArrowAfterDelay(arrow, timeBetweenShots));
    }

    private IEnumerator LaunchArrowAfterDelay(GameObject arrow, float delay)
    {
        if (arrow == null) yield break;

        yield return new WaitForSeconds(delay);

        if (arrow == null) yield break;

        arrow.transform.SetParent(null, true);

        Rigidbody rb = arrow.GetComponent<Rigidbody>();
        if (rb != null)
        {
            float deviation = UnityEngine.Random.Range(-arrowDeviationAngle, arrowDeviationAngle);
            Vector3 deviatedDir = Quaternion.Euler(0f, deviation, 0f) * arrowSpawnPoint.forward.normalized;
            rb.isKinematic = false;
            rb.linearVelocity = deviatedDir * arrowProjectileSpeed;
            ArrowProjectile arrowProjectile = arrow.GetComponent<ArrowProjectile>();
            if(arrowProjectile != null)
            {
                arrowProjectile.Release();
            }
        }
    }

    private void EndShooting()
    {
        isShooting = false;
        playerDeadDuringAttack = false;

        shotCooldownTimer = timeBetweenShots;

        if (m_Animator != null)
        {
            m_Animator.SetBool("Attack", false);
        }
    }

    #endregion

    #region Overridden Methods for Archer Behavior

    private new void HandleMovementBehavior()
    {
        if (dead || isHit)
        {
            if (navAgent != null) navAgent.isStopped = true;
            sprintAxis = 0f;
            return;
        }

        if (IsPlayerDead() && !isShooting)
        {
            if (isChasing || inAttackRange)
            {
                StopAllCombat();
                ResumePatrol();
            }
            return;
        }

        // Use archer-specific attack range
        float currentAttackRange = archerAttackRange;

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
            if (IsPlayerDead() && !isShooting)
            {
                Debug.Log("[Archer] Player died during chase, returning to patrol");
                isChasing = false;
                inAttackRange = false;
                isAware = false;
                ResumePatrol();
                return;
            }

            float dist = Vector3.Distance(transform.position, player.position);

            if (dist > chaseLoseDistance)
            {
                isChasing = false;
                inAttackRange = false;
                isAware = false;
                ResumePatrol();
                return;
            }

            bool inFOV = IsInViewAngle(player);
            bool los = HasLineOfSight(player);
            if (!inFOV && !los)
            {
                isChasing = false;
                inAttackRange = false;
                isAware = false;
                ResumePatrol();
                return;
            }

            if (dist <= currentAttackRange)
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

    #endregion

    #region Archer-specific Animator

    private new void UpdateAnimator()
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
            m_Animator.SetBool("Attack", isShooting);
            m_Animator.SetFloat("Sprint", sprintAxis);
            m_Animator.SetBool("Block", blocking);
            m_Animator.SetBool("Hit", isHit);
            m_Animator.SetBool("Dead", dead);
            m_Animator.SetBool("Aware", isAware);
        }
    }

    #endregion
}