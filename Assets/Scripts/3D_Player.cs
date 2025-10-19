using System;
using UnityEngine;

[RequireComponent(typeof(Animator))]
public class ThreeD_Character : MonoBehaviour
{
    [Header("Movement")]
    public float Speed = 2.0f;                // base walk speed
    public float sprintMultiplier = 2.0f;     // speed multiplier while sprinting
    public float jumpHeight = 1.5f;           // how high the character jumps
    public float gravity = 9.81f;             // gravity strength

    [Header("Attack / Combo (code-driven)")]
    public int maxCombo = 4;                  // number of combo attacks (1..4)
    [Tooltip("Normalized time (0..1) where chaining can begin")]
    public float comboWindowStart = 0.3f;
    [Tooltip("Normalized time (0..1) where chaining must finish)")]
    public float comboWindowEnd = 0.9f;
    public float transitionDuration = 0.08f;  // animator crossfade duration
    public float attackRayDistance = 15f;     // raycast distance for attack hit detection
    public LayerMask attackHitMask = ~0;      // layer mask for raycast (default: everything)

    private bool isAttacking = false;         // overall attacking state (blocks movement)
    private bool queuedCombo = false;         // player requested next attack (pressed Fire1 while attacking)
    private int comboIndex = 0;               // current attack index (1..maxCombo)
    private float lastStateNormalizedTime = 0f;
    private float sprintAxis = 0f;

    private Animator m_Animator;

    [Header("Mouse Look")]
    public Transform cameraTransform;         // assign your Main Camera here
    public float mouseSensitivity = 2.0f;     // sensitivity for mouse rotation
    public float verticalLookUpLimit = 50f;
    public float verticalLookDownLimit = 50f;

    // CharacterController related
    private CharacterController controller;
    private Vector3 moveVelocity = Vector3.zero;
    private float verticalVelocity = 0f;
    private float cameraPitch = 0f; // for up/down look
    float xRotation = 0f;

    void Start()
    {
        m_Animator = GetComponent<Animator>();

        controller = GetComponent<CharacterController>();
        if (controller == null)
        {
            controller = gameObject.AddComponent<CharacterController>();
            controller.height = 2.0f;
            controller.radius = 0.4f;
            controller.center = new Vector3(0f, 1.0f, 0f);
        }

        // Lock/hide cursor for play (change behavior for menus)
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        HandleMouseLook();
        HandleMovementInput();
        HandleAttackInput_InputCheck(); // check inputs & queue/start combos
        UpdateComboStateMachine();      // manage combo progression
        UpdateAnimator();
    }

    // ---------- Mouse Look ----------
    private void HandleMouseLook()
    {
        if (cameraTransform == null) return;

        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        // Rotate player left/right (Y-axis)
        transform.Rotate(Vector3.up * mouseX);

        // Rotate camera up/down (X-axis)
        cameraPitch -= mouseY;
        cameraPitch = Mathf.Clamp(cameraPitch, -verticalLookDownLimit, verticalLookUpLimit);
        cameraTransform.localRotation = Quaternion.Euler(cameraPitch, 0f, 0f);
    }

    // ---------- Movement (WASD + Sprint + Jump) ----------
    private void HandleMovementInput()
    {
        float inputZ = Input.GetAxis("Vertical");
        float inputX = Input.GetAxis("Horizontal");
        sprintAxis = Input.GetAxis("Sprint"); 

        float currentSpeed = Speed * (1f + (sprintAxis * (sprintMultiplier - 1f)));

        // If attacking, disable movement entirely (matching previous behaviour)
        if (isAttacking)
        {
            inputX = 0f;
            inputZ = 0f;
        }

        Vector3 localMove = transform.forward * inputZ + transform.right * inputX;
        localMove = localMove.normalized * currentSpeed;

        if (controller.isGrounded)
        {
            if (verticalVelocity < 0f) verticalVelocity = -1f;

            if (Input.GetButtonDown("Jump") && !isAttacking)
            {
                verticalVelocity = Mathf.Sqrt(2f * gravity * Mathf.Max(0.1f, jumpHeight));
            }
        }
        else
        {
            verticalVelocity -= gravity * Time.deltaTime;
        }

        moveVelocity = localMove + Vector3.up * verticalVelocity;
        controller.Move(moveVelocity * Time.deltaTime);
    }

