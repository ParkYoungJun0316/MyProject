using System.IO;
using UnityEngine;

/// <summary>
/// Vosk 모델 zip을 StreamingAssets에서 persistentDataPath로 1회 압축 해제.
///
/// [흐름]
/// 1. persistentDataPath/vosk-model-small-en-us-0.15 폴더가 없으면 zip 해제
/// 2. 이미 있으면 경로만 반환 (재설치 없음)
/// 3. 실패 시 null 반환 → CheerKeywordEngine이 에러 처리
///
/// [호출]
/// CheerKeywordEngine.InitModel() 에서만 사용.
/// </summary>
public static class VoskModelLoader
{
    const string ZipName         = "vosk-model-small-en-us-0.15.zip";
    const string ModelFolderName = "vosk-model-small-en-us-0.15";

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
}
