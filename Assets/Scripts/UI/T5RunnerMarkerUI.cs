using TMPro;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// T.Stage5 러너 머리 위 마커. **2층 안내자에게만** 보인다.
/// `TStage5RunnerRedesign.md` §1.5.
///
/// [왜 씬에 1개인가]
/// 러너는 판마다 한 명뿐이라 마커도 하나면 충분하다. Player 프리팹에 붙이면 인원수만큼
/// 컴포넌트가 생기고 프리팹 수정까지 필요한데, 씬 오브젝트 하나가 러너를 따라다니면 그만이다.
/// (PlayerNameTagUI가 프리팹에 붙는 건 사람마다 내용이 다르기 때문 — 여기는 그렇지 않다)
///
/// [표시 규칙]
/// - 러너가 아직 정해지지 않았으면 숨김.
/// - **로컬 플레이어가 러너면 숨김** — 자기 머리 위 화살표는 시야만 가린다. 1층을 달리는
///   본인은 이미 자기 위치를 안다. 솔로도 이 규칙에 걸려 자동으로 안 보인다.
/// - 그 외(=내가 2층 안내자)에게만 보인다. 이게 §1.5가 요구한 "2층 인원에게만".
///
/// [네트워크] 쓰기 없음. `StageNetworkState`의 러너 clientId NV를 읽기만 한다.
///
/// [씬 설정] 빈 GameObject에 이 스크립트만. 텍스트는 코드가 만든다.
/// </summary>
public class T5RunnerMarkerUI : MonoBehaviour
{
    [Header("표시 위치")]
    [Tooltip("러너 머리 위 오프셋. PlayerNameTagUI(2.2) 위로 올라가도록 조금 더 높게.")]
    [SerializeField] Vector3 offset = new Vector3(0f, 3.4f, 0f);

    [Header("모양")]
    [Tooltip("표시할 글자. 기호 하나를 권장 — 2층에서 내려다볼 때 읽는 게 아니라 위치만 찾는 용도다.")]
    [SerializeField] string markerText = "▼";
    [SerializeField] float fontSize = 6f;
    [SerializeField] Color textColor = new Color(1f, 0.85f, 0.2f, 1f);
    [SerializeField] Color outlineColor = new Color32(0, 0, 0, 255);
    [SerializeField] float outlineWidth = 0.25f;

    [Header("애니메이션")]
    [Tooltip("위아래로 흔들리는 폭(m). 0이면 고정.")]
    [SerializeField] float bobAmplitude = 0.25f;
    [Tooltip("위아래 흔들림 속도")]
    [SerializeField] float bobSpeed = 3f;

    TextMeshPro _text;
    Transform   _camTransform;

    // 러너 Transform 캐시 — clientId가 바뀔 때만 다시 찾는다(매 프레임 FindObjects 방지)
    Transform _runnerTransform;
    ulong     _cachedRunnerId = ulong.MaxValue;
    bool      _cacheValid;

    void Awake()
    {
        BuildText();
        SetVisible(false);
    }

    void LateUpdate()
    {
        Transform runner = ResolveRunner();
        if (runner == null) { SetVisible(false); return; }

        SetVisible(true);

        float bob = bobAmplitude > 0f ? Mathf.Sin(Time.time * bobSpeed) * bobAmplitude : 0f;
        _text.transform.position = runner.position + offset + Vector3.up * bob;

        // Y축만 카메라를 따라 회전 — 글자가 항상 수직으로 선다 (PlayerNameTagUI와 동일 패턴).
        if (_camTransform == null) _camTransform = Camera.main?.transform;
        if (_camTransform != null)
            _text.transform.rotation = Quaternion.Euler(0f, _camTransform.eulerAngles.y, 0f);
    }

    // ── 내부 ────────────────────────────────────────────────────

    /// <summary>표시해야 할 러너의 Transform. 숨겨야 하는 상황이면 null.</summary>
    Transform ResolveRunner()
    {
        var net = StageNetworkState.Instance;
        var nm  = NetworkManager.Singleton;
        if (net == null || nm == null || !nm.IsListening) return null;
        ulong runnerId = net.T5RunnerClientId;
        if (runnerId == StageNetworkState.NoRunner) return null; // 아직 미추첨

        // 내가 러너면 내 머리 위에 화살표를 띄우지 않는다.
        if (runnerId == nm.LocalClientId) return null;

        if (!_cacheValid || runnerId != _cachedRunnerId || _runnerTransform == null)
        {
            _runnerTransform = FindPlayerTransform(runnerId);
            _cachedRunnerId  = runnerId;
            _cacheValid      = true;
        }

        return _runnerTransform;
    }

    /// <summary>
    /// clientId로 Player를 찾는다. Client에서는 `ConnectedClients`가 비어 있으므로
    /// 씬의 Player를 훑어 OwnerClientId로 대조한다 — 판당 한 번만 도는 경로다.
    /// </summary>
    static Transform FindPlayerTransform(ulong clientId)
    {
        foreach (Player p in FindObjectsByType<Player>(FindObjectsSortMode.None))
        {
            NetworkObject no = p.GetComponent<NetworkObject>();
            if (no != null && no.OwnerClientId == clientId) return p.transform;
        }
        return null;
    }

    void SetVisible(bool visible)
    {
        if (_text != null && _text.gameObject.activeSelf != visible)
            _text.gameObject.SetActive(visible);
    }

    void BuildText()
    {
        var go = new GameObject("RunnerMarker");
        go.transform.SetParent(transform, false);

        _text              = go.AddComponent<TextMeshPro>();
        _text.text         = markerText;
        _text.alignment    = TextAlignmentOptions.Center;
        _text.color        = textColor;
        _text.fontStyle    = FontStyles.Bold;
        _text.fontSize     = fontSize;
        _text.outlineWidth = outlineWidth;
        _text.outlineColor = outlineColor;
    }
}
