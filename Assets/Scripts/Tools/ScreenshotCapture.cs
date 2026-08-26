using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using Debug = UnityEngine.Debug;

/// <summary>
/// 스크린샷 촬영용 고해상도 캡처 툴 (디버그/마케팅 툴 — 게임플레이 로직과 무관).
/// 화면 해상도보다 superSize 배율만큼 크게 렌더링해서 PNG로 저장한다.
///
/// [사용법]
/// 1) 빈 GameObject에 이 컴포넌트를 붙여 씬에 배치 (ScreenshotFreeCamera / ScreenshotPauseController와
///    같은 오브젝트에 둬도 무방).
/// 2) Play 모드에서 captureKey(기본 F6)를 누르면 현재 화면을 즉시 캡처.
/// 3) 저장 위치: 프로젝트 루트의 Screenshots/ 폴더 (Assets 밖 — git에 안 걸리고 Unity가 임포트하지 않음).
///    파일명: yyyyMMdd_HHmmss_fff.png (밀리초까지 포함 — 연속 캡처 시 덮어쓰기 방지)
///
/// [Steam 오버레이(F12) 대신 이 방식을 쓰는 이유]
/// - 오버레이 알림/토스트 없이 순수 게임 화면만 저장됨.
/// - superSize로 실제 화면 해상도보다 큰 원본을 얻을 수 있어(Steam 권장 1920x1080 이상) 후보정 여유가 생김.
///
/// [주의]
/// - ScreenCapture.CaptureScreenshot은 화면 백버퍼를 읽으므로 Play 모드에서만 동작한다(에디트 모드 불가).
/// - Time.timeScale = 0(ScreenshotPauseController로 일시정지)이어도 현재 프레임 그대로 캡처된다.
/// - 순수 디버그 툴. 게임 로직에는 관여하지 않는다.
/// </summary>
public class ScreenshotCapture : MonoBehaviour
{
    [Header("Key")]
    [Tooltip("캡처 실행 키")]
    [SerializeField] Key captureKey = Key.F6;

    [Header("Resolution")]
    [Tooltip("화면 해상도 대비 배율. 2~4 권장 (Steam 권장 1920x1080 이상을 여유 있게 넘기기 위함)")]
    [SerializeField, Range(1, 4)] int superSize = 2;

    [Header("Save")]
    [Tooltip("저장 폴더 이름 (프로젝트 루트 기준, Assets 폴더 밖에 생성)")]
    [SerializeField] string folderName = "Screenshots";

    [Tooltip("캡처 직후 탐색기(Windows)로 저장 폴더를 열고 방금 찍은 파일을 선택 표시. " +
             "저장 폴더가 Assets 밖이라 Unity Project 창에는 안 보이기 때문에 확인용으로 켜둠.")]
    [SerializeField] bool openFolderAfterCapture = true;

    string FolderPath => Path.Combine(Directory.GetParent(Application.dataPath).FullName, folderName);

    void Update()
    {
        if (Keyboard.current == null) return;
        if (Keyboard.current[captureKey].wasPressedThisFrame)
            Capture();
    }

    void Capture()
    {
        string folder = FolderPath;
        Directory.CreateDirectory(folder);

        string fileName = $"{DateTime.Now:yyyyMMdd_HHmmss_fff}.png";
        string fullPath = Path.Combine(folder, fileName);

        ScreenCapture.CaptureScreenshot(fullPath, superSize);
        Debug.Log($"[ScreenshotCapture] 캡처 완료 — {fullPath} (superSize x{superSize})");

        if (openFolderAfterCapture)
            StartCoroutine(OpenFolderAndSelect(fullPath));
    }

    IEnumerator OpenFolderAndSelect(string path)
    {
        // ScreenCapture.CaptureScreenshot은 비동기로 디스크에 씀 — 파일이 실제로 생기기까지 잠깐 대기.
        yield return new WaitForSecondsRealtime(0.5f);

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        try
        {
            Process.Start("explorer.exe", $"/select,\"{path}\"");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[ScreenshotCapture] 탐색기 열기 실패 — {e.Message}");
        }
#endif
    }
}
