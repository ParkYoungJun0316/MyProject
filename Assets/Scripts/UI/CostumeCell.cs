using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 꾸미기 창(<see cref="CostumePanelUI"/>)의 칸 하나 — 버튼·아이콘·선택 표시 참조 묶음.
/// 창이 이 템플릿을 소품 개수만큼 복제하므로, 복제본의 자식을 이름으로 찾지 않도록 참조를 여기에 둔다.
/// </summary>
public class CostumeCell : MonoBehaviour
{
    public Button button;
    public Image icon;
    public GameObject selectedMark;
}
