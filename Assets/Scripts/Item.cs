using UnityEngine;

public class Item : MonoBehaviour
{
    public enum Type { Coin, Grenade, HealthPotion, Shield }
    public Type type;
    public int value;

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
