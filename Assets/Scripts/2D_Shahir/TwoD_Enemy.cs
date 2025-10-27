using UnityEngine;

public class TwoD_Enemy : MonoBehaviour
{
    public ShahirGameManager gameManager;
    public virtual void Hit()
    {
        Debug.Log("Enemy Hit");
    }
}
