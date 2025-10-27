using UnityEngine;

public class GameManager : Base_GameManager
{
    void Start()
    {
        Debug.Log("Game Manager");
    }

    void Update()
    {
        // your per-frame logic
    }

    // Implement required abstract method
    public override bool CanProceed()
    {
        // Example policy: allow proceeding while not game over
        return !gameOver;
    }
}
