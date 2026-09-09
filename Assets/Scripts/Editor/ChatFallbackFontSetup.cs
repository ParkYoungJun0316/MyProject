#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

/// <summary>
/// 채팅(InGameChatUI)처럼 어떤 문자가 올지 미리 알 수 없는 텍스트를 위한 Dynamic 폰트를 만들고,
/// TMP Settings 글로벌 Fallback 목록 맨 뒤에 등록한다. 폰트 명단은 <see cref="NotoFontRoster"/>.
///
/// [왜 메인 폰트와 별도 에셋인가]
/// 메인 UI 폰트는 <see cref="NotoFontStaticBaker"/>로 Static 전환했다 — 런타임에 되쓰지 않아
/// "Importer generated inconsistent result" 재임포트 churn(폰트 깨짐의 근본 원인)이 사라졌다.
/// 채팅은 사전 베이킹으로 못 덮으니 Dynamic이 필요한데, 메인 폰트를 다시 Dynamic으로 되돌리면
/// 그 버그가 UI 전체에 재발한다. 그래서 "채팅 전용" Dynamic 폰트를 별도로 만들어 Fallback 맨
/// 뒤에만 둔다 — 평소 UI 텍스트는 Static 폰트로 커버되어 이 경로를 타지 않고, 채팅에서 사전
/// 베이킹에 없는 문자가 나올 때만 이 폰트가 그때 굽는다.
///
/// [Fallback 순서가 중요한 이유]
/// TMP는 글리프가 없을 때 이 목록을 앞에서부터 훑는다. Static 메인 폰트가 앞에 있어야 평소
/// 텍스트가 Dynamic 경로를 타지 않는다. 그래서 채팅 폰트는 항상 뒤에 붙이고,
/// 검사 메뉴가 순서가 뒤집혔는지 확인해준다.
/// </summary>
public static class ChatFallbackFontSetup
{
    const string OutputFolder = "Assets/Font/Noto/Chat";
    const int AtlasSize = 1024; // 채팅 누적량이 늘면 멀티 아틀라스로 페이지가 자동 증설된다

    // ── 메뉴 ──────────────────────────────────────────────────────

    [MenuItem("Tools/Font/채팅용 Dynamic Fallback 생성 + 등록")]
    static void CreateAndRegister()
    {
        if (!AssetDatabase.IsValidFolder(OutputFolder))
            AssetDatabase.CreateFolder(Path.GetDirectoryName(OutputFolder).Replace('\\', '/'),
                                       Path.GetFileName(OutputFolder));

        var chatFonts = new List<TMP_FontAsset>();

        foreach (NotoFontRoster.Entry entry in NotoFontRoster.Entries)
        {
            var chatFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(entry.chatAssetPath);

            if (chatFont == null)
                chatFont = Create(entry);

            if (chatFont == null) continue;

            // 이미 있던 에셋도 매번 원하는 설정으로 맞춘다(멱등) — 예전 실행이 다른 설정으로
            // 만들어놨을 때 재실행만으로 교정되도록.
            ApplySettings(chatFont, entry);
            chatFonts.Add(chatFont);
        }

        RegisterFallbacks(chatFonts);
    }

    [MenuItem("Tools/Font/채팅용 Dynamic Fallback - 등록 상태 검사")]
    static void Inspect()
    {
        List<TMP_FontAsset> list = TMP_Settings.fallbackFontAssets;
        if (list == null)
        {
            Debug.LogError("[ChatFallbackFontSetup] TMP Settings.fallbackFontAssets가 null입니다.");
            return;
        }

        Debug.Log("[ChatFallbackFontSetup] TMP Settings 글로벌 Fallback 목록 (순서대로):\n" +
                  string.Join("\n", list.Select((f, i) =>
                      $"  {i}: {(f != null ? $"{f.name} [{f.atlasPopulationMode}]" : "(null)")}")));

        WarnIfOrderInverted(list);
    }

