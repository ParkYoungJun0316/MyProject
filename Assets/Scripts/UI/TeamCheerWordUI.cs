using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// 인게임 HUD에 이번 판 TeamCheerWord를 상시 표시한다.
/// 개인 CheerName(<c>Txt.Nickname</c>) 오른쪽에 캡션/단어를 위아래로 붙인다 —
/// 캡션은 작게, 실제 외칠 단어만 크게. TeamStatus 슬롯에는 넣지 않는다(세션 공용 1회).
///
/// [배치] UI.prefab 루트(HP_Panel · Txt.Nickname 형제). 위치·폰트는 에디터.
/// 미연결이면 이 HUD만 없음 — 응원 판정은 그대로다.
/// </summary>
public class TeamCheerWordUI : MonoBehaviour
{
    [Header("표시")]
    [Tooltip("위 줄. 비우면 캡션은 프리팹에 적어 둔 텍스트를 유지.")]
    [SerializeField] TextMeshProUGUI captionLabel;
    [Tooltip("아래 줄 — 실제 TeamCheerWord (대문자). TextMeshPro / TextMeshProUGUI 둘 다 가능.")]
    [SerializeField] TMP_Text wordLabel;

    Coroutine _waitSubscribe;

    void Awake()
    {
        if (captionLabel != null) captionLabel.raycastTarget = false;
        if (wordLabel is TextMeshProUGUI wordUgui)
            wordUgui.raycastTarget = false;
    }

    void OnEnable()
    {
        RefreshWord();
        TrySubscribe();
    }

    void OnDisable() => Unsubscribe();

    void TrySubscribe()
    {
        if (CheerService.Instance != null)
        {
            Subscribe();
            return;
        }
        if (_waitSubscribe != null) return;
        _waitSubscribe = StartCoroutine(WaitAndSubscribe());
    }

    IEnumerator WaitAndSubscribe()
    {
        while (CheerService.Instance == null)
            yield return null;
        _waitSubscribe = null;
        if (isActiveAndEnabled)
            Subscribe();
    }

    void Subscribe()
    {
        var svc = CheerService.Instance;
        if (svc == null) return;
        svc.OnTeamCheerWordChanged -= RefreshWord;
        svc.OnTeamCheerWordChanged += RefreshWord;
        RefreshWord();
    }

    void Unsubscribe()
    {
        if (_waitSubscribe != null)
        {
            StopCoroutine(_waitSubscribe);
            _waitSubscribe = null;
        }
        if (CheerService.Instance != null)
            CheerService.Instance.OnTeamCheerWordChanged -= RefreshWord;
    }

    void RefreshWord()
    {
        if (wordLabel == null) return;
        wordLabel.text = CheerService.ResolveTeamCheerWord().ToUpperInvariant();
    }
}
