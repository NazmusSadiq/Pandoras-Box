using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(NavMeshAgent))]
public class ThreeD_Boss : ThreeD_Enemy
{
    [Header("Boss - Jump Attack")]
    public float jumpAttackRange = 8f;
    public float jumpAttackCooldown = 10f;
    public float jumpAttackDamage = 25f;
    public float jumpAttackDelay = 0.5f;
    public float jumpAttckChance = 0.7f;
    public AudioClip jumpAttackSound;

    protected float jumpAttackCooldownTimer = 0f;
    protected Vector3 jumpAttackTarget;

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

        health = maxHealth;

        jumpAttackCooldownTimer = jumpAttackCooldown;
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
            if (!isAttacking && !isJumpAttacking && (isChasing || inAttackRange))
            {
                StopAllCombat();
                ResumePatrol();
            }
            else if ((isAttacking || isJumpAttacking) && !playerDeadDuringAttack)
            {
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

        if (jumpAttackCooldownTimer > 0f)
            jumpAttackCooldownTimer -= Time.deltaTime;

        HandleMovementBehavior();
        HandleAICombat();

        if (isAttacking)
        {
            UpdateComboStateMachine();
        }

        UpdateAnimator();
    }

    protected override void HandleAICombat()
    {
        if (dead || isHit || blocking || isJumpAttacking) return;

        if (postHitAttackLockout > 0f)
            return;

        if (IsPlayerDead())
        {
            if (!isAttacking && !isJumpAttacking && inAttackRange)
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

        if (isChasing && jumpAttackCooldownTimer <= 0f &&
            Vector3.Distance(transform.position, player.position) <= jumpAttackRange && Vector3.Distance(transform.position, player.position) >= 5.0f)
        {
            float randomChance = UnityEngine.Random.Range(0f, 1f);
            if (randomChance > jumpAttckChance) 
            {
                StartJumpAttack();
                return;
            }
        }

        if (inAttackRange && !isAttacking && attackCooldownTimer <= 0f)
        {
            StartAttack(1);
        }
    }

    protected void StartJumpAttack()
    {
        if (isHit || dead || isJumpAttacking) return;

        if (IsPlayerDead())
        {
            StopAllCombat();
            ResumePatrol();
            return;
        }

        isJumpAttacking = true;
        Debug.Log("Jump Attacking");
        playerDeadDuringAttack = false;
        jumpAttackCooldownTimer = jumpAttackCooldown;

        jumpAttackTarget = player.position;

        if (navAgent != null)
        {
            navAgent.isStopped = true;
        }

        if (m_Animator != null)
        {
            m_Animator.SetTrigger("JumpAttack");
        }

        if (jumpAttackSound != null)
        {
            AudioSource.PlayClipAtPoint(jumpAttackSound, transform.position);
        }

        StartCoroutine(JumpAttackTimer());
    }

    protected IEnumerator JumpAttackTimer()
    {
        yield return new WaitForSeconds(jumpAttackDelay);

        DoJumpAttackDamage();

        float remainingTime = 1.5f - jumpAttackDelay;
        if (remainingTime > 0)
        {
            yield return new WaitForSeconds(remainingTime);
        }

        EndJumpAttack();
    }

    protected void DoJumpAttackDamage()
    {
        if (playerCharacter == null) return;
        if (IsPlayerDead()) return;

        // Check if player is within damage radius
        float landingDistance = Vector3.Distance(transform.position, player.position);
        if (landingDistance <= attackRange * 1.5f) 
        {
            if (!playerCharacter.IsBlocking())
            {
                playerCharacter.GetHit(jumpAttackDamage);
            }
            else
            {
                PlayBlockingSound();
                MakePlayerFaceEnemy();
            }
        }
    }

    protected void EndJumpAttack()
    {
        isJumpAttacking = false;
        playerDeadDuringAttack = false;

        if (!dead && navAgent != null)
        {
            navAgent.isStopped = false;
        }
        Vector3 forwardDir = transform.forward;
        Vector3 newPos = transform.position + forwardDir * 1f;

        // Optional: smooth the movement (looks more natural)
        StartCoroutine(SmoothForwardStep(newPos, 0.15f));
    }

    private IEnumerator SmoothForwardStep(Vector3 targetPos, float duration)
    {
        Vector3 start = transform.position;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            transform.position = Vector3.Lerp(start, targetPos, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.position = targetPos;
    }

    protected override void UpdateComboStateMachine()
    {
        if (!isAttacking) return;
        if (m_Animator == null) return;

        if (player != null && Vector3.Distance(transform.position, player.position) > attackRange)
        {
            EndCombo();
            if (isChasing && !IsPlayerDead())
            {
                inAttackRange = false;
                if (navAgent != null)
                {
                    navAgent.isStopped = false;
                    navAgent.SetDestination(player.position);
                }
            }
            return;
        }

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

            if (player != null && Vector3.Distance(transform.position, player.position) <= attackRange)
            {
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
            else
            {
                EndCombo();
                if (isChasing && !IsPlayerDead())
                {
                    inAttackRange = false;
                    if (navAgent != null)
                    {
                        navAgent.isStopped = false;
                        navAgent.SetDestination(player.position);
                    }
                }
            }
        }
    }
    protected override void UpdateAnimator()
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
            m_Animator.SetBool("JumpAttack", isJumpAttacking);
        }
    }

    protected override IEnumerator HitRoutine()
    {
        isHit = true;

        if (isJumpAttacking)
        {
            EndJumpAttack();
        }

        if (!isChasing && !inAttackRange && player != null)
        {
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
            m_Animator.SetBool("JumpAttack", false);
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

    public void OnJumpAttackAnimationEnd()
    {
        if (isJumpAttacking)
        {
            EndJumpAttack();
        }
    }
}