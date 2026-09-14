using UnityEngine;
using TMPro;

/// <summary>
/// 팀 공유 목숨(DownedReviveSystemDesign.md §4B) 상시 HUD — 아이콘 + 남은 개수. 읽기 전용(네트워크 쓰기 없음).
/// 이벤트 대신 매 프레임 폴링: Client는 NV 초기값이 스폰 데이터로 들어오면 OnValueChanged가 불리지 않는다.
///
/// [숨김] StageNetworkState 없는 씬(튜토리얼 — 목숨 제한 없음), 목숨 미초기화, 솔로(항상 0이라 의미 없음).
/// [구성] content = 아이콘+숫자를 담은 **자식** 오브젝트. 이 컴포넌트가 붙은 오브젝트를 넣으면 꺼진 뒤 다시 켜지지 않는다.
/// </summary>
public class TeamLivesUI : MonoBehaviour
{
    [SerializeField] GameObject content;
    [SerializeField] TextMeshProUGUI countText;

    int _shown = int.MinValue;

    void Update()
    {
        int lives = ReadVisibleLives();
        if (lives == _shown) return;
        _shown = lives;

        if (content != null) content.SetActive(lives >= 0);
        if (countText != null && lives >= 0) countText.text = lives.ToString();
    }

    /// <summary>화면에 표시할 남은 목숨. 숨겨야 하면 -1. LocalDownOverlayUI도 같은 기준을 쓴다.</summary>
    public static int ReadVisibleLives()
    {
        var state = StageNetworkState.Instance;
        if (state == null || PlayerSpawnCoordinator.EntryCount <= 1) return -1;
        return state.TeamLivesRemaining;
    }
}
