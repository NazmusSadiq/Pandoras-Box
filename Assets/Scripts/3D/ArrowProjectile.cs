using UnityEngine;

public class ArrowProjectile : MonoBehaviour
{
    public float damage = 10f;
    public float lifetime = 5f;
    private ThreeD_Enemy ownerArcher;

    private Rigidbody rb;
    private bool isReleased = false;
    private float speed;
    private Vector3 targetPosition;
    public AudioClip stuckSound;
    public AudioClip fleshSound;
    public BoxCollider boxCollider = null;

    public void Release(ThreeD_Enemy archerRef)
    {
        ownerArcher = archerRef;
        isReleased = true;
        if(boxCollider != null )
        {
            boxCollider.enabled = true;
        }
    }
    void OnCollisionEnter(Collision collision)
    {
        if (!isReleased) return;

        if (collision.collider.transform.root.CompareTag("Enemy")) return;

        if (collision.collider.CompareTag("Player"))
        {
            ThreeD_Character player = collision.collider.GetComponentInParent<ThreeD_Character>();
            if (player != null)
            {
                if (!player.IsBlocking())
                {
                    player.GetHit(damage);
                    AudioSource.PlayClipAtPoint(fleshSound, transform.position);
                }
                if (ownerArcher != null)
                {
                    ownerArcher.MakePlayerFaceEnemy();
                    AudioSource.PlayClipAtPoint(stuckSound, transform.position);
                }
            }
            Destroy(gameObject);
            return;
        }
        else
        {
            AudioSource.PlayClipAtPoint(stuckSound, transform.position);
            Destroy(gameObject);
            return;
        }
    }

}