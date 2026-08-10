using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Toggle의 on/off 상태에 따라 대상 Image의 스프라이트를 교체하는 범용 컴포넌트.
///
/// 예전에는 에디터 툴(SetupSettingPanel.cs)이 <c>toggle.onValueChanged.AddListener(closure)</c>로
/// 직접 등록했는데, 이건 에디터 실행 시점에만 메모리에 존재하는 비영속(non-persistent) 리스너라
/// 씬 저장 후 도메인 리로드·빌드에서는 사라져 시각 피드백이 깨지는 버그가 있었음
/// (SoundAndSettingsDesign.md §9.2-③). 이 컴포넌트는 필드 참조로 Inspector에 정상 직렬화되고,
/// 매번 OnEnable에서 스스로 구독하므로 그 문제가 없음.
/// </summary>
public class ToggleSpriteSwap : MonoBehaviour
{
    [SerializeField] Toggle toggle;
    [SerializeField] Image targetImage;
    [SerializeField] Sprite onSprite;
    [SerializeField] Sprite offSprite;

    void OnEnable()
    {
        if (toggle == null) return;
        toggle.onValueChanged.AddListener(Apply);
        Apply(toggle.isOn);
    }

    void OnDisable()
    {
        if (toggle != null) toggle.onValueChanged.RemoveListener(Apply);
    }

    void Apply(bool isOn)
    {
        if (targetImage == null) return;
        Sprite sprite = isOn ? onSprite : offSprite;
        if (sprite != null) targetImage.sprite = sprite;
    }
}
