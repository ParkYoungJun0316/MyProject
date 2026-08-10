using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 옵션 패널 좌측 탭(일반/사운드/팀 보이스) 전환.
/// 각 탭 버튼 클릭 시 해당 콘텐츠 패널만 켜고, 선택/비선택 스프라이트를 교체한다.
/// </summary>
public class OptionsPanelTabs : MonoBehaviour
{
    [System.Serializable]
    public class Tab
    {
        public Button button;
        public GameObject content;
        public Image buttonBackground;
    }

    [SerializeField] Tab[] tabs;
    [SerializeField] Sprite selectedSprite;
    [SerializeField] Sprite unselectedSprite;
    [SerializeField] int defaultTabIndex;

    /// <summary>마지막으로 본 탭(패널 인스턴스 간 공유 — 타이틀/ESC 어느 쪽에서 열어도 이어짐). -1이면 아직 없음.</summary>
    static int s_lastTabIndex = -1;

    void Awake()
    {
        for (int i = 0; i < tabs.Length; i++)
        {
            int index = i;
            if (tabs[i].button != null)
                tabs[i].button.onClick.AddListener(() => ShowTab(index));
        }
    }

    void OnEnable() => ShowTab(s_lastTabIndex >= 0 ? s_lastTabIndex : defaultTabIndex);

    public void ShowTab(int index)
    {
        if (tabs == null || tabs.Length == 0) return;
        index = Mathf.Clamp(index, 0, tabs.Length - 1);
        s_lastTabIndex = index;

        for (int i = 0; i < tabs.Length; i++)
        {
            bool on = i == index;
            if (tabs[i].content != null)
                tabs[i].content.SetActive(on);
            if (tabs[i].buttonBackground != null)
            {
                if (on && selectedSprite != null)
                    tabs[i].buttonBackground.sprite = selectedSprite;
                else if (!on && unselectedSprite != null)
                    tabs[i].buttonBackground.sprite = unselectedSprite;
            }
        }
    }
}
