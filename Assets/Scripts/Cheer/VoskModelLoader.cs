using System.IO;
using UnityEngine;
using Vosk;

/// <summary>
/// Vosk 모델 zip을 StreamingAssets에서 persistentDataPath로 1회 압축 해제.
/// 네이티브 Model 인스턴스는 앱 실행 중 1회만 생성해 공유한다.
///
/// [흐름]
/// 1. LoadSync() — 로비 Start에서 메인 스레드 동기 로드 (1회, 이미 로드됐으면 no-op)
/// 2. CheerKeywordEngine.InitCoroutine이 GetSharedModel() 호출 → 캐시 즉시 반환
/// 3. 이후 스폰·리스폰·씬 전환은 캐시된 인스턴스 즉시 반환
/// </summary>
public static class VoskModelLoader
{
    const string ZipName         = "vosk-model-en-us-0.22-lgraph.zip";
    const string ModelFolderName = "vosk-model-en-us-0.22-lgraph";

    static Model _sharedModel;

    /// <summary>
    /// 모델 폴더가 없으면 zip 해제 후 경로 반환.
    /// 이미 있으면 즉시 경로 반환.
    /// 실패 시 null.
    /// </summary>
    public static string EnsureModel()
    {
        string modelPath = Path.Combine(Application.persistentDataPath, ModelFolderName);

        if (Directory.Exists(modelPath))
        {
            Debug.Log($"[VoskModelLoader] 모델 이미 존재: {modelPath}");
            return modelPath;
        }

        string zipPath = Path.Combine(Application.streamingAssetsPath, ZipName);
        if (!File.Exists(zipPath))
        {
            Debug.LogError($"[VoskModelLoader] zip 없음: {zipPath}");
            return null;
        }

        Debug.Log($"[VoskModelLoader] 압축 해제 시작 — {zipPath}");

        try
        {
            using (var zip = Ionic.Zip.ZipFile.Read(zipPath))
            {
                zip.ExtractAll(
                    Application.persistentDataPath,
                    Ionic.Zip.ExtractExistingFileAction.DoNotOverwrite
                );
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[VoskModelLoader] 압축 해제 실패: {e.Message}");
            return null;
        }

        if (!Directory.Exists(modelPath))
        {
            Debug.LogError($"[VoskModelLoader] 해제 후 폴더 없음: {modelPath}");
            return null;
        }

        Debug.Log($"[VoskModelLoader] 압축 해제 완료: {modelPath}");
        return modelPath;
    }

    /// <summary>
    /// 메인 스레드에서 Model을 동기 로드한다. 로비 Start에서 1회 호출.
    /// 이미 로드된 경우 즉시 반환 (no-op).
    /// </summary>
    public static void LoadSync()
    {
        if (_sharedModel != null) return;

        string modelPath = EnsureModel();
        if (modelPath == null)
        {
            Debug.LogError("[VoskModelLoader] 모델 경로 확인 실패 — 로드 중단");
            return;
        }

        Vosk.Vosk.SetLogLevel(0);
        _sharedModel = new Model(modelPath);
        Debug.Log("[VoskModelLoader] 공유 Model 동기 로드 완료");
    }

    /// <summary>
    /// 캐시된 Model 반환.
    /// LoadSync 미호출 시 동기 fallback 로드 (에디터 직접 Play 등).
    /// </summary>
    public static Model GetSharedModel()
    {
        if (_sharedModel != null) return _sharedModel;

        string modelPath = EnsureModel();
        if (modelPath == null) return null;

        // libvosk 네이티브 라이브러리가 빌드에 없으면 DllNotFoundException이 던져진다.
        // 동기 호출(LobbySlotUI.Refresh 등)에서 이 예외가 안 잡히면 호출자의 루프 전체가
        // 끊기므로(로비 슬롯 갱신 중단 등), 여기서 흡수하고 모델 없음(null)으로 취급한다.
        try
        {
            Vosk.Vosk.SetLogLevel(0);
            _sharedModel = new Model(modelPath);
            Debug.Log("[VoskModelLoader] 공유 Model 동기 로드 완료 (fallback)");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[VoskModelLoader] 공유 Model 로드 실패 (libvosk 누락 등) — {e.Message}");
            _sharedModel = null;
        }
        return _sharedModel;
    }
}
