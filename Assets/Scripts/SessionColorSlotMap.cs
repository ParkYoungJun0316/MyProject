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
/// [T5 고유색 순열 — 2026-09-20]
///  T5는 씬 로드 때 `SetDesignPermutation(seed)`으로 고유 4색을 한 번 섞는다. 문 180개의 색 배치가
///  같아도 **매판 다른 사람이 그 문의 열쇠를 쥔다**(`TStage5RunnerRedesign.md` §1.12).
///  흑·백은 순열 대상이 아니므로 생성기가 보장한 연결성·최소 전환 횟수는 그대로 산다.
///
/// [T5 러너 제외 — Common이 아니라 벽]
///  T.Stage5의 러너는 1층이라 2층 패드를 밟을 수 없다. 그래서 러너 색은 **이번 판에 없는 색과
///  똑같이 취급**한다 — `SetRunnerExclusion()`이 그 한 겹이고, 판정은 `IsSlotAbsent()`가 한다.
///  **T5에는 Common 문도 Common 패드도 없다**(2026-09-20 §1.4).
///  T1/T4는 이 호출이 없고, 그쪽에서 "없는 색"은 여전히 Resolve가 Common으로 떨어뜨린다.
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

    // 이번 판의 고유색 순열. designSlot → "실제로 그 색이 칠해진 것처럼" 취급할 슬롯.
    // 비어 있으면 항등(순열 없음)이고, 그것이 T1/T4의 상태다 — T5만 SetDesignPermutation()으로 얹는다.
    static readonly Dictionary<PlayerColorType, PlayerColorType> s_permutation =
        new Dictionary<PlayerColorType, PlayerColorType>();

    const int PermutationSalt = 0x54355043; // "T5PC" — 러너 뽑기 시드와 같은 시드를 써도 답이 갈리게

    /// <summary>
    /// 설계슬롯의 이번 판 실제 색.
    /// 고유색 슬롯이 이번 판에 살아 있고 라운드 제외 대상도 아니면 자기 자신,
    /// 아니면 <see cref="PlayerColorType.Common"/>. 흑/백/Common은 그대로 돌려준다.
    /// </summary>
    public static PlayerColorType Resolve(PlayerColorType designSlot)
    {
        if (!IsDesignSlot(designSlot)) return designSlot;   // 흑·백·Common 등은 치환 대상이 아니다

        PlayerColorType actual = Permute(designSlot);       // 순열이 없으면 자기 자신
        return s_activeSlots.Contains(actual) ? actual : PlayerColorType.Common;
    }

    /// <summary>
    /// 설계슬롯에 이번 판 순열을 적용한 슬롯. 순열이 없으면(T1/T4) 자기 자신이다.
    /// <see cref="Resolve"/>가 안에서 쓰는 것과 같은 값 — 벽 판정(T5)처럼 "Common으로 떨어지기 전"이
    /// 필요한 쪽만 직접 부른다.
    /// </summary>
    public static PlayerColorType Permute(PlayerColorType designSlot)
    {
        if (!IsDesignSlot(designSlot)) return designSlot;
        PlayerColorType mapped;
        return s_permutation.TryGetValue(designSlot, out mapped) ? mapped : designSlot;
    }

    /// <summary>
    /// 이 슬롯이 이번 판에 **아무도 열 수 없는 색인가** = T5의 "벽" 판정 (`TStage5RunnerRedesign.md` §1.4).
    /// 둘 다 벽이다:
    ///  · **이번 판에 없는 색** — 들고 있는 사람이 아예 없다.
    ///  · **러너 색** — 그 색을 든 사람은 1층에 있어 2층 패드를 밟을 수 없다(`SetRunnerExclusion`).
    ///
    /// T1/T4는 이 질문을 하지 않는다. 그쪽에서 "없는 색"은 <see cref="Resolve"/>가 Common으로
    /// 떨어뜨려 **누구나 밟는 패드**가 되고, 그게 그 스테이지들의 완화 규칙이다(`PressurePad`).
    /// 같은 처리를 문 180개짜리 T5에 쓰면 2인에서 문의 절반이 한 번에 열려 스테이지가 사라진다.
    /// 흑·백·Common은 치환 대상이 아니므로 언제나 false(= 벽이 될 수 없다).
    /// </summary>
    public static bool IsSlotAbsent(PlayerColorType designSlot)
    {
        if (!IsDesignSlot(designSlot)) return false;

        PlayerColorType actual = Permute(designSlot);
        return actual == s_excluded || !s_activeSlots.Contains(actual);
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
        s_permutation.Clear();   // 순열도 판마다 다시 정한다 — T5가 OnPlayersReady에서 얹는다

        if (activeColors != null)
            foreach (PlayerColorType c in activeColors)
                if (IsDesignSlot(c)) s_activeSlots.Add(c);

        Version++;
    }

    /// <summary>
    /// T5 전용: 이번 판의 고유색 순열을 시드로 정한다(`TStage5RunnerRedesign.md` §1.12 / §4.2-2).
    /// 문에 칠해진 설계 4색을 섞어 **매판 다른 사람이 열쇠를 쥐게** 한다.
    ///
    /// 흑·백·Common은 건드리지 않는다 — 그래서 §1.6의 "흑 ∪ 백이 start→goal을 잇는다"와
    /// 생성기가 보장한 최소 전환 횟수가 **순열에 대해 불변**이고, 순열마다 검증을 다시 돌릴 필요가 없다.
    ///
    /// 전 머신이 같은 시드로 같은 답을 내므로 NV에 싣지 않는다(§4.3).
    /// 값이 실제로 바뀔 때만 Version이 오른다.
    /// </summary>
    public static void SetDesignPermutation(int seed)
    {
        var shuffled = new List<PlayerColorType>(DesignSlots);

        var rng = new System.Random(seed ^ PermutationSalt);
        for (int i = shuffled.Count - 1; i > 0; i--)
        {
            int j = rng.Next(0, i + 1);
            (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }

        bool changed = false;
        for (int i = 0; i < DesignSlots.Length; i++)
        {
            PlayerColorType from = DesignSlots[i];
            PlayerColorType to   = shuffled[i];

            PlayerColorType prev;
            if (!s_permutation.TryGetValue(from, out prev) || prev != to) changed = true;
            s_permutation[from] = to;
        }

        if (changed) Version++;
    }

    /// <summary>
    /// T5 전용: 이번 판 러너의 색. Common을 넘기면 제외 없음.
    ///
    /// **이 색은 <see cref="IsSlotAbsent"/>에서 "없는 색"과 같이 벽이 된다** (2026-09-20 §1.4).
    /// 구 규칙은 러너 색을 Common(누구나 엶)으로 떨어뜨렸는데, 모든 변이 문인 구조에서는
    /// 러너 색 30개가 통째로 "아무나 여는 문"이 되어 격자가 헐거워진다. 러너는 1층이라 애초에
    /// 자기 패드를 밟을 수 없으므로 **그 색은 이번 판에 없는 색과 다를 게 없다**는 쪽으로 통일했다.
    /// 그래서 T5에는 Common 문도 Common 패드도 존재하지 않는다.
    ///
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
        s_permutation.Clear();
        Version++;
    }
}
