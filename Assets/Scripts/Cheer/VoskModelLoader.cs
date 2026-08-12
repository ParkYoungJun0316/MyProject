using System;
using System.IO;
using UnityEngine;
using Vosk;

/// <summary>
/// StreamingAssets에 배포된 Vosk 모델 폴더를 열어 네이티브 Model 인스턴스를 1회 생성해 공유한다.
///
/// [흐름]
/// 1. LoadSync() — 로비 Start에서 메인 스레드 동기 로드 (1회, 실패해도 재시도하지 않음)
/// 2. CheerKeywordEngine.InitCoroutine이 GetSharedModel() 호출 → 캐시 즉시 반환
/// 3. 이후 스폰·리스폰·씬 전환은 캐시된 인스턴스 즉시 반환
///
/// 모델 로드에 실패하면 음성 인식만 비활성화되고 게임 진행은 영향받지 않는다.
/// </summary>
public static class VoskModelLoader
{
    const string ModelFolderName = "vosk-model-en-us-0.22-lgraph";

    /// <summary>
    /// 모델 무결성 검증용 필수 파일. 하나라도 없거나 크기가 기준 미달이면 손상으로 간주한다.
    /// libvosk는 모델이 불완전해도 예외 없이 NULL 핸들을 돌려주므로 로드 전에 걸러야 한다.
    /// </summary>
    static readonly (string rel, long minBytes)[] RequiredFiles =
    {
        ("am/final.mdl",       60_000_000),
        ("am/tree",               500_000),
        ("conf/mfcc.conf",            100),
        ("conf/model.conf",           100),
        ("graph/HCLr.fst",     50_000_000),
        ("graph/Gr.fst",       40_000_000),
        ("graph/words.txt",     4_000_000),
        ("graph/phones.txt",          500),
        ("ivector/final.ie",   15_000_000),
        ("ivector/final.dubm",    100_000),
    };

    static Model  _sharedModel;
    static bool   _loadAttempted;
    static string _resolvedPath;

    /// <summary>
    /// 메인 스레드에서 Model을 동기 로드한다. 로비 Start에서 1회 호출.
    /// 이미 시도했으면 즉시 반환 (성공/실패 무관 — 실패 재시도는 하지 않는다).
    /// </summary>
    public static void LoadSync()
    {
        if (_loadAttempted) return;
        _loadAttempted = true;

        string modelPath = ResolveModelPath();
        if (modelPath == null)
        {
            Debug.LogError("[VoskModelLoader] 모델 경로 확인 실패 — 음성 인식 비활성화");
            return;
        }

        Model model;
        try
        {
            Vosk.Vosk.SetLogLevel(0);
            model = new Model(modelPath);
        }
        catch (Exception e)
        {
            Debug.LogError($"[VoskModelLoader] Model 생성 예외 (libvosk 누락 등) — {e.Message}");
            return;
        }

        // new Model()은 실패해도 예외를 던지지 않고 네이티브 핸들이 NULL인 객체를 돌려준다.
        // 그 상태로 vosk_model_find_word / VoskRecognizer를 호출하면 프로세스가 즉사하므로
        // 핸들을 직접 확인해 실패를 걸러낸다.
        if (Model.getCPtr(model).Handle == IntPtr.Zero)
        {
            Debug.LogError($"[VoskModelLoader] 네이티브 Model 생성 실패 (핸들 NULL) — 음성 인식 비활성화. path={modelPath}");
            return;
        }

        _sharedModel = model;
        Debug.Log($"[VoskModelLoader] 공유 Model 로드 완료: {modelPath}");
    }

    /// <summary>
    /// 캐시된 Model 반환. 아직 로드 시도 전이면 동기 로드 (에디터 직접 Play 등).
    /// 로드에 실패한 경우 null — 호출자는 음성 인식 없이 동작해야 한다.
    /// </summary>
    public static Model GetSharedModel()
    {
        if (_sharedModel == null && !_loadAttempted) LoadSync();
        return _sharedModel;
    }

