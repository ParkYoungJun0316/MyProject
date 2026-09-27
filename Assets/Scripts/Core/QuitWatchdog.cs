using System;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;

/// <summary>
/// 빌드 종료가 엔진 정리 단계에서 멈추면 프로세스를 강제로 끝내는 감시 스레드.
///
/// [원인 — 2026-09-27]
/// 특정 USB HID 장치(RGB 컨트롤러·헤드셋 등)가 이름 조회(HidD_GetProductString)에 응답하지 않으면
/// UnityPlayer의 HID 스레드가 커널 호출에서 영원히 돌아오지 않고, 종료 중인 메인 스레드가 그 스레드를
/// 기다리다 멈춘다 — 창 응답 없음·포커스 불가·Quit 안 됨. Mono/IL2CPP 무관(엔진 네이티브 코드),
/// 우리 코드로는 막을 수 없어 종료 자체를 보장하는 쪽으로 처리한다.
///
/// [동작]
/// Application.quitting(Quit 버튼·Alt+F4·창 X·재시작 경로 공통)에서 설정을 먼저 저장하고 감시 스레드를 띄운다.
/// 정상 종료(NGO 연결 해제·Steam Shutdown 등 기존 OnApplicationQuit 정리 포함)는 그대로 진행되고,
/// 제한 시간 안에 끝나면 프로세스와 함께 이 스레드도 사라진다. 끝나지 않을 때만 프로세스를 끝낸다.
/// 종료 직후 바로 끊지 않는 이유: NGO Shutdown()은 연결 해제를 다음 프레임으로 미루므로
/// 즉시 끊으면 상대가 타임아웃까지 기다린다.
///
/// 강제 종료는 kernel32 TerminateProcess를 직접 호출한다 — IL2CPP 빌드에서 Process.Kill()은
/// 로그만 남기고 프로세스를 끝내지 못했다(2026-09-27 실측). ExitProcess는 DLL 분리 단계에서 같은
/// 방식으로 멈출 수 있어 쓰지 않는다.
///
/// 에디터에서는 동작하지 않는다 — 종료 대상이 에디터 프로세스가 된다.
/// </summary>
public static class QuitWatchdog
{
    const int KillAfterMs = 2000;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Install()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        Application.quitting -= OnQuitting;
        Application.quitting += OnQuitting;
#endif
    }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
    [DllImport("kernel32.dll")]
    static extern IntPtr GetCurrentProcess();

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool TerminateProcess(IntPtr hProcess, uint exitCode);

    static int _armed;

    static void OnQuitting()
    {
        if (Interlocked.Exchange(ref _armed, 1) == 1) return;

        // 강제 종료되면 엔진의 종료 시 자동 저장을 거치지 않는다 — 옵션·언어 저장값을 먼저 확정한다.
        PlayerPrefs.Save();

        new Thread(WatchLoop) { IsBackground = true, Name = "QuitWatchdog" }.Start();
    }

    static void WatchLoop()
    {
        Thread.Sleep(KillAfterMs);
        Debug.LogWarning($"[QuitWatchdog] 종료가 {KillAfterMs}ms 안에 끝나지 않아 프로세스를 강제 종료합니다.");
        if (!TerminateProcess(GetCurrentProcess(), 0))
            Debug.LogError($"[QuitWatchdog] TerminateProcess 실패 — Win32 error {Marshal.GetLastWin32Error()}");
    }
#endif
}