    /// <summary>
    /// 채팅 폰트에 굽힌 글리프를 비운다. ClearDynamicDataOnBuild는 빌드 시점에만 비우므로,
    /// 에디터에서 채팅을 치다 보면 에셋이 dirty로 남아 git diff가 더러워진다 — 커밋 전 정리용.
    /// </summary>
    [MenuItem("Tools/Font/채팅용 Dynamic Fallback - 굽힌 데이터 비우기")]
    static void ClearBakedData()
    {
        int cleared = 0;

        foreach (NotoFontRoster.Entry entry in NotoFontRoster.Entries)
        {
            var chatFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(entry.chatAssetPath);
            if (chatFont == null) continue;

            chatFont.ClearFontAssetData(false);
            EditorUtility.SetDirty(chatFont);
            cleared++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[ChatFallbackFontSetup] 채팅 폰트 {cleared}개의 굽힌 데이터를 비웠습니다.");
    }

    // ── 생성 · 설정 ───────────────────────────────────────────────

    /// <summary>
    /// TMP의 공개 팩토리 메서드로 생성한다. 내부 전용(internal) 필드(atlasWidth 세터,
    /// sourceFontFile 세터, freeGlyphRects 등)를 직접 흉내내려 하면 우리 어셈블리에서
    /// 컴파일 에러가 난다 — TMP_FontAsset.CreateFontAsset이 그 초기화를 전부 대신 해준다.
    /// </summary>
    static TMP_FontAsset Create(NotoFontRoster.Entry entry)
    {
        var sourceFont = AssetDatabase.LoadAssetAtPath<Font>(entry.sourceFontPath);
        if (sourceFont == null)
        {
            Debug.LogError($"[ChatFallbackFontSetup] 소스 폰트를 찾을 수 없습니다 — {entry.sourceFontPath}");
            return null;
        }

        TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(
            sourceFont, 90, 9, GlyphRenderMode.SDFAA, AtlasSize, AtlasSize,
            AtlasPopulationMode.Dynamic, enableMultiAtlasSupport: true);

        if (fontAsset == null)
        {
            Debug.LogError($"[ChatFallbackFontSetup] 폰트 에셋 생성 실패 — {sourceFont.name} " +
                           "(Font Import Settings의 \"Include Font Data\" 확인 필요).", sourceFont);
            return null;
        }

        string assetName = entry.ChatLabel;
        fontAsset.name = assetName;

        AssetDatabase.CreateAsset(fontAsset, entry.chatAssetPath);

        if (fontAsset.material != null)
        {
            fontAsset.material.name = assetName + " Material";
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
        }

        if (fontAsset.atlasTextures != null && fontAsset.atlasTextures.Length > 0 && fontAsset.atlasTextures[0] != null)
        {
            fontAsset.atlasTextures[0].name = assetName + " Atlas";
            AssetDatabase.AddObjectToAsset(fontAsset.atlasTextures[0], fontAsset);
        }

        Debug.Log($"[ChatFallbackFontSetup] 생성 — {entry.chatAssetPath} " +
                  $"(Dynamic, {AtlasSize}x{AtlasSize} 시작, 자동 증설).", fontAsset);
        return fontAsset;
    }

    static void ApplySettings(TMP_FontAsset chatFont, NotoFontRoster.Entry entry)
    {
        // 원본 폰트 참조를 복원하는 세터를 타야 하므로 SerializedObject가 아니라 프로퍼티로 설정.
        if (chatFont.atlasPopulationMode != AtlasPopulationMode.Dynamic)
        {
            chatFont.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            Debug.LogWarning($"[ChatFallbackFontSetup] {entry.ChatLabel} — Dynamic이 아니어서 되돌렸습니다. " +
                             "채팅 폰트가 Static이면 처음 보는 문자를 못 그립니다.", chatFont);
        }

        chatFont.isMultiAtlasTexturesEnabled = true;

        // clearDynamicDataOnBuild의 세터가 internal이라 SerializedObject로 설정한다.
        // 채팅 폰트는 true가 맞다 — 굽힌 글자가 에셋에 남으면 빌드마다 그때그때 입력된 잡다한
        // 문자가 실려 가고 git diff도 계속 더러워진다. 매 실행 새로 구워도 무방한 데이터다.
        // (메인 폰트는 지켜야 할 사전 베이킹 데이터가 있어서 반대로 false다.)
        var so = new SerializedObject(chatFont);
        so.FindProperty("m_ClearDynamicDataOnBuild").boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(chatFont);
    }

    // ── TMP Settings 등록 ─────────────────────────────────────────

    /// <summary>기존 목록에 이미 있으면 건너뛰고, 글로벌 Fallback 맨 뒤에 순서대로 추가.</summary>
    static void RegisterFallbacks(List<TMP_FontAsset> chatFonts)
    {
        // 게임이 실제로 쓰는 인스턴스에서 역으로 에셋을 얻는다 — 경로를 하드코딩하면 프로젝트에
        // TMP Settings가 둘일 때 "엉뚱한 에셋에 쓰고 검사만 통과하는" 형태로 조용히 갈라진다.
        TMP_Settings settings = TMP_Settings.instance;
        if (settings == null)
        {
            Debug.LogError("[ChatFallbackFontSetup] TMP Settings를 찾을 수 없습니다 " +
                           "(Resources/TMP Settings). TMP Essential Resources를 임포트했는지 확인하세요.");
            return;
        }

        var so = new SerializedObject(settings);
        SerializedProperty listProp = so.FindProperty("m_fallbackFontAssets");

        var existing = new HashSet<Object>();
        for (int i = 0; i < listProp.arraySize; i++)
        {
            Object obj = listProp.GetArrayElementAtIndex(i).objectReferenceValue;
            if (obj != null) existing.Add(obj);
        }

        int added = 0;
        foreach (TMP_FontAsset font in chatFonts)
        {
            if (!existing.Add(font)) continue;

            int newIndex = listProp.arraySize;
            listProp.InsertArrayElementAtIndex(newIndex);
            listProp.GetArrayElementAtIndex(newIndex).objectReferenceValue = font;
            added++;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();

        Debug.Log($"[ChatFallbackFontSetup] 완료 — 채팅 폰트 {chatFonts.Count}개 확인, " +
                  $"{added}개 새로 등록 (나머지는 이미 등록됨). 대상: {AssetDatabase.GetAssetPath(settings)}");

        WarnIfOrderInverted(TMP_Settings.fallbackFontAssets);
    }

    /// <summary>Static 메인 폰트보다 앞에 놓인 채팅 폰트가 있으면 경고.</summary>
    static void WarnIfOrderInverted(List<TMP_FontAsset> list)
    {
        if (list == null) return;

        var chatPaths = new HashSet<string>(NotoFontRoster.Entries.Select(e => e.chatAssetPath));
        var mainPaths = new HashSet<string>(NotoFontRoster.Entries.Select(e => e.mainAssetPath));

        int firstChat = -1, lastMain = -1;
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] == null) continue;
            string path = AssetDatabase.GetAssetPath(list[i]);

            if (firstChat < 0 && chatPaths.Contains(path)) firstChat = i;
            if (mainPaths.Contains(path)) lastMain = i;
        }

        if (firstChat >= 0 && lastMain > firstChat)
            Debug.LogWarning($"[ChatFallbackFontSetup] Fallback 순서가 뒤집혔습니다 — 채팅 Dynamic 폰트가 " +
                             $"인덱스 {firstChat}에 있는데 Static 메인 폰트가 {lastMain}에 있습니다. " +
                             "채팅 폰트를 목록 맨 뒤로 옮기세요(그대로 두면 평소 UI 텍스트도 Dynamic 경로를 탑니다).");
    }
}
#endif
