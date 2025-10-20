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
    public Transform[] patrolPoints;             // inspector-friendly, can be empty -> auto-collect
    private Vector3[] patrolPositions;           // stationary world-space positions (snapshotted)
    public float moveSpeed = 1.5f;
    public float sprintMultiplier = 2.0f;
    public float waitTimeAtPoint = 2f;
    public float pointReachThreshold = 0.5f;
    private int currentPatrolIndex = 0;
    private float waitTimer = 0f;
    private bool waiting = false;

    [Header("Detection / Chase")]
    public float detectionRange = 10f;    // start chasing when player within this distance and visible
    public float chaseLoseDistance = 15f; // if player goes beyond this while chasing, enemy gives up
    public float viewAngle = 75f;         // full cone angle (degrees) the enemy can see in front
    public float attackRange = 2f;        // when within this distance, stop and prepare to attack
    public LayerMask obstructionMask = ~0; // used for LOS raycast (default = everything)
    private Transform player;
    private bool isChasing = false;
    private bool inAttackRange = false;

    [Header("Attack / Combo (kept for compatibility)")]
    public int maxCombo = 4;
    public float comboWindowStart = 0.75f;
    public float comboWindowEnd = 1.25f;
    public float transitionDuration = 0.08f;
    public float attackRayDistance = 15f;
    public LayerMask attackHitMask = ~0;

    private bool isAttacking = false;
    private bool queuedCombo = false;
    private int comboIndex = 0;
    private float sprintAxis = 0f;

    [Header("Blocking")]
    private bool blocking = false;
    private bool queuedBlock = false;

    [Header("Hit")]
    private bool isHit = false;

    private NavMeshAgent navAgent;
    private Animator m_Animator;

    // Debug / watchdog
    private float stuckTimer = 0f;
    public float stuckTimeout = 2.0f; // seconds before we consider agent stuck
    public float stuckVelocityThreshold = 0.05f; // magnitude below which agent considered stalled

    void Start()
    {
        m_Animator = GetComponent<Animator>();
        navAgent = GetComponent<NavMeshAgent>();

        // Configure agent defaults
        navAgent.updatePosition = true;
        navAgent.updateRotation = false; // rotate manually so the enemy can face the player
        navAgent.speed = moveSpeed;
        navAgent.stoppingDistance = attackRange;
        navAgent.autoBraking = false; // smoother continuous movement between points

        // Auto-fetch patrol Transforms if none assigned
        if (patrolPoints == null || patrolPoints.Length == 0)
        {
            // first try a child named "PatrolPoints"
            Transform container = transform.Find("PatrolPoints");

            // fallback to a root-level object named "PatrolPoints"
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

        Debug.Log("[Enemy] patrolPoints found: " + (patrolPoints != null ? patrolPoints.Length : 0));

        // Snapshot world positions so they remain stationary even if Transforms move
        if (patrolPoints != null && patrolPoints.Length > 0)
        {
            patrolPositions = new Vector3[patrolPoints.Length];
            for (int i = 0; i < patrolPoints.Length; i++)
            {
                if (patrolPoints[i] != null)
                    patrolPositions[i] = patrolPoints[i].position;
                else
                    patrolPositions[i] = transform.position; // fallback
            }
        }

        // find player by tag
        var pgo = GameObject.FindWithTag("Player");
        if (pgo != null) player = pgo.transform;

        // If no patrol points found, stay put
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

        // ensure chaseLoseDistance is at least detectionRange to avoid immediate drop
        if (chaseLoseDistance < detectionRange) chaseLoseDistance = detectionRange * 1.5f;
    }

    void Update()
    {
        if (!isHit)
            DetectPlayer();

        HandleMovementBehavior();

        HandleBlockInput_AI();
        UpdateComboStateMachine();
        UpdateAnimator();
    }

    #region Movement / Patrol / Chase

    private void HandleMovementBehavior()
    {
        if (isHit)
        {
            if (navAgent != null) navAgent.isStopped = true;
            sprintAxis = 0f;
            return;
        }

        if (inAttackRange)
        {
            // stop movement and face the player
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
            float dist = Vector3.Distance(transform.position, player.position);

            // Give up chase if the player went too far
            if (dist > chaseLoseDistance)
            {
                Debug.Log("[Enemy] Giving up chase: player too far (" + dist + " > " + chaseLoseDistance + ")");
                isChasing = false;
                inAttackRange = false;
                ResumePatrol();
                return;
            }

            // If player is both outside view cone AND not visible (occluded), give up chase as well
            bool inFOV = IsInViewAngle(player);
            bool los = HasLineOfSight(player);
            if (!inFOV && !los)
            {
                Debug.Log("[Enemy] Giving up chase: lost sight and outside FOV");
                isChasing = false;
                inAttackRange = false;
                ResumePatrol();
                return;
            }

            // If within attackRange, stop and set inAttackRange
            if (dist <= attackRange)
            {
                inAttackRange = true;
                sprintAxis = 0f;
                if (navAgent != null) navAgent.isStopped = true;

                // always face player when in attack range
                Vector3 dirToPlayer = (player.position - transform.position);
                dirToPlayer.y = 0f;
                if (dirToPlayer.sqrMagnitude > 0.0001f) RotateTowards(dirToPlayer.normalized);

                return;
            }

            // chase: sprint
            sprintAxis = 1f;
            if (navAgent != null)
            {
                navAgent.isStopped = false;
                navAgent.speed = moveSpeed * sprintMultiplier;
                navAgent.SetDestination(player.position);
            }

            // rotate to face player while chasing (makes the enemy look at player even while moving)
            Vector3 chaseDir = (player.position - transform.position);
            chaseDir.y = 0f;
            if (chaseDir.sqrMagnitude > 0.0001f) RotateTowards(chaseDir.normalized);

            // reset stuck timer while chasing
            stuckTimer = 0f;

            return;
        }

        // PATROL behavior
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

        // Debug state
        if (Time.frameCount % 30 == 0) // reduce spam: print every 30 frames
        {
            Debug.Log($"[Enemy][Patrol] idx={currentPatrolIndex} distToTarget={dist:F2} waiting={waiting} agentStopped={(navAgent != null ? navAgent.isStopped : false)} " +
                      $"hasPath={(navAgent != null ? navAgent.hasPath : false)} pathPending={(navAgent != null ? navAgent.pathPending : false)} " +
                      $"remainingDistance={(navAgent != null ? navAgent.remainingDistance : -1f):F2} pathStatus={(navAgent != null ? navAgent.pathStatus.ToString() : "NA")}");
        }

        // If reached point (use world-space distance check instead of navAgent.remainingDistance)
        if (dist <= pointReachThreshold && !waiting)
        {
            // mark waiting and stop agent
            waiting = true;
            waitTimer = 0f;
            if (navAgent != null) navAgent.isStopped = true;
            sprintAxis = 0f;
            Debug.Log("[Enemy][Patrol] Arrived at point " + currentPatrolIndex + " | starting wait");
            return;
        }

        if (waiting)
        {
            // while waiting we keep agent stopped
            if (navAgent != null) navAgent.isStopped = true;

            waitTimer += Time.deltaTime;
            if (waitTimer >= waitTimeAtPoint)
            {
                // move to next point and resume agent
                waiting = false;
                waitTimer = 0f;
                currentPatrolIndex = (currentPatrolIndex + 1) % patrolPositions.Length;
                if (navAgent != null)
                {
                    navAgent.isStopped = false;
                    navAgent.speed = moveSpeed;
                    navAgent.SetDestination(patrolPositions[currentPatrolIndex]);
                }
                Debug.Log("[Enemy][Patrol] Wait finished - moving to point " + currentPatrolIndex);
            }
            sprintAxis = 0f;

            // still ensure stuck timer is reset while waiting
            stuckTimer = 0f;
            return;
        }

        // Move toward patrol point
        sprintAxis = 0f;
        if (navAgent != null)
        {
            navAgent.isStopped = false;
            navAgent.speed = moveSpeed;
            Vector3 dest = patrolPositions[currentPatrolIndex];

            // set destination if it's meaningfully different
            if (Vector3.Distance(navAgent.destination, dest) > 0.05f)
            {
                navAgent.SetDestination(dest);
                Debug.Log("[Enemy][Patrol] SetDestination -> idx:" + currentPatrolIndex + " dest:" + dest);
            }
        }

        // rotate roughly toward movement direction so patrol looks natural
        if (navAgent != null)
        {
            Vector3 vel = navAgent.velocity;
            vel.y = 0f;
            if (vel.sqrMagnitude > 0.001f)
                RotateTowards(vel.normalized);
        }

        // WATCHDOG: if agent velocity is near zero while it should be moving, increment stuckTimer
        if (navAgent != null)
        {
            float planarSpeed = new Vector3(navAgent.velocity.x, 0f, navAgent.velocity.z).magnitude;

            if (planarSpeed < stuckVelocityThreshold && !waiting && !isChasing && !inAttackRange)
                stuckTimer += Time.deltaTime;
            else
                stuckTimer = 0f;

            if (stuckTimer > stuckTimeout)
            {
                Debug.LogWarning("[Enemy][Patrol] Agent appears stuck. stuckTimer=" + stuckTimer + ". Forcing destination/reset.");
                stuckTimer = 0f;

                // try forcing destination again; if still stuck, advance index
                if (navAgent != null)
                {
                    navAgent.ResetPath();
                    navAgent.SetDestination(patrolPositions[currentPatrolIndex]);
                    navAgent.isStopped = false;
                }

                // if still no movement next update, advance to next point to avoid deadlock
                // (this will be visible in logs)
                currentPatrolIndex = (currentPatrolIndex + 1) % patrolPositions.Length;
                if (navAgent != null)
                {
                    navAgent.SetDestination(patrolPositions[currentPatrolIndex]);
                    Debug.Log("[Enemy][Patrol] Forced advance to index " + currentPatrolIndex);
                }
            }
        }
    }

    private void ResumePatrol()
    {
        if (patrolPositions == null || patrolPositions.Length == 0) return;
        isChasing = false;
        inAttackRange = false;
        if (navAgent != null)
        {
            navAgent.isStopped = false;
            navAgent.speed = moveSpeed;
            navAgent.SetDestination(patrolPositions[currentPatrolIndex]);
        }
    }

    #endregion

    #region Detection / LOS / FOV

    private void DetectPlayer()
    {
        if (player == null)
        {
            var pgo = GameObject.FindWithTag("Player");
            if (pgo != null) player = pgo.transform;
            if (player == null) return;
        }

        if (isHit) return;

        float dist = Vector3.Distance(transform.position, player.position);

        // detect only if inside detectionRange, inside view cone, and has line of sight
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
            Debug.Log("[Enemy] Detected player - starting chase");
        }
    }

    // check view cone (uses full cone angle viewAngle)
    private bool IsInViewAngle(Transform target)
    {
        Vector3 toTarget = (target.position - transform.position);
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude <= 0.0001f) return true; // overlapping positions -> treat as visible
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
            // if the raycast hit the target (or a child), we have LOS
            if (hit.collider != null && (hit.collider.transform.IsChildOf(target) || hit.collider.gameObject.CompareTag("Player")))
                return true;

            // blocked by something else
            return false;
        }

        // nothing hit -> LOS
        return true;
    }

    #endregion

    #region Attack / Combo placeholders

    private void HandleAttackInput()
    {
        // Enemy attack will be AI driven later. For now, we disallow manual attack while hit or blocking.
        if (blocking || isHit) return;
    }

    public void StartAttack(int index)
    {
        if (index < 1 || index > maxCombo) return;
        if (isHit) return;

        comboIndex = index;
        queuedCombo = false;
        isAttacking = true;

        if (m_Animator != null)
        {
            string stateName = "Attack" + index;
            m_Animator.CrossFade(stateName, transitionDuration, 0);
        }

        DoAttackRaycast();
    }

    private void DoAttackRaycast()
    {
        Vector3 origin = transform.position + Vector3.up * 1f;
        Vector3 dir = transform.forward;

        if (Physics.Raycast(origin, dir, out RaycastHit hit, attackRayDistance, attackHitMask))
        {
            GameObject root = hit.collider.transform.root.gameObject;
            if (hit.collider.gameObject != this.gameObject)
                Destroy(hit.collider.gameObject);
        }

        Debug.DrawRay(origin, dir * attackRayDistance, Color.yellow, 0.5f);
    }

    private void UpdateComboStateMachine()
    {
        if (!isAttacking)
        {
            if (queuedBlock && !isHit)
            {
                blocking = true;
                queuedBlock = false;
                if (m_Animator != null) m_Animator.SetBool("Block", true);
            }
            return;
        }

        if (m_Animator == null) return;
        AnimatorStateInfo state = m_Animator.GetCurrentAnimatorStateInfo(0);
        float normalizedTime = state.normalizedTime % 1f;

        if (queuedCombo && normalizedTime >= comboWindowStart && normalizedTime <= comboWindowEnd)
        {
            if (comboIndex < maxCombo)
            {
                comboIndex++;
                queuedCombo = false;
                StartAttack(comboIndex);
                return;
            }
        }

        if (normalizedTime >= 1f || normalizedTime > comboWindowEnd)
        {
            if (queuedCombo && !(normalizedTime >= comboWindowStart && normalizedTime <= comboWindowEnd))
                queuedCombo = false;

            if (!queuedCombo || comboIndex >= maxCombo)
                EndCombo();
        }
    }

    private void EndCombo()
    {
        isAttacking = false;
        queuedCombo = false;
        comboIndex = 0;
        if (m_Animator != null) m_Animator.SetBool("Attack", false);
    }

    #endregion

    #region Blocking (AI hooks)

    private void HandleBlockInput_AI()
    {
        if (isHit) return;
        if (m_Animator != null) m_Animator.SetBool("Block", blocking);
    }

    #endregion

    #region Animator

    private void UpdateAnimator()
    {
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
        }
    }

    #endregion

    #region Hit / Stun

    public void GetHit()
    {
        if (isHit) return;
        StartCoroutine(HitRoutine());
    }

    private IEnumerator HitRoutine()
    {
        isHit = true;

        isAttacking = false;
        queuedCombo = false;

        if (m_Animator != null)
        {
            m_Animator.SetBool("Attack", false);
            m_Animator.SetBool("Hit", true);
        }

        if (navAgent != null) navAgent.isStopped = true;

        yield return new WaitForSeconds(0.25f);

        isHit = false;

        if (m_Animator != null) m_Animator.SetBool("Hit", false);

        if (isChasing && player != null && IsInViewAngle(player) && HasLineOfSight(player) &&
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
        else
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
        // draw patrol points + lines + threshold
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

        // highlight current target
        if (patrolPositions != null && patrolPositions.Length > 0)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(patrolPositions[currentPatrolIndex], pointReachThreshold);
        }

        // draw view cone
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.15f);
        Vector3 fwd = transform.forward;
        Quaternion leftQ = Quaternion.Euler(0f, -viewAngle * 0.5f, 0f);
        Quaternion rightQ = Quaternion.Euler(0f, viewAngle * 0.5f, 0f);
        Gizmos.DrawRay(transform.position + Vector3.up * 0.25f, leftQ * fwd * detectionRange);
        Gizmos.DrawRay(transform.position + Vector3.up * 0.25f, rightQ * fwd * detectionRange);
    }

    #endregion
}
