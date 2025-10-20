using System;
using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Animator))]
public class ThreeD_Character : MonoBehaviour
{
    [Header("Movement")]
    public float Speed = 2.0f;
    float sprintMultiplier = 3.0f;
    public float jumpHeight = 1.5f;
    float gravity = 9.81f;

    [Header("Attack / Combo")]
    public int maxCombo = 4;
    float comboWindowStart = 0.85f;
    float comboWindowEnd = 0.99f;
    float transitionDuration = 0.08f;
    public float attackRayDistance = 15f;
    public LayerMask attackHitMask = ~0;

    private bool isAttacking = false;
    private bool queuedCombo = false;
    private int comboIndex = 0;
    private float sprintAxis = 0f;

    [Header("Camera / Orbit")]
    public Transform cameraTransform;
    Vector3 cameraOffset = new Vector3(-0.5f, 1.5f, 0.5f);
    public float mouseSensitivity = 3.0f;
    float verticalLookUpLimit = 30f;
    float verticalLookDownLimit = 10f;

    private Vector3 initialCameraOffset;
    private float cameraPitch;
    private float cameraYaw;

    [Header("Blocking")]
    private bool blocking = false;
    private bool queuedBlock = false;

    [Header("Hit")]
    private bool isHit = false;

    private Animator m_Animator;
    private CharacterController controller;
    private Vector3 moveVelocity = Vector3.zero;
    private float verticalVelocity = 0f;
    private float directionValue = 0f;

    private void Awake()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
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

        if (cameraTransform != null)
        {
            initialCameraOffset = cameraTransform.position - transform.position;

            Vector3 angles = cameraTransform.eulerAngles;
            cameraYaw = angles.y;
            cameraPitch = angles.x;
        }
    }

    void Update()
    {
        HandleMouseLook();
        HandleMovementInput();
        HandleBlockInput();
        HandleAttackInput();
        UpdateComboStateMachine();
        UpdateAnimator();
        UpdateCameraPosition();
    }

    private void HandleMouseLook()
    {
        if (cameraTransform == null) return;

        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        cameraYaw += mouseX;
        cameraPitch -= mouseY;
        cameraPitch = Mathf.Clamp(cameraPitch, -verticalLookDownLimit, verticalLookUpLimit);
    }

    private void UpdateCameraPosition()
    {
        if (cameraTransform == null) return;

        float distance = initialCameraOffset.magnitude;

        Quaternion rotation = Quaternion.Euler(cameraPitch, cameraYaw, 0f);

        Vector3 baseOffset = new Vector3(0f, cameraOffset.y, -distance);

        Vector3 sidewaysOffset = new Vector3(cameraOffset.x, 0f, 0f);

        Vector3 finalOffset = rotation * (baseOffset + sidewaysOffset);

        cameraTransform.position = transform.position + finalOffset;

        cameraTransform.LookAt(transform.position + new Vector3(0f, cameraOffset.y, 0f));
    }


    private void HandleMovementInput()
    {
        if (isHit) return;

        float inputZ = Input.GetAxisRaw("Vertical");
        float inputX = Input.GetAxisRaw("Horizontal");
        sprintAxis = Input.GetAxis("Sprint");

        float currentSpeed = Speed * (1f + (sprintAxis * (sprintMultiplier - 1f)));

        if (isAttacking || blocking)
        {
            inputX = 0f;
            inputZ = 0f;
        }

        Vector3 cameraForward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
        Vector3 cameraRight = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized;

        Vector3 moveDir = cameraForward * inputZ + cameraRight * inputX;
        moveDir = moveDir.normalized;

        Vector3 localMove = moveDir * currentSpeed;

        if (controller.isGrounded)
        {
            if (verticalVelocity < 0f) verticalVelocity = -1f;
            if (Input.GetButtonDown("Jump") && !isAttacking && !blocking)
                verticalVelocity = Mathf.Sqrt(2f * gravity * Mathf.Max(0.1f, jumpHeight));
        }
        else
        {
            verticalVelocity -= gravity * Time.deltaTime;
        }

        moveVelocity = localMove + Vector3.up * verticalVelocity;
        controller.Move(moveVelocity * Time.deltaTime);

        if (moveDir.magnitude > 0.1f)
        {
            transform.forward = Vector3.Slerp(transform.forward, moveDir, Time.deltaTime * 10f);
        }

        directionValue = inputX;
    }

    private void HandleAttackInput()
    {
        if (blocking || isHit || !controller.isGrounded) return;
        if (Input.GetButtonDown("Fire1"))
        {
            if (!isAttacking)
            {
                comboIndex = 1;
                queuedCombo = false;
                StartAttack(comboIndex);
                isAttacking = true;
            }
            else
            {
                queuedCombo = true;
            }
        }
    }

    private void HandleBlockInput()
    {
        if (isHit || !controller.isGrounded) return;
        bool blockPressed = Input.GetButton("Block");

        if (blockPressed)
        {
            if (isAttacking)
            {
                queuedBlock = true;
            }
            else if (!blocking)
            {
                blocking = true;
                isAttacking = false;
            }
        }
        else
        {
            blocking = false;
            queuedBlock = false;
        }

        m_Animator.SetBool("Block", blocking);
    }

    private void UpdateComboStateMachine()
    {
        if (!isAttacking)
        {
            if (queuedBlock && !isHit)
            {
                blocking = true;
                queuedBlock = false;
                m_Animator.SetBool("Block", true);
            }
            return;
        }

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

    private void StartAttack(int index)
    {
        if (index < 1 || index > maxCombo) return;

        string stateName = "Attack" + index;
        m_Animator.CrossFade(stateName, transitionDuration, 0);

        DoAttackRaycast();
        isAttacking = true;
        queuedCombo = false;
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

    private void EndCombo()
    {
        isAttacking = false;
        queuedCombo = false;
        comboIndex = 0;
        m_Animator.SetBool("Attack", false);
    }

    public void OnComboWindowOpen()
    {
        if (queuedCombo && comboIndex < maxCombo)
        {
            comboIndex++;
            queuedCombo = false;
            StartAttack(comboIndex);
        }
    }

    private void UpdateAnimator()
    {
        Vector3 planarVelocity = new Vector3(controller.velocity.x, 0f, controller.velocity.z);

        float forward = Vector3.Dot(planarVelocity, transform.forward);
        float right = Vector3.Dot(planarVelocity, transform.right);

        float targetSpeed = Mathf.Abs(forward) > 0.01f ? forward : 0f;
        float targetDirection = Mathf.Abs(right) > 0.01f ? right / Speed : 0f;

        float smoothTime = 0.1f;
        m_Animator.SetFloat("Speed", targetSpeed, smoothTime, Time.deltaTime);
        m_Animator.SetFloat("Direction", targetDirection, smoothTime, Time.deltaTime);
        m_Animator.SetBool("Attack", isAttacking);
        m_Animator.SetBool("Grounded", controller.isGrounded);
        m_Animator.SetFloat("Sprint", sprintAxis);
        m_Animator.SetBool("Block", blocking);
        m_Animator.SetBool("Hit", isHit);
    }

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

        m_Animator.SetBool("Attack", false);
        m_Animator.SetBool("Hit", true);

        yield return new WaitForSeconds(0.25f);

        isHit = false;
        m_Animator.SetBool("Hit", false);
    }
}
