using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 스크린샷 촬영용 일시정지 + 프레임 단위 진행 컨트롤러 (디버그/마케팅 툴 — 게임플레이 로직과 무관).
/// Time.timeScale을 직접 다뤄서 트랩/애니메이션을 완전히 멈추거나, 한 프레임씩만 진행시킨다.
///
/// [사용법]
/// 1) 빈 GameObject에 이 컴포넌트를 붙여 씬에 배치 (ScreenshotFreeCamera와 같은 오브젝트에 둬도 무방).
/// 2) Play 모드에서 pauseKey(기본 F8)로 일시정지 On/Off.
/// 3) 일시정지 중 stepKey(기본 F7)를 누르면 딱 한 프레임만 진행 후 다시 멈춤
///    — 트랩이 터지는 결정적 순간을 프레임 단위로 찾을 때 사용.
/// 4) ScreenshotFreeCamera(F9)는 Time.timeScale과 무관하게(unscaledDeltaTime 기반) 항상 자유롭게 움직이므로,
///    멈춘 채로 카메라 구도만 조정할 수 있다.
///
/// [주의]
/// - Time.timeScale = 0은 FixedUpdate(물리)를 멈추고 Animator/Update 기반 이동도 정지시킨다.
///   완전한 정지가 필요 없고 "게임 진행은 그대로 두고 카메라만 자유롭게" 원하면 이 컴포넌트 없이
///   ScreenshotFreeCamera만 켜도 된다.
/// - 순수 디버그 툴. 실제 게임 로직(NetworkDamageUtil, 트랩 스케줄 등)에는 관여하지 않는다.
/// </summary>
public class ScreenshotPauseController : MonoBehaviour
{
    [Header("Keys")]
    [Tooltip("일시정지 On/Off 키")]
    [SerializeField] Key pauseKey = Key.F8;

    [Tooltip("일시정지 중 한 프레임만 진행하는 키")]
    [SerializeField] Key stepKey = Key.F7;

    [Header("Options")]
    [Tooltip("일시정지 중 오디오도 함께 멈출지 여부")]
    [SerializeField] bool pauseAudio = true;

    public bool IsPaused { get; private set; }

    void Update()
    {
        if (Keyboard.current == null) return;

        if (Keyboard.current[pauseKey].wasPressedThisFrame)
        {
            if (IsPaused) Resume();
            else Pause();
        }

        if (IsPaused && Keyboard.current[stepKey].wasPressedThisFrame)
            StartCoroutine(StepOneFrame());
    }

    void OnDestroy()
    {
        // 컴포넌트가 사라져도 timeScale이 0으로 고정되지 않도록 안전장치.
        Time.timeScale = 1f;
        AudioListener.pause = false;
    }

    void Pause()
    {
        Time.timeScale = 0f;
        if (pauseAudio) AudioListener.pause = true;
        IsPaused = true;
        Debug.Log("[ScreenshotPauseController] 일시정지");
    }

    void Resume()
    {
        Time.timeScale = 1f;
        if (pauseAudio) AudioListener.pause = false;
        IsPaused = false;
        Debug.Log("[ScreenshotPauseController] 재개");
    }

    IEnumerator StepOneFrame()
    {
        Time.timeScale = 1f;
        if (pauseAudio) AudioListener.pause = false;

        yield return null; // 실제 프레임 하나만 진행

        Time.timeScale = 0f;
        if (pauseAudio) AudioListener.pause = true;
        Debug.Log("[ScreenshotPauseController] 1프레임 진행");
    }
}
