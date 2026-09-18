using System.Collections.Generic;

/// <summary>
/// 설계슬롯(Blue/Purple/Green/Yellow) → 이번 판 실제 색의 <b>단일 진실 공급원</b>.
/// 정적 클래스 — `NetworkSessionData`와 같은 이유로 DontDestroyOnLoad가 필요 없다.
///
/// [규칙 — 2026-09-18 확정]
///  씬은 항상 **4인 기준**으로 만든다(Blue/Purple/Green/Yellow 패드·문 + 흑/백).
///  인원이 4명보다 적으면 빠진 색 슬롯을 **Common(누구나)** 으로 바꾼다.
///
///  | 인원 | 고유색 슬롯 | Common 슬롯 |
///  |---|---|---|
///  | 4인 | 4 | 0 |
///  | 3인 | 3 | 1 |
///  | 2인 | 2 | 2 |
///  | 1인 | 1 | 3 |
///
///  흑/백은 **고유색처럼 항상 존재하는 색**이라 이 치환의 대상이 아니다(그대로 통과).
///
/// [왜 Common인가 — 구 규칙(활성 색 재배정) 폐기]
///  예전엔 빠진 색을 살아있는 색 중 하나에게 재배정했다(2인 → 2+2). 그 규칙은
///   · 뽑기(셔플)가 필요해 **시드에 의존**했고, 시드는 사망 리로드 때 RPC로 오므로 매핑을
///     `OnPlayersReady`까지 미뤄야 했다. 그 지연 동안 색이 확정되지 않는 창이 생긴다.
///   · 서로 다른 두 설계슬롯이 같은 색으로 겹칠 수 있어 다색 AND 문(Door.C류)을 위한
///     충돌 보정 코드가 따로 필요했다.
///  Common 규칙은 **결정적 치환**이라 둘 다 사라진다 — 활성 색 목록만 알면 즉시 계산되고
///  (활성 색은 `PlayerSpawnCoordinator` NetworkList가 씬 로드 전부터 갖고 있다),
///  두 슬롯이 같은 고유색으로 겹치는 일이 구조적으로 불가능하다.
///
/// [Pull 규약 — push 금지]
///  이 맵은 **아무에게도 값을 밀어넣지 않는다.** 패드·문·비주얼이 각자 `Resolve()`로 당겨간다.
///  `Version`이 바뀌면 다시 당기면 된다. 예전 `StagePressurePadSetup`처럼 씬 전역을
///  `FindObjectsByType`으로 훑어 밀어넣으면 ① 나중에 생기거나 늦게 켜지는 대상이 누락되고
///  ② 호출 순서가 곧 정합성이 되며 ③ 1회 적용 후 래치라 실패해도 복구 경로가 없다.
///
/// [T5 라운드 제외]
///  T.Stage5는 라운드마다 러너가 바뀌고 러너는 1층이라 패드를 밟을 수 없다. 그래서 그 라운드의
///  **러너 색도 Common**으로 떨어뜨린다 — `SetRunnerExclusion()`이 그 한 겹이다.
///  T1/T4는 이 호출이 없으므로 활성 색만으로 씬 로드 즉시 확정된다.
/// </summary>
public static class SessionColorSlotMap
{
    /// <summary>치환 대상인 고유색 설계슬롯. 흑/백/Common은 여기 없다(= 치환하지 않는다).</summary>
    public static readonly PlayerColorType[] DesignSlots =
    {
        PlayerColorType.Blue,
        PlayerColorType.Purple,
        PlayerColorType.Green,
        PlayerColorType.Yellow,
    };

    /// <summary>매핑이 바뀔 때마다 증가. 조회자는 이 값이 달라졌을 때만 다시 당기면 된다.</summary>
    public static int Version { get; private set; }

    // 이번 판 참가 색(고유색만). Rebuild 전까지 바뀌지 않는다 — 라운드 제외는 여기서 빼지 않고
    // Resolve에서 걸러낸다. 그래야 "제외 해제"가 원본을 훼손하지 않는다.
    static readonly HashSet<PlayerColorType> s_activeSlots = new HashSet<PlayerColorType>();
    static PlayerColorType s_excluded = PlayerColorType.Common; // Common = 제외 없음

    /// <summary>
    /// 설계슬롯의 이번 판 실제 색.
    /// 고유색 슬롯이 이번 판에 살아 있고 라운드 제외 대상도 아니면 자기 자신,
    /// 아니면 <see cref="PlayerColorType.Common"/>. 흑/백/Common은 그대로 돌려준다.
    /// </summary>
    public static PlayerColorType Resolve(PlayerColorType designSlot)
    {
        if (!IsDesignSlot(designSlot)) return designSlot;   // 흑·백·Common 등은 치환 대상이 아니다
        if (designSlot == s_excluded) return PlayerColorType.Common;
        return s_activeSlots.Contains(designSlot) ? designSlot : PlayerColorType.Common;
    }

    /// <summary>designSlot이 치환 대상(고유색 4슬롯)인가.</summary>
    public static bool IsDesignSlot(PlayerColorType color)
    {
        foreach (PlayerColorType slot in DesignSlots)
            if (slot == color) return true;
        return false;
    }

    /// <summary>
    /// 활성 색 목록으로 매핑을 다시 세운다. `GameSession.Apply()`가 활성 색을 확정할 때마다 호출한다 —
    /// 활성 색이 바뀌는 자리가 거기 하나뿐이라 여기도 진입점이 하나로 유지된다.
    /// 라운드 제외(T5)는 유지되지 않고 초기화된다: 인원이 바뀌었다는 것은 판이 새로 시작됐다는 뜻이다.
    /// </summary>
    public static void Rebuild(IReadOnlyList<PlayerColorType> activeColors)
    {
        s_activeSlots.Clear();
        s_excluded = PlayerColorType.Common;

        if (activeColors != null)
            foreach (PlayerColorType c in activeColors)
                if (IsDesignSlot(c)) s_activeSlots.Add(c);

        Version++;
    }

    /// <summary>
    /// T5 전용: 슬롯에서 빼둘 색(= 러너 색). Common을 넘기면 제외 없음.
    /// 값이 실제로 바뀔 때만 Version이 오른다 — 같은 값을 다시 넣으면 조회자를 깨우지 않는다.
    /// </summary>
    public static void SetRunnerExclusion(PlayerColorType excluded)
    {
        if (s_excluded == excluded) return;
        s_excluded = excluded;
        Version++;
    }

    /// <summary>타이틀 복귀·새 게임. `NetworkSessionData.Clear()`와 같은 자리에서 호출한다.</summary>
    public static void Clear()
    {
        s_activeSlots.Clear();
        s_excluded = PlayerColorType.Common;
        Version++;
    }
}
