using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(NavMeshAgent))]
public class ThreeD_Patrolling_Enemy : ThreeD_Enemy
{
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
                Debug.Log("[Enemy] Player is dead, stopping all combat and returning to patrol");
                StopAllCombat();
                ResumePatrol();
            }
            else if (isAttacking && !playerDeadDuringAttack)
            {
                Debug.Log("[Enemy] Player died during attack, will finish current attack then stop");
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
        HandleMovementBehavior();
        HandleAICombat();
        UpdateComboStateMachine();
        UpdateAnimator();
    }

}