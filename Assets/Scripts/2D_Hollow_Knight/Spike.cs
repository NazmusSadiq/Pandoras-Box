using System.Collections;
using UnityEngine;

public class Spike : MonoBehaviour
{
    BoxCollider2D box_Collider = null;
    public Base_GameManager gameManager = null;

    [Header("Damage Cooldown")]
    [Tooltip("Seconds the spike will be inactive after dealing damage")]
    public float damageCooldown = 1.0f;

    // Spike can only damage while this is true
    private bool canDamage = true;

    void Start()
    {
        if (box_Collider == null)
        {
            box_Collider = GetComponent<BoxCollider2D>();
            if (box_Collider == null)
                Debug.LogWarning("[Spike] BoxCollider2D not assigned or found!");
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!canDamage) return;

        if (other.CompareTag("Player"))
        {
            if (gameManager != null)
            {
                gameManager.Reduce_Lives();
            }

            StartCoroutine(DamageCooldownRoutine());
        }
    }

    private IEnumerator DamageCooldownRoutine()
    {
        canDamage = false;
        yield return new WaitForSeconds(damageCooldown);
        canDamage = true;
    }
}
