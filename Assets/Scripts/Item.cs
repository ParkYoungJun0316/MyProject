using UnityEngine;

public class Item : MonoBehaviour
{
    public enum Type { Grenade }
    public Type type;

    Rigidbody rigid;
    Collider itemCollider;

    void Awake()
    {
        rigid = GetComponent<Rigidbody>();
        itemCollider = GetComponent<Collider>();
    }

    void Update()
    {
        transform.Rotate(Vector3.up * 20 * Time.deltaTime);
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Floor"))
        {
            rigid.isKinematic = true;
            if (itemCollider != null)
                itemCollider.isTrigger = true;
        }
    }
}
