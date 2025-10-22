using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
public class FloatingEnemy : MonoBehaviour
{
    [Header("Path")]
    [Tooltip("Optional: assign a Transform in the inspector for the other endpoint. If left empty, an 'EndPoint' child will be used or a default offset will be created.")]
    public Transform pointB = null;

    [Header("Movement")]
    public float speed = 2f;
    [Tooltip("If > 0 the enemy will pause this many seconds at each endpoint.")]
    public float waitAtPoint = 0.5f;
    [Tooltip("Use smooth damped turning (true) or instant direction change (false).")]
    public bool smoothTurning = true;
    [Tooltip("How quickly the sprite orientation smooths when smoothTurning=true.")]
    public float turningLerp = 12f;

    // internal (captured at start and kept fixed)
    private Vector2 a;          // start point (captured at Start)
    private Vector2 b;          // end point (captured at Start)
    private Vector2 target;
    private Rigidbody2D rb;
    private Animator animator;
    private bool movingToB = true;
    private float waitTimer = 0f;
    private float dirSign = 1f;
    private Vector3 desiredScale;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody2D>();

        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.simulated = true;

        animator = GetComponent<Animator>();
        desiredScale = transform.localScale;
    }

    void Start()
    {
        // capture fixed endpoints at start and never change them later
        a = transform.position; // starting position frozen as point A

        // Determine point B:
        if (pointB != null)
        {
            b = pointB.position;
        }
        else
        {
            Transform endPointChild = FindEndPointChild();
            if (endPointChild != null)
            {
                b = endPointChild.position;
            }
            else
            {
                // fallback: a small default offset to the right
                b = a + Vector2.right * 3f;
                Debug.LogWarning("[FloatingEnemy] No pointB assigned and no 'EndPoint' child found. Using default offset.");
            }
        }

        // Set initial target and state
        target = b;
        movingToB = true;
        waitTimer = 0f;
        desiredScale = transform.localScale;

        // ensure the transform starts exactly at point A to avoid immediate toggle
        transform.position = a;

        // ensure animator idle at start
        SetMovingState(false);
    }

    void FixedUpdate()
    {
        if (waitTimer > 0f)
        {
            waitTimer -= Time.fixedDeltaTime;
            rb.linearVelocity = Vector2.zero;
            SetMovingState(false); // idle while waiting
            return;
        }

        Vector2 current = rb.position;
        Vector2 toTarget = target - current;
        float dist = toTarget.magnitude;

        if (dist <= 0.05f) // reached target
        {
            ToggleTarget();
            SetMovingState(false);
            return;
        }

        Vector2 direction = toTarget.normalized;
        Vector2 velocity = direction * speed;
        rb.MovePosition(current + velocity * Time.fixedDeltaTime);

        SetMovingState(true); // moving between points

        // sprite flipping smoothing
        float newDirSign = direction.x >= 0f ? 1f : -1f;
        if (smoothTurning)
        {
            dirSign = Mathf.Lerp(dirSign, newDirSign, Time.fixedDeltaTime * turningLerp);
        }
        else
        {
            dirSign = newDirSign;
        }

        // Apply stable scale flipping (preserve original y/z)
        Vector3 newScale = desiredScale;
        newScale.x = Mathf.Abs(newScale.x) * Mathf.Sign(dirSign);
        transform.localScale = newScale;
    }

    private Transform FindEndPointChild()
    {
        foreach (Transform child in transform)
        {
            if (child.name == "EndPoint")
                return child;
        }
        return null;
    }

    private void ToggleTarget()
    {
        movingToB = !movingToB;
        target = movingToB ? b : a;
        waitTimer = waitAtPoint;
    }

    /// <summary>
    /// Sets animator boolean "Moving" if available.
    /// </summary>
    private void SetMovingState(bool moving)
    {
        if (animator != null)
        {
            animator.SetBool("Moving", moving);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Vector2 gizmoA = Application.isPlaying ? a : (Vector2)transform.position;
        Vector2 gizmoB;

        if (Application.isPlaying)
        {
            gizmoB = b;
        }
        else
        {
            if (pointB != null) gizmoB = pointB.position;
            else
            {
                Transform endPointChild = null;
                foreach (Transform child in transform)
                    if (child.name == "EndPoint") { endPointChild = child; break; }

                gizmoB = endPointChild != null ? (Vector2)endPointChild.position : (Vector2)transform.position + Vector2.right * 3f;
            }
        }

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(gizmoA, 0.1f);
        Gizmos.DrawWireSphere(gizmoB, 0.1f);
        Gizmos.DrawLine(gizmoA, gizmoB);

        if (Application.isPlaying)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(target, 0.15f);
        }
    }
}
