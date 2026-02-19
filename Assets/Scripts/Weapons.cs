using UnityEngine;
using System.Collections;

public class Weapon : MonoBehaviour 
{
    public enum Type { Melee, Range};
    public Type type;
    public int damage;
    public float rate;
    public int maxAmmo;
    public int curAmmo;

    public BoxCollider meleeArea;
    public TrailRenderer trailEffect;
    public Transform bulletPos;
    public GameObject bullet;
    public Transform bulletCasePos;
    public GameObject bulletCase;
    public bool isAuto;

    public float maxRange = 50f; // 머신건 35, 권총 50 같은 식으로

    public void Use()
    {
        if (type == Type.Melee)
        {
                StopCoroutine("Swing");
                StartCoroutine("Swing");
        }
         else if (type == Type.Range && curAmmo>0)
        {
            curAmmo--;
                StartCoroutine("Shot");
        }
        
    }

    IEnumerator Swing()
    {
        var viz = GetComponentInChildren<HammerRangeVisualizer>();
        if (viz != null) viz.SetAttacking(true);
        //1
        yield return new WaitForSeconds(0.1f); // 0.1초 대기
        meleeArea.enabled = true;
        trailEffect.enabled = true;

        yield return new WaitForSeconds(0.3f);
        meleeArea.enabled = false;

        yield return new WaitForSeconds(0.3f);
        trailEffect.enabled = false;
        if (viz != null) viz.SetAttacking(false);

        //yield return null; // 1프레임 대기
        //yield return new WaitForSeconds(0.1); // 0.1초 대기

    }

    //Use() 메인루틴 -> Swing() 서브루틴 -> Use() 메인루틴
    //코르틴 함수 : Use() 메인루틴 + Swing() 코루틴(Co-Op)

    IEnumerator Shot()
    {
        //1. 총알 발사
        GameObject intantBullet = Instantiate(bullet, bulletPos.position, bulletPos.rotation);
        Rigidbody bulletRigid = intantBullet.GetComponent<Rigidbody>();
        bulletRigid.linearVelocity = bulletPos.forward * 50f;

        yield return null;


        //2. 탄피 배출
        GameObject intantCase = Instantiate(bulletCase, bulletCasePos.position, bulletCasePos.rotation);
        Rigidbody caseRigid = intantCase.GetComponent<Rigidbody>();
        Vector3 caseVec = bulletCasePos.forward * Random.Range(-2f, -3f) + Vector3.up * Random.Range(2f, 3f);
        caseRigid.AddForce(caseVec, ForceMode.Impulse);
        caseRigid.AddTorque(Vector3.up * 10, ForceMode.Impulse);

    }

}