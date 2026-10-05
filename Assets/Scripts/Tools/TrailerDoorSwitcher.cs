using System.Reflection;
using UnityEngine;

/// <summary>
/// 트레일러 촬영 전용(Marketing_ 씬): T.Stage5 색 문 전환을 ColorGateController와 같은 규칙으로 낸다 —
/// 한 번에 한 색만 열리고, 그 색을 열면 나머지는 닫힌다. 문 움직임은 게임 DoorController.Open/Close 그대로.
/// groups[i] = i번 색의 문들. openAtStart 색은 시작 프레임에 이미 열린 상태로 둔다(직전 전환이 끝난 상태).
/// </summary>
public class TrailerDoorSwitcher : MonoBehaviour
{
    [System.Serializable]
    public class Group { public DoorController[] doors; public bool neverOpens; }

    public Group[] groups;
    public int openAtStart = -1;

    void Start()
    {
        if (openAtStart < 0) return;
        FieldInfo isOpen = typeof(DoorController).GetField("_isOpen", BindingFlags.Instance | BindingFlags.NonPublic);
        foreach (DoorController d in groups[openAtStart].doors)
        {
            Vector3 up = d.transform.position + Vector3.up * d.openAmount;
            d.transform.position = up;
            var rb = d.GetComponent<Rigidbody>();
            if (rb != null) rb.position = up;
            isOpen?.SetValue(d, true);
        }
    }

    /// <summary>index 색을 열고 나머지 색은 닫는다(Signal float 인자 = 색 번호).</summary>
    public void Switch(float index)
    {
        int open = Mathf.RoundToInt(index);
        for (int i = 0; i < groups.Length; i++)
        {
            if (groups[i].neverOpens) continue;
            foreach (DoorController d in groups[i].doors)
            {
                if (i == open) d.Open();
                else d.Close();
            }
        }
    }
}
