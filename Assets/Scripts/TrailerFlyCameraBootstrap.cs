using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 촬영 전용 프리캠 F9 토글 — 씬/프리팹 수정 없이 게임 시작 시 자동 등록됨.
/// 에디터 Play 모드·빌드 실행 파일 양쪽에서 동일하게 동작.
///
/// [사용법]
///  1. 평소처럼 Title → Lobby → 스테이지 진입 (정상 플로우 그대로, 스킵 없음)
///  2. 아무 때나 F9 → 현재 게임 카메라 비활성 + 프리캠 활성 (마우스 우클릭 드래그 시점, WASD/QE 이동)
///  3. 다시 F9 → 프리캠 제거 + 원래 게임 카메라 복귀
///  캐릭터 조작·네트워크 상태와 무관하게 동작 (카메라만 교체하는 방식).
///
/// 제거하려면 이 파일과 TrailerFlyCamera.cs를 삭제하면 됨 — 트레일러/스크린샷 촬영 끝나면 삭제 권장.
/// </summary>
public static class TrailerFlyCameraBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        var runner = new GameObject("~TrailerFlyCameraToggle") { hideFlags = HideFlags.DontSave };
        Object.DontDestroyOnLoad(runner);
        runner.AddComponent<TrailerFlyCameraToggleRunner>();
    }
}

/// <summary>부트스트랩이 생성하는 실제 토글 로직. 직접 씬에 배치할 필요 없음.</summary>
class TrailerFlyCameraToggleRunner : MonoBehaviour
{
    const Key ToggleKey = Key.F9;

    GameObject _flyCamObj;
    Camera _prevCamera;
    AudioListener _prevListener;
    bool _prevListenerEnabled;

    void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard[ToggleKey].wasPressedThisFrame)
        {
            if (_flyCamObj == null) EnableFlyCam();
            else DisableFlyCam();
        }
    }

    void EnableFlyCam()
    {
        _prevCamera = LocalPlayerCamera.Instance != null ? LocalPlayerCamera.Instance.Cam : Camera.main;

        if (_prevCamera != null)
        {
            _prevListener = _prevCamera.GetComponent<AudioListener>();
            if (_prevListener != null)
            {
                _prevListenerEnabled = _prevListener.enabled;
                _prevListener.enabled = false;
            }
            _prevCamera.gameObject.SetActive(false);
        }

        _flyCamObj = new GameObject("TrailerFlyCamera(Temp)");
        var cam = _flyCamObj.AddComponent<Camera>();
        _flyCamObj.AddComponent<AudioListener>();
        _flyCamObj.AddComponent<TrailerFlyCamera>();

        if (_prevCamera != null)
        {
            _flyCamObj.transform.SetPositionAndRotation(
                _prevCamera.transform.position,
                _prevCamera.transform.rotation);
            cam.fieldOfView = _prevCamera.fieldOfView;
        }

        Debug.Log("[TrailerFlyCamera] 활성화 — 우클릭 드래그: 시점, WASD/QE: 이동, F9: 종료");
    }

    void DisableFlyCam()
    {
        if (_flyCamObj != null) Destroy(_flyCamObj);
        _flyCamObj = null;

        if (_prevCamera != null)
        {
            _prevCamera.gameObject.SetActive(true);
            if (_prevListener != null) _prevListener.enabled = _prevListenerEnabled;
        }
        _prevCamera = null;
        _prevListener = null;

        Debug.Log("[TrailerFlyCamera] 비활성화 — 원래 카메라 복귀");
    }
}
