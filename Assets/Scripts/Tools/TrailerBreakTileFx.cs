using UnityEngine;

/// <summary>
/// 트레일러 촬영 전용(Marketing_ 씬): Timeline Signal로 게임 BreakTile의 Host 확정 경로를 그대로 호출한다.
/// 네트워크가 없으면 BreakTile.ServerNow()가 Time.timeAsDouble이라 "지금 + seconds"를 넘기면
/// 게임과 같은 경고(탠저린→진홍)·파괴(파편·분출) 연출이 그 시각에 난다.
/// </summary>
public class TrailerBreakTileFx : MonoBehaviour
{
    public BreakTile tile;
    uint _seq;

    /// <summary>① 정원 초과: seconds 뒤 파괴로 경고 시작(매번 새 경고 번호).</summary>
    public void ArmCapacity(float seconds) => tile.ArmCapacityFromServer(Time.timeAsDouble + seconds, ++_seq);

    /// <summary>① 경고 중 정원 이하로 줄어 취소.</summary>
    public void CancelCapacity() => tile.CancelCapacityFromServer(_seq);

    /// <summary>③ 밟기: seconds 뒤 파괴로 경고 시작.</summary>
    public void ArmStep(float seconds) => tile.ArmFromServer(Time.timeAsDouble + seconds);
}
