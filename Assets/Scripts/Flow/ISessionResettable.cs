/// <summary>
/// 타이틀 복귀 시 초기화가 필요한 DDOL·static 시스템이 구현하는 인터페이스.
///
/// [등록 방법]
/// void Awake() => TitleReturnFlow.Instance?.Register(this);
/// void OnDestroy() => TitleReturnFlow.Instance?.Unregister(this);
///
/// 씬에 붙은 일반 MonoBehaviour는 구현 불필요.
/// LoadScene(Single)으로 파괴될 때 자동으로 정리된다.
/// </summary>
public interface ISessionResettable
{
    void OnSessionReset(TitleReturnScope scope);
}
