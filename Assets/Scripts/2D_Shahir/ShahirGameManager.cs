using TMPro;
using UnityEngine;

public class ShahirGameManager : Base_GameManager
{
    [Header("Activation Target")]
    public GameObject objectToActivate;
    public bool activateOnStart = true;
    public float activateDelay = 0f;

    protected override void Start()
    {
        base.Start();
        if (activateOnStart && objectToActivate)
        {
            if (activateDelay <= 0f) objectToActivate.SetActive(true);
            else Invoke(nameof(ActivateTarget), activateDelay);
        }
    }

    public override bool CanProceed()
    {
        return !gameOver && enemy_Num <= 0;
    }

    public void ActivateTarget()
    {
        if (objectToActivate && !objectToActivate.activeSelf)
            objectToActivate.SetActive(true);
    }

    public void DeactivateTarget()
    {
        if (objectToActivate && objectToActivate.activeSelf)
            objectToActivate.SetActive(false);
    }
}