    /// <summary>
    /// 사용할 모델 폴더 경로를 확정한다. 검증 실패 시 null.
    /// </summary>
    static string ResolveModelPath()
    {
        if (_resolvedPath != null) return _resolvedPath;

        string shipped = Path.Combine(Application.streamingAssetsPath, ModelFolderName);
        if (!ValidateModel(shipped, out string reason))
        {
            Debug.LogError($"[VoskModelLoader] 배포 모델 검증 실패 — {reason} / path={shipped}");
            return null;
        }

        // libvosk(Kaldi)는 std::ifstream으로 모델 파일을 열기 때문에 Windows에서 비ASCII 경로를
        // 열지 못하고, 실패를 알리지 않은 채 이후 네이티브 호출에서 프로세스를 죽인다.
        // (alphacep/vosk-api#1072 — 업스트림 미해결) 설치 경로에 비ASCII가 섞이면 ASCII 경로로 복사한다.
        if (IsAscii(shipped))
        {
            _resolvedPath = shipped;
            return _resolvedPath;
        }

        Debug.LogWarning($"[VoskModelLoader] 설치 경로에 비ASCII 문자 포함 — ASCII 경로로 복사 시도. path={shipped}");
        _resolvedPath = CopyToAsciiPath(shipped);
        return _resolvedPath;
    }

    /// <summary>
    /// 필수 파일 존재와 최소 크기를 확인한다. 폴더 존재만으로는 판단하지 않는다.
    /// </summary>
    static bool ValidateModel(string root, out string reason)
    {
        if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
        {
            reason = "폴더 없음";
            return false;
        }

        foreach (var (rel, minBytes) in RequiredFiles)
        {
            var fi = new FileInfo(Path.Combine(root, rel.Replace('/', Path.DirectorySeparatorChar)));
            if (!fi.Exists)
            {
                reason = $"파일 없음: {rel}";
                return false;
            }
            if (fi.Length < minBytes)
            {
                reason = $"파일 크기 미달: {rel} ({fi.Length}B < {minBytes}B)";
                return false;
            }
        }

        reason = null;
        return true;
    }

    /// <summary>
    /// 모델을 ASCII 전용 경로(공용 문서 폴더)로 복사하고 그 경로를 반환한다. 실패 시 null.
    /// 설치 경로에 비ASCII가 섞인 드문 경우에만 실행된다.
    /// </summary>
    static string CopyToAsciiPath(string src)
    {
        string root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments),
            "KkulTteok");

        if (!IsAscii(root))
        {
            Debug.LogError($"[VoskModelLoader] 폴백 경로도 비ASCII — 음성 인식 비활성화. path={root}");
            return null;
        }

        string dst = Path.Combine(root, ModelFolderName);
        if (ValidateModel(dst, out _))
        {
            Debug.Log($"[VoskModelLoader] ASCII 경로 복사본 재사용: {dst}");
            return dst;
        }

        try
        {
            Directory.CreateDirectory(dst);
            foreach (string dir in Directory.GetDirectories(src, "*", SearchOption.AllDirectories))
                Directory.CreateDirectory(dst + dir.Substring(src.Length));
            foreach (string file in Directory.GetFiles(src, "*", SearchOption.AllDirectories))
                File.Copy(file, dst + file.Substring(src.Length), true);
        }
        catch (Exception e)
        {
            Debug.LogError($"[VoskModelLoader] ASCII 경로 복사 실패 — {e.Message}");
            return null;
        }

        if (!ValidateModel(dst, out string reason))
        {
            Debug.LogError($"[VoskModelLoader] 복사 후 검증 실패 — {reason} / path={dst}");
            return null;
        }

        Debug.Log($"[VoskModelLoader] ASCII 경로 복사 완료: {dst}");
        return dst;
    }

    static bool IsAscii(string s)
    {
        for (int i = 0; i < s.Length; i++)
            if (s[i] > 0x7F) return false;
        return true;
    }
}
