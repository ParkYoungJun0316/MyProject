/// <summary>
/// 타이틀 복귀 시 초기화 범위.
/// </summary>
public enum TitleReturnScope
{
    /// <summary>판 단위 초기화: 네트워크·색·타이머·채팅·커서·timeScale.</summary>
    SessionOnly,

    /// <summary>SessionOnly + SceneFlowManager 스테이지 진행도(Cleared 기록) 리셋.</summary>
    FullRunReset,
}

/// <summary>
/// 타이틀 복귀 트리거 원인. 로그·분석용.
/// </summary>
public enum TitleReturnReason
{
    UserQuit,
    HostQuitRoom,
    ClientDisconnected,
    EndDemo,
    LobbyQuit,
}

/// <summary>
/// TitleReturnFlow.Request()에 전달하는 옵션.
/// </summary>
public struct TitleReturnOptions
{
    public TitleReturnScope  Scope;
    public TitleReturnReason Reason;

    public static TitleReturnOptions Default => new TitleReturnOptions
    {
        Scope  = TitleReturnScope.SessionOnly,
        Reason = TitleReturnReason.UserQuit,
    };
}
