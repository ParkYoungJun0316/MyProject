using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 인게임에 동시에 열릴 수 있는 여러 마우스-필요 UI(치어네임 패널/ESC 메뉴/이모트 메뉴 등)가
/// 커서 lock/visible을 각자 무조건 덮어써서 "마지막에 닫은 쪽이 이긴다"로 서로 충돌하는 문제를
/// 막기 위한 공유 요청 카운트. 각 UI는 열 때 Request(this), 닫을 때 Release(this)만 호출하면
/// 되고, 실제 Cursor 반영은 여기 한 곳에서만 "아직 다른 요청이 남아있는지"로 계산한다
/// (요청 목록 → 실제 Cursor 상태로만 흐르는 단방향 — 2026-08-22, Bug Hunter 리뷰 3항목 중 2번 수정).
///
/// [사용처] TutorialCheerNameUI, EscMenuController, PlayerEmoteMenuUI.
/// TitleReturnFlow/EndDemoController처럼 인게임을 완전히 벗어나는 전역 전환은 이 유틸을 거치지
/// 않고 Cursor를 직접 강제 설정한다 — 그 시점엔 다른 UI 상태가 의미 없어지므로 정상.
///
/// [정적 상태 안전성 — 파괴 시엔 Forget, 정상 닫힘일 때만 Release, 2026-08-22 수정]
/// 씬이 갈아치워질 때(TitleReturnFlow의 SceneManager.LoadScene 등) 열려있던 UI가 Close() 없이
/// 통째로 파괴되면서 OnDisable/OnDestroy가 자동 호출되는데, 이 시점에 Release로 실제 Cursor를
/// 잠가버리면 TitleReturnFlow가 그 직전에 이미 정해둔 최종 커서 상태를 덮어써서 "타이틀 씬에서
/// 마우스가 사라지는" 회귀가 생겼다(원인: OnDestroy 안전장치가 Release를 썼던 버전).
/// 그래서 각 사용처는 "지금 이 파괴가 정상적인 사용자 닫기인지, 씬 통째 언로드인지"를
/// (예: gameObject.scene.isLoaded) 구분해서, 언로드 중이면 Forget(목록 제거만) / 정상 닫힘이면
/// Release(실제 Cursor 적용까지)를 쓴다(각 사용처 주석 참고).
/// </summary>
public static class CursorUnlockRequestUtil
{
    static readonly HashSet<object> _requesters = new();

    /// <summary>지금 마우스가 필요한 UI(Esc메뉴/이모트메뉴/치어네임패널/채팅 등)가 하나라도 떠 있는지 —
    /// 커서 해제 상태의 SSOT. ThirdPersonCamera 등 "이 UI 켜져있나?"를 개별로 알 필요 없이 이 값 하나만
    /// 보면 되는 소비자가 쓴다(2026-08-22, 특정 UI를 하드코딩해서 체크하던 걸 이걸로 대체).</summary>
    public static bool IsRequested => _requesters.Count > 0;

    /// <summary>이 UI가 커서 해제를 요청. 이미 다른 UI가 요청 중이어도 안전(중복 Add 무시).</summary>
    public static void Request(object requester)
    {
        _requesters.Add(requester);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
    }

    /// <summary>이 UI가 정상적으로 닫혔음을 알림. 아직 다른 요청이 남아있으면 잠그지 않는다.</summary>
    /// <param name="relockIfEmpty">남은 요청이 없을 때만 적용 — false면 이 UI가 마지막이어도
    /// 강제로 잠그지 않음(기존 lockCursorOnClose 옵션과 동일 의미).</param>
    public static void Release(object requester, bool relockIfEmpty = true)
    {
        _requesters.Remove(requester);
        if (_requesters.Count > 0 || !relockIfEmpty) return;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
    }

    /// <summary>씬 언로드 등으로 Close() 없이 통째로 파괴될 때만 쓰는 안전장치 전용 — 요청
    /// 목록에서만 제거하고 실제 Cursor.lockState/visible은 절대 건드리지 않는다. 이 시점엔
    /// 이미 다른 흐름(TitleReturnFlow 등)이 최종 커서 상태를 정해둔 뒤라 여기서 다시 손대면
    /// 그 값을 덮어쓰게 된다. 정상적으로 닫힐 때는 반드시 Release를 쓸 것.</summary>
    public static void Forget(object requester) => _requesters.Remove(requester);
}
