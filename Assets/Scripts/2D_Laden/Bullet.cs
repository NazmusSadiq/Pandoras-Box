using UnityEngine;
using UnityEngine.Audio;

public class Bullet : MonoBehaviour
{
    public float speed = 10f; 
    private int direction = 1;
    public BoxCollider2D box_Collider = null;
    

    void Start()
    {
        
        if (box_Collider == null)
        {
            box_Collider = GetComponent<BoxCollider2D>();
            if (box_Collider == null)
                Debug.LogWarning("BoxCollider2D not assigned or found!");
        }
    }
    public void SetDirection(int dir)
    {
        direction = dir;
        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) * direction;
        transform.localScale = scale;
    }

    void Update()
    {
        transform.Translate(Vector3.right * speed * direction * Time.deltaTime);
        if (Mathf.Abs(transform.position.x) >= 9f)
        {
            Destroy(transform.gameObject);
        }
    }

}
