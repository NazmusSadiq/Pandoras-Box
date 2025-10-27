using UnityEngine;

public class Finish_Line : MonoBehaviour
{
    public BoxCollider2D box_Collider = null;
    public Base_GameManager gameManager = null;

    void Start()
    {
        if (box_Collider == null)
        {
            box_Collider = GetComponent<BoxCollider2D>();
            if (box_Collider == null)
                Debug.LogWarning("BoxCollider2D not assigned or found!");
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player") && gameManager.CanProceed())
        {
            gameManager.Finish_Game();
        }
    }

}