    // ---------- Input: Attack press handling ----------
    private void HandleAttackInput_InputCheck()
    {
        // Player pressed attack
        if (Input.GetButtonDown("Fire1"))
        {
            OnAttackPressed();
        }
    }

    // Called when the player presses Fire1
    private void OnAttackPressed()
    {
        if (!isAttacking)
        {
            // Start first attack
            comboIndex = 1;
            queuedCombo = false;
            StartAttack(comboIndex);
            isAttacking = true;
        }
        else
        {
            // Already attacking -> queue the next attack request
            queuedCombo = true;
        }
    }

    // ---------- Combo state machine (called every frame) ----------
    private void UpdateComboStateMachine()
    {
        if (!isAttacking) return;

        // Get animator state info (layer 0)
        AnimatorStateInfo state = m_Animator.GetCurrentAnimatorStateInfo(0);

        // Safety: only run logic if in an Attack state (named AttackX)
        string expectedStateName = "Attack" + comboIndex;
        bool inExpectedAttackState = state.IsName(expectedStateName);

        // normalized time within current clip (0..1)
        float normalizedTime = state.normalizedTime % 1f;

        // If player queued a combo and we're inside the valid combo window -> advance
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

        // If the animation has finished (normalized >= 1) or passed the comboWindowEnd without a queued input,
        // then we should either end the combo or ignore queued input if it's too late.
        // We treat normalizedTime >= 1f as clip end (note: for non-looping clips normalizedTime will reach >=1).
        if (normalizedTime >= 1f || normalizedTime > comboWindowEnd)
        {
            // If there is a queuedCombo but we missed the window, drop it
            if (queuedCombo && !(normalizedTime >= comboWindowStart && normalizedTime <= comboWindowEnd))
            {
                queuedCombo = false;
            }

            // If we're at the last attack or nothing queued, end combo
            if (!queuedCombo || comboIndex >= maxCombo)
            {
                EndCombo();
            }
        }

        lastStateNormalizedTime = normalizedTime;
    }

    // Start a specific attack index (plays animation and does immediate raycast hit)
    private void StartAttack(int index)
    {
        if (index < 1 || index > maxCombo) return;

        string stateName = "Attack" + index; // must match animator state name exactly
        m_Animator.CrossFade(stateName, transitionDuration, 0);

        // optional: perform raycast immediately to detect hit for this attack
        // (you can replace this with an animation event for more precise timing)
        DoAttackRaycast();

        // ensure flags
        isAttacking = true;
        queuedCombo = false;
    }

    // Raycast logic for hitting something in front of the player
    private void DoAttackRaycast()
    {
        Vector3 origin = transform.position + Vector3.up * 1f;
        Vector3 dir = transform.forward;

        if (Physics.Raycast(origin, dir, out RaycastHit hit, attackRayDistance, attackHitMask))
        {
            GameObject root = hit.collider.transform.root.gameObject;
            Debug.Log("[Attack] Ray hit: " + root.name);

            if (hit.collider.gameObject != this.gameObject)
            {
                // Default behaviour: destroy the hit object (change as needed)
                Destroy(hit.collider.gameObject);
            }
        }

        Debug.DrawRay(origin, dir * attackRayDistance, Color.yellow, 0.5f);
    }

    // End combo & reset state
    private void EndCombo()
    {
        isAttacking = false;
        queuedCombo = false;
        comboIndex = 0;
        // Make sure animator knows we're not attacking (if you rely on a boolean)
        m_Animator.SetBool("Attack", false);
    }

    // Optional hook: call from animation event at exact combo window
    // If you add an animation event named OnComboWindowOpen, you can chain precisely there:
    public void OnComboWindowOpen()
    {
        if (queuedCombo && comboIndex < maxCombo)
        {
            comboIndex++;
            queuedCombo = false;
            StartAttack(comboIndex);
        }
    }

    // ---------- Animator updates ----------
    private void UpdateAnimator()
    {
        Vector3 planarVelocity = new Vector3(controller.velocity.x, 0f, controller.velocity.z);
        float speedValue = planarVelocity.magnitude;

        m_Animator.SetFloat("Speed", speedValue);
        m_Animator.SetBool("Attack", isAttacking);
        m_Animator.SetBool("Grounded", controller.isGrounded);
        m_Animator.SetFloat("VerticalVelocity", verticalVelocity);
        m_Animator.SetFloat("Sprint", sprintAxis);
    }
}
