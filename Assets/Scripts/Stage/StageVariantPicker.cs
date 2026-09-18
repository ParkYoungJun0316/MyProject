using UnityEngine;

/// <summary>
/// 변형판 N개 중 **이번 런에 쓸 하나**를 시드로 골라 활성화한다. 판이 한 번 정해지면 끝까지 그대로다.
/// 라운드마다 갈아끼우는 쪽은 <see cref="StageVariantRoundPicker"/>.
///
/// [쓰는 곳]
///  T.Stage4 격자 판 5장(`BreakTileDirector`가 호출). 앞으로 T.Stage2·T.Boss처럼 "지형만 런마다
///  바뀌면 되는" 스테이지도 이 컴포넌트 하나면 된다.
///
/// [호출 시점 — ⚠️ OnPlayersReady 이후]
///  `Pick()`을 Start에서 부르면 안 된다. 사망 리로드 때 새 시드가 RPC로 오는데 그게 Start보다
///  먼저 도착한다는 보장이 없어, 그 판만 **이전 시드로** 골라 버린다. 호출자가
///  `PlayerSpawnCoordinator.OnPlayersReady` 이후에 부를 것(`BreakTileDirector` 참고).
///
/// [씬 설정]
///  1. 빈 GameObject에 이 컴포넌트.
///  2. `variants`에 후보 루트들을 넣거나, `variantsRoot` + `childNamePrefix`로 자동 수집.
///     **후보는 전부 비활성으로 저장할 것.**
///  3. `anchor`에 옮겨 놓을 위치(선택 — 비우면 제자리에서 켜진다).
/// </summary>
public class StageVariantPicker : StageVariantPickerBase
{
    /// <summary>이번 런에 고른 변형판. `Pick()` 전이면 null.</summary>
    public Transform Chosen { get; private set; }

    /// <summary>이번 런에 고른 인덱스. `Pick()` 전이면 -1.</summary>
    public int ChosenIndex { get; private set; } = -1;

    /// <summary>
    /// 시드로 하나 골라 활성화하고 그 Transform을 돌려준다. 두 번째 호출부터는 첫 결과를 그대로 준다
    /// (같은 런에서 판이 바뀌면 이미 밟고 서 있던 바닥이 사라진다).
    /// </summary>
    public Transform Pick()
    {
        if (ChosenIndex >= 0) return Chosen;

        if (Count == 0)
        {
            Debug.LogWarning($"[StageVariantPicker] {name}: 후보가 비어 있다 — variants 또는 childNamePrefix를 지정할 것.", this);
            return null;
        }

        int[] draw = DrawIndices(1);
        ChosenIndex = draw[0];
        Chosen      = ActivateOnly(ChosenIndex);

        Debug.Log($"[StageVariantPicker] {name}: 후보={Count} 선택={ChosenIndex}({Chosen?.name}) seed={NetworkSessionData.Seed}");
        return Chosen;
    }
}
