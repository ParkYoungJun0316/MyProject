using UnityEngine;

/// <summary>
/// 배경 연출(EnvironmentEcho)용 저사양 회전 유틸.
/// 게임플레이 오브젝트에는 붙이지 말 것 — 순수 배경 데코 전용.
/// </summary>
public class EnvironmentEchoRotator : MonoBehaviour
{
    [SerializeField] private Vector3 axis = Vector3.right;
    [SerializeField] private float degreesPerSecond = 12f;

    void Update()
    {
        transform.Rotate(axis, degreesPerSecond * Time.deltaTime, Space.Self);
    }
}
