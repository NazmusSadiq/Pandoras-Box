using UnityEngine;

public class AnimationLocationFix : StateMachineBehaviour
{
    [Header("Correction Settings")]
    [Tooltip("Total backward distance to compensate for forward drift during the animation.")]
    public float forwardCompensation = 0.5f;

    [Tooltip("Normalized point (0–1) in the animation after which the fix starts.")]
    [Range(0f, 1f)]
    public float startPercentage = 0.3f;

    [Tooltip("Normalized point (0–1) in the animation after which the fix stops.")]
    [Range(0f, 1f)]
    public float endPercentage = 0.8f;

    private Transform playerTransform;
    private Vector3 originalPosition;
    private float appliedAmount;

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        // Always use player (root) transform, never the animator itself
        playerTransform = animator.transform.root;

        if (playerTransform == null)
        {
            Debug.LogWarning("[AnimationLocationFix] Player transform not found!");
            return;
        }

        originalPosition = playerTransform.position;
        appliedAmount = 0f;
    }

    public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (playerTransform == null) return;

        float normalizedTime = Mathf.Clamp01(stateInfo.normalizedTime);

        // Only apply correction between start and end percentages
        if (normalizedTime < startPercentage || normalizedTime > endPercentage)
            return;

        // Normalize progress in correction window
        float t = Mathf.InverseLerp(startPercentage, endPercentage, normalizedTime);

        // Calculate total intended compensation so far
        float totalSoFar = forwardCompensation * t;

        // Apply incremental change per frame
        float delta = totalSoFar - appliedAmount;

        if (Mathf.Abs(delta) > 0.0001f)
        {
            playerTransform.position -= playerTransform.forward * delta;
            appliedAmount += delta;
        }
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (playerTransform == null) return;

        // At end of animation, move the player slightly forward
        playerTransform.position = originalPosition;
    }
}
