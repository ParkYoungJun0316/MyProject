/// <summary>
/// 플레이어 고유색 타입.
/// 오브젝트 소유권(상자, 바닥 등) 판별에 공통으로 사용.
/// </summary>
public enum PlayerColorType
{
    Common, // 공용 패드 등 — 모든 플레이어. 값 유지(직렬화). ColorTile 흑백에 쓰지 않음.
    Blue,
    Purple,
    Green,
    Yellow,
    Danger, // SequenceRing 즉사 등. 값 유지(직렬화).
    Black,
    White,
}
