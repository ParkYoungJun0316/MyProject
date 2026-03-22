/// <summary>
/// 플레이어 고유색 타입.
/// 오브젝트 소유권(상자, 바닥 등) 판별에 공통으로 사용.
/// </summary>
public enum PlayerColorType
{
    Common, // 공용 — 모든 플레이어 사용 가능
    Blue,
    Red,
    Green,
    Yellow,
    Danger, // 모두 즉사
}
