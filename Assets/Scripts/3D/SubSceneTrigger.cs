using UnityEngine;

[RequireComponent(typeof(Collider))]
public class SubSceneTrigger : MonoBehaviour
{
    [Tooltip("Name of the subscene to load (must match the scene name in Build Settings).")]
    public string subSceneName;

    [Tooltip("Optional: require the GameObject that collides to have this tag (leave empty for no tag-check).")]
    public string requiredTag = "Player";

    private Collider myCollider;
    public bool hasCompleted = false;

    private void Start()
    {
        myCollider = GetComponent<Collider>();
        if (myCollider == null)
        {
            Debug.LogError("SubSceneTrigger requires a Collider. Attach a collider to this object.");
            return;
        }

        // Ensure it's a trigger collider
        if (!myCollider.isTrigger)
        {
            Debug.LogWarning("Collider is not marked as Trigger. Marking it as trigger now.");
            myCollider.isTrigger = true;
        }

        if (string.IsNullOrEmpty(subSceneName))
        {
            Debug.LogWarning("SubSceneTrigger: subSceneName not set in inspector.");
        }
    }

    private void Update()
    {
        if (hasCompleted)
        {
            Destroy(transform.root.gameObject);
        }
    }
    private void OnTriggerEnter(Collider other)
    {
        // Optional tag check
        if (!string.IsNullOrEmpty(requiredTag) && other.tag != requiredTag) return;

        // Example additional checks: you can replace or expand this
        if (!CanProceedFromHere(other))
        {
            Debug.Log("SubSceneTrigger: conditions not met to enter subscene.");
            return;
        }

        if (SceneTransitionController.Instance == null)
        {
            Debug.LogError("SceneTransitionController not found. Place SceneTransitionController in the main scene (DontDestroyOnLoad).");
            return;
        }

        SceneTransitionController.Instance.EnterSubScene(subSceneName);
    }

    private bool CanProceedFromHere(Collider who)
    {
        return !hasCompleted;
    }
}
