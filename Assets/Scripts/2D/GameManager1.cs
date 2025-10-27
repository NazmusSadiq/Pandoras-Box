using UnityEngine;

public class GameManager1 : Base_GameManager
{
    [Header("Activation Target")]
    public GameObject objectToActivate;
    public bool activateOnStart = true;
    public float activateDelay = 0f;

    void Start()
    {
        if (activateOnStart && objectToActivate)
        {
            if (activateDelay <= 0f) objectToActivate.SetActive(true);
            else Invoke(nameof(ActivateTarget), activateDelay);
        }
    }

    void Update()
    {
        // Your per-frame game logic, if any
    }

    // Implement required abstract method
    public override bool CanProceed()
    {
        // Example policy: allow proceeding while not game over
        return !gameOver;
    }

    // Public API to activate the target from anywhere
    public void ActivateTarget()
    {
        if (objectToActivate && !objectToActivate.activeSelf)
            objectToActivate.SetActive(true);
    }

    // Convenience: deactivate if you ever need it
    public void DeactivateTarget()
    {
        if (objectToActivate && objectToActivate.activeSelf)
            objectToActivate.SetActive(false);
    }
}
