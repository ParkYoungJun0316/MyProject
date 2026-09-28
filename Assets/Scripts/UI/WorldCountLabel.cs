using TMPro;
using UnityEngine;

/// <summary>
/// 발판·안전 칸 위에 띄우는 "현재/최대" 월드 숫자 — 모양의 SSOT.
/// 폰트 스타일·외곽선·부모 Scale 상쇄·카메라 Y 회전을 여기서만 정한다.
/// 무엇을 셀지(패드 인원 / 안전 칸 인원)와 언제 보일지는 쓰는 쪽이 정한다.
///
/// [쓰는 곳]
///  - PressurePadCountUI : 압력 발판 (T.Stage1 · T.Stage3 · T.Boss P2)
///  - SafeZoneWarnSign   : T.Boss P3 공용 안전 칸 (겹침이면 진홍)
///
/// [부모 Scale 상쇄] 쿠키 발판처럼 루트 Scale이 30~80배인 부모 밑에 붙어도 글자는 월드 1배로 보인다.
///  부모는 유지해서 Phase 루트·마커가 꺼지면 같이 꺼진다.
/// </summary>
public class WorldCountLabel : MonoBehaviour
{
    TextMeshPro _text;
    Transform   _camTransform;

    // Instantiate로 복제된 라벨은 비직렬화 필드(_text)가 비어 있다 — 붙어 있는 TMP를 늦게 찾아온다.
    TextMeshPro Text => _text != null ? _text : (_text = GetComponent<TextMeshPro>());

    /// <summary>parent 자식으로 만들고 worldPosition에 둔다. fontSize ≤ 0이면 TMP 기본값.</summary>
    public static WorldCountLabel Create(Transform parent, Vector3 worldPosition, float fontSize)
    {
        // TMP는 붙자마자 Awake에서 머티리얼을 잡아야 outlineWidth 등을 설정할 수 있다. 꺼진 부모(안전 칸 마커는
        // 꺼둔 채 옮긴 뒤 켠다) 밑에서 AddComponent하면 Awake가 안 돌아 SetOutlineThickness가 NRE를 던진다
        // (2026-09-29 T.Boss P3 라운드 멈춤). 그래서 켜진 루트에서 만들고 꾸민 뒤에 부모로 옮긴다.
        var go = new GameObject("CountLabel");
        var label = go.AddComponent<WorldCountLabel>();
        label.BuildText(fontSize);

        go.transform.SetParent(parent, false);
        go.transform.position = worldPosition;
        go.transform.rotation = Quaternion.identity;

        Vector3 s = parent != null ? parent.lossyScale : Vector3.one;
        go.transform.localScale = new Vector3(
            Mathf.Approximately(s.x, 0f) ? 1f : 1f / s.x,
            Mathf.Approximately(s.y, 0f) ? 1f : 1f / s.y,
            Mathf.Approximately(s.z, 0f) ? 1f : 1f / s.z);

        return label;
    }

    void BuildText(float fontSize)
    {
        _text           = gameObject.AddComponent<TextMeshPro>();
        _text.alignment = TextAlignmentOptions.Center;
        _text.color     = Color.white;
        _text.fontStyle = FontStyles.Bold;

        if (fontSize > 0f)
            _text.fontSize = fontSize;

        _text.outlineWidth = 0.2f;
        _text.outlineColor = new Color32(0, 0, 0, 255);
    }

    /// <summary>"current/max"를 color로 표시. 값이 같으면 TMP 재생성을 피하려고 건너뛴다.</summary>
    public void Set(int current, int max, Color color)
    {
        TextMeshPro text = Text;
        if (text == null) return;
        string s = $"{current}/{max}";
        if (text.text != s) text.text = s;
        if (text.color != color) text.color = color;
    }

    public void Set(int current, int max) => Set(current, max, Color.white);

    void LateUpdate()
    {
        // Y 축만 카메라를 따라 수평 회전 — X·Z 고정으로 텍스트 항상 수직 유지
        if (_camTransform == null) _camTransform = Camera.main?.transform;
        if (_camTransform != null)
            transform.rotation = Quaternion.Euler(0f, _camTransform.eulerAngles.y, 0f);
    }
}
