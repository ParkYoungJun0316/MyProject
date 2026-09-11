#if UNITY_EDITOR
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

/// <summary>
/// Noto 폰트 에셋을 Static 아틀라스로 사전 베이킹하는 에디터 툴. 폰트 명단은 <see cref="NotoFontRoster"/>.
///
/// [왜 필요한가]
/// 폰트가 Dynamic이면 플레이 중 새 글자를 아틀라스에 굽고, TMP가 그 에셋을 재임포트 큐에 넣어
/// (TMP_EditorResourceManager) 계속 다시 임포트한다. 그 결과 Unity 애셋 파이프라인이
/// "Importer generated inconsistent result"를 내고, 메모리/디스크/빌드의 폰트가 어긋나
/// 에디터·빌드 양쪽에서 텍스트가 깨진다. Static이면 런타임 되쓰기가 없어 이 경로가 사라진다.
///
/// [베이킹 문자 집합]
/// 공통(모든 폰트): ASCII + Latin-1 Supplement + 자주 쓰는 기호
/// 폰트별: 담당 스크립트 범위(Latin Ext-A / 키릴 / 한글 자모 / 카나 / CJK 문장부호)
///        + 담당 로케일 String Table에 등장하는 문자 전량
///        + 담당 로케일의 언어 드롭다운 표시명 문자(<see cref="LocaleDisplayName"/>)
///
/// [주의 — 워크플로]
/// Static은 굽지 않은 문자를 렌더링하지 못한다. 따라서 <b>대사·UI 텍스트를 추가하거나 번역을
/// 갱신하면 이 툴을 다시 실행해야 한다.</b> 채팅처럼 런타임에 뭐가 올지 모르는 텍스트는
/// <see cref="ChatFallbackFontSetup"/>이 만든 Dynamic 폴백이 담당한다.
/// 그 폴백이 베이킹 누락까지 조용히 가려주기 때문에, 아래 경고 로그를 무시하지 말 것.
/// </summary>
public static class NotoFontStaticBaker
{
    const string StringTableFolder = "Assets/Localization/StringTables";
    const int AtlasSize = 4096;

    /// <summary>프리플라이트에서 원본 폰트를 열어볼 때 쓰는 크기. 실제 굽기는 에셋의 faceInfo를 따른다.</summary>
    const int PreflightPointSize = 90;

    static readonly (int from, int to)[] CommonRanges =
    {
        (0x0020, 0x007E), // ASCII 출력 가능 문자
        (0x00A0, 0x00FF), // Latin-1 Supplement
    };

    static readonly int[] CommonExtras =
    {
        0x2013, 0x2014,                     // – —
        0x2018, 0x2019, 0x201C, 0x201D,     // ‘ ’ “ ”
        0x2026,                             // …
        0x20A9, 0x20AC,                     // ₩ €
        0x2192, 0x2190,                     // → ←
    };

    // ── 메뉴 ──────────────────────────────────────────────────────

    [MenuItem("Tools/Font/Noto Static 베이킹 - 검사만")]
    static void Inspect()
    {
        Dictionary<string, List<string>> tables = DiscoverLocaleTables();
        WarnLocaleMismatches(tables.Keys);

        foreach (NotoFontRoster.Entry entry in NotoFontRoster.Entries)
        {
            HashSet<int> codes = BuildCodepoints(entry, tables);
            int cjk = codes.Count(c => c >= 0x2E80);
            Debug.Log($"[FontBaker] {entry.MainLabel} — 굽을 문자 {codes.Count}자 (CJK·한글 {cjk}자). " +
                      $"아틀라스 {AtlasSize}x{AtlasSize} 예상 페이지 {EstimatePages(entry.mainAssetPath, codes.Count)}장.");
        }
    }

    [MenuItem("Tools/Font/Noto Static 베이킹 - 실행")]
    static void BakeAll()
    {
        Debug.Log("[FontBaker] Noto 폰트 5종을 Static 아틀라스로 다시 굽습니다 (기존 동적 데이터는 초기화).");

        Dictionary<string, List<string>> tables = DiscoverLocaleTables();
        WarnLocaleMismatches(tables.Keys);

        int succeeded = 0;
        var failedLabels = new List<string>();

        try
        {
            for (int i = 0; i < NotoFontRoster.Entries.Length; i++)
            {
                NotoFontRoster.Entry entry = NotoFontRoster.Entries[i];
                EditorUtility.DisplayProgressBar("Noto Static 베이킹", entry.MainLabel,
                    (float)i / NotoFontRoster.Entries.Length);

                if (Bake(entry, tables)) succeeded++;
                else failedLabels.Add(entry.MainLabel);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        if (failedLabels.Count > 0)
            Debug.LogError($"[FontBaker] {succeeded}개 성공 / {failedLabels.Count}개 실패 — " +
                           $"실패: {string.Join(", ", failedLabels)}. 위 에러를 먼저 해결하고 다시 실행하세요.");
        else
            Debug.Log($"[FontBaker] 완료 ({succeeded}개). 대사·UI 텍스트를 추가하면 이 툴을 다시 실행하세요.");
    }

    // ── 베이킹 ────────────────────────────────────────────────────

    /// <returns>Static으로 굳히는 데까지 성공했는지.</returns>
    static bool Bake(NotoFontRoster.Entry entry, Dictionary<string, List<string>> tables)
    {
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(entry.mainAssetPath);
        if (font == null)
        {
            Debug.LogError($"[FontBaker] 폰트 에셋을 찾을 수 없습니다 — {entry.mainAssetPath}");
            return false;
        }

        // ── 프리플라이트 ──
        // 기존 데이터를 지우기 "전에" 원본 폰트가 실제로 열리는지 확인한다. 이 검사 없이 지우고
        // 굽다가 실패하면, 멀쩡했던 아틀라스만 날리고 빈 폰트가 남는다.
        var sourceFont = AssetDatabase.LoadAssetAtPath<Font>(entry.sourceFontPath);
        if (sourceFont == null)
        {
            Debug.LogError($"[FontBaker] {entry.MainLabel} — 원본 폰트 파일을 찾을 수 없어 중단합니다 " +
                           $"({entry.sourceFontPath}). 기존 아틀라스는 건드리지 않았습니다.");
            return false;
        }

        FontEngine.InitializeFontEngine();
        if (FontEngine.LoadFontFace(sourceFont, PreflightPointSize) != FontEngineError.Success)
        {
            Debug.LogError($"[FontBaker] {entry.MainLabel} — 원본 폰트를 열 수 없어 중단합니다. " +
                           "Font Import Settings의 \"Include Font Data\"를 확인하세요. " +
                           "기존 아틀라스는 건드리지 않았습니다.", sourceFont);
            return false;
        }

        HashSet<int> codes = BuildCodepoints(entry, tables);
        uint[] unicodes = codes.OrderBy(c => c).Select(c => (uint)c).ToArray();

        // TryAddCharacters는 Dynamic에서만 동작하고, Dynamic으로 되돌려야 원본 폰트 참조가 복원된다.
        font.atlasPopulationMode = AtlasPopulationMode.Dynamic;

        // 아틀라스 크기는 setter가 internal이라 SerializedObject로 설정한다.
        // ClearFontAssetData가 page 0을 이 크기로 재초기화하므로 반드시 초기화 "전에" 지정해야 한다.
        var so = new SerializedObject(font);
        so.FindProperty("m_AtlasWidth").intValue = AtlasSize;
        so.FindProperty("m_AtlasHeight").intValue = AtlasSize;
        so.FindProperty("m_IsMultiAtlasTexturesEnabled").boolValue = true;
        so.FindProperty("m_ClearDynamicDataOnBuild").boolValue = false;
        so.ApplyModifiedPropertiesWithoutUndo();

        font.ClearFontAssetData(false);

        // 반환값(added)은 신뢰하지 않는다 — TMP_FontAsset.TryAddCharacters는
        // "allGlyphsAddedToTexture && !isMissingCharacters"를 반환하므로, 요청한 426자 중 딱
        // 1~2자만 이 폰트에 없어도(흔한 정상 상황 — 예: 라틴 폰트에 ₩ 기호가 없는 경우) false가
        // 나온다. 그걸 "폰트 페이스 자체를 못 열어 전량 실패"와 똑같이 취급해 멈추면, 424자가
        // 멀쩡한데도 매번 Dynamic으로 되돌리게 된다(실제로 겪음). 진짜 재앙의 신호는 딱 하나 —
        // 프리플라이트를 통과했는데도 요청한 글자가 "전부" 빠지는 경우(missingCount ==
        // unicodes.Length)뿐이다. 그 외의 부분 누락은 missingNote로 알리고 정상 진행한다.
        font.TryAddCharacters(unicodes, out uint[] missing, includeFontFeatures: true);
        int missingCount = missing?.Length ?? 0;

        if (unicodes.Length > 0 && missingCount >= unicodes.Length)
        {
            Debug.LogError($"[FontBaker] {entry.MainLabel} — 베이킹 실패로 Static 전환을 건너뜁니다 " +
                           $"(요청 {unicodes.Length}자 전량 실패). 폰트는 Dynamic 상태로 남겨뒀습니다.", font);
            return false;
        }

        // Static으로 굳히면 TMP가 더는 이 에셋에 되쓰지 않는다(원본 폰트 참조도 해제됨).
        // 재베이크는 여전히 가능하다 — m_SourceFontFileGUID가 남아 LoadFontFace가 지연 복구한다.
        font.atlasPopulationMode = AtlasPopulationMode.Static;

        UpdateMainMaterial(font, entry);
        EditorUtility.SetDirty(font);

        string missingNote = missingCount > 0
            ? $" / 원본 폰트에 없어 건너뜀 {missingCount}자: {DescribeCodepoints(missing)}"
            : "";
        Debug.Log($"[FontBaker] {entry.MainLabel} — 요청 {unicodes.Length}자, 아틀라스 페이지 " +
                  $"{font.atlasTextures?.Length ?? 0}장{missingNote}.", font);
        return true;
    }

    /// <summary>
    /// 아틀라스 크기가 바뀌면 SDF 셰이더가 쓰는 머티리얼 파라미터도 같이 갱신해야 한다
    /// (_TextureWidth/_TextureHeight가 실제 아틀라스와 어긋나면 안티에일리어싱·두께가 틀어진다).
    /// 주 머티리얼만 손댄다 — 예전엔 서브에셋 머티리얼 전부에 page 0을 꽂아서, 페이지별·프리셋
    /// 머티리얼이 생기는 순간 잘못된 아틀라스를 가리키게 될 구조였다.
    /// </summary>
    static void UpdateMainMaterial(TMP_FontAsset font, NotoFontRoster.Entry entry)
    {
        Material mat = font.material;
        if (mat == null)
        {
            Debug.LogWarning($"[FontBaker] {entry.MainLabel} — 주 머티리얼이 없어 셰이더 파라미터를 갱신하지 못했습니다.", font);
            return;
        }

        Texture2D atlas = font.atlasTextures != null && font.atlasTextures.Length > 0
            ? font.atlasTextures[0]
            : null;

        if (atlas != null) mat.SetTexture("_MainTex", atlas);
        mat.SetFloat("_TextureWidth", font.atlasWidth);
        mat.SetFloat("_TextureHeight", font.atlasHeight);
        mat.SetFloat("_GradientScale", font.atlasPadding + 1);
        EditorUtility.SetDirty(mat);

        int materialCount = AssetDatabase.LoadAllAssetsAtPath(entry.mainAssetPath).Count(o => o is Material);
        if (materialCount > 1)
            Debug.LogWarning($"[FontBaker] {entry.MainLabel} — 머티리얼 서브에셋이 {materialCount}개입니다. " +
                             "주 머티리얼만 갱신했으니 나머지의 _TextureWidth/_TextureHeight는 직접 확인하세요.", font);
    }

    // ── 로케일 발견 ───────────────────────────────────────────────

    /// <summary>
    /// 로케일 코드 → 그 로케일의 String Table 파일들. 어떤 로케일이 존재하는지의 SSOT는
    /// 하드코딩 목록이 아니라 이 폴더다 — 로케일을 추가하고 로스터를 안 고치면 그 언어가 조용히
    /// 안 구워지는 사고를 막기 위해, 파일에서 읽고 <see cref="WarnLocaleMismatches"/>로 대조한다.
    /// </summary>
    static Dictionary<string, List<string>> DiscoverLocaleTables()
    {
        var tables = new Dictionary<string, List<string>>();

        if (!Directory.Exists(StringTableFolder))
        {
            Debug.LogWarning($"[FontBaker] String Table 폴더가 없습니다 — {StringTableFolder}. " +
                             "대사·UI 문자를 수집하지 못하고 공통 범위만 굽습니다.");
            return tables;
        }

        foreach (string path in Directory.GetFiles(StringTableFolder, "*.asset"))
        {
            string name = Path.GetFileNameWithoutExtension(path);

            // "Dialogue_zh-Hans" → "zh-Hans". 접미사가 없는 "Dialogue Shared Data" 류는 건너뛴다.
            int sep = name.LastIndexOf('_');
            if (sep < 0 || sep == name.Length - 1) continue;

            string code = name.Substring(sep + 1);
            if (code.Contains(' ')) continue;

            if (!tables.TryGetValue(code, out List<string> files))
                tables[code] = files = new List<string>();
            files.Add(path);
        }

        return tables;
    }

    /// <summary>로스터와 실제 파일이 어긋나면 경고. 조용한 누락을 눈에 보이게 만드는 게 목적.</summary>
    static void WarnLocaleMismatches(IEnumerable<string> presentCodes)
    {
        var present  = new HashSet<string>(presentCodes);
        var assigned = new HashSet<string>(NotoFontRoster.Entries.SelectMany(e => e.localeCodes));

        foreach (string code in present.Where(c => !assigned.Contains(c)).OrderBy(c => c))
            Debug.LogWarning($"[FontBaker] '{code}' String Table이 있는데 어떤 폰트에도 배정되지 않았습니다 — " +
                             "NotoFontRoster에 추가하지 않으면 그 언어 문자가 구워지지 않습니다" +
                             "(런타임엔 채팅 Dynamic 폴백이 대신 그려서 눈에 안 띌 수 있습니다). " +
                             "로케일이 아닌 파일이면 무시하세요.");

        foreach (NotoFontRoster.Entry entry in NotoFontRoster.Entries)
            foreach (string code in entry.localeCodes.Where(c => !present.Contains(c)))
                Debug.LogWarning($"[FontBaker] {entry.MainLabel}에 배정된 '{code}'의 String Table을 찾지 못했습니다 — " +
                                 "코드 오타이거나 아직 그 언어 테이블이 없습니다.");
    }

    // ── 문자 집합 수집 ────────────────────────────────────────────

    static HashSet<int> BuildCodepoints(NotoFontRoster.Entry entry, Dictionary<string, List<string>> tables)
    {
        var codes = new HashSet<int>();

        foreach ((int from, int to) in CommonRanges)
            for (int c = from; c <= to; c++)
                codes.Add(c);

        foreach (int c in CommonExtras)
            codes.Add(c);

        foreach ((int from, int to) in entry.scriptRanges)
            for (int c = from; c <= to; c++)
                codes.Add(c);

        foreach (string localeCode in entry.localeCodes)
        {
            AddNativeNameCharacters(localeCode, codes);

            if (tables.TryGetValue(localeCode, out List<string> files))
                foreach (string path in files)
                    CollectStringTableCharacters(path, codes);
        }

        return codes;
    }

    /// <summary>
    /// 옵션 메뉴 언어 드롭다운은 <see cref="LocaleDisplayName"/>을 보여준다.
    /// "한국어" · "日本語" · "中文 (简体)"처럼 String Table엔 없는 문자가 UI에 뜨므로,
    /// 담당 폰트에 넣어두면 폴백이 알아서 찾아간다.
    /// </summary>
    static void AddNativeNameCharacters(string localeCode, HashSet<int> codes)
    {
        AddNonAsciiText(LocaleDisplayName.Of(localeCode), codes);
    }

    /// <summary>
    /// String Table 에셋에서 실제 등장하는 문자를 모은다.
    /// Localization 패키지 API 대신 .asset 텍스트를 직접 읽는다 — 값이 "\uXXXX" 이스케이프로
    /// 저장되고 여러 줄로 감싸이기 때문에, 이스케이프와 원문 비ASCII 문자를 모두 훑는 편이 안전하다.
    /// </summary>
    static void CollectStringTableCharacters(string path, HashSet<int> codes)
    {
        string text = File.ReadAllText(path);

        // \uXXXX 이스케이프: 서로게이트 페어가 원문에서 "붙어 있을 때만" 하나로 합쳐지도록,
        // 인접하지 않은 매치 사이에는 구분자를 끼워 엉뚱한 조합을 막는다.
        var escaped = new StringBuilder();
        int lastEnd = -1;
        foreach (Match m in Regex.Matches(text, @"\\u([0-9A-Fa-f]{4})"))
        {
            if (m.Index != lastEnd) escaped.Append('\n');
            escaped.Append((char)int.Parse(m.Groups[1].Value, NumberStyles.HexNumber));
            lastEnd = m.Index + m.Length;
        }
        AddNonAsciiText(escaped.ToString(), codes);

        // \xXX 이스케이프는 항상 0x100 미만이라 서로게이트가 될 수 없다.
        foreach (Match m in Regex.Matches(text, @"\\x([0-9A-Fa-f]{2})"))
        {
            int c = int.Parse(m.Groups[1].Value, NumberStyles.HexNumber);
            if (c > 0x7F) codes.Add(c);
        }

        AddNonAsciiText(text, codes);
    }

    /// <summary>
    /// 비ASCII 문자의 코드포인트를 모은다(ASCII·제어문자는 공통 범위가 이미 덮으므로 제외).
    /// 서로게이트 페어는 하나의 코드포인트로 합치고 짝을 잃은 반쪽은 버린다 — 반쪽은 어느 폰트에도
    /// 없는 값이고, TryAddCharacters가 입력 배열의 서로게이트를 다시 합치려 하면서
    /// (TMP_FontAssetUtilities.GetCodePoint) 엉뚱한 문자를 요청하게 만든다.
    /// </summary>
    static void AddNonAsciiText(string text, HashSet<int> codes)
    {
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];

            if (char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                codes.Add(char.ConvertToUtf32(c, text[i + 1]));
                i++;
                continue;
            }

            if (char.IsSurrogate(c)) continue;
            if (c > 0x7F) codes.Add(c);
        }
    }

    // ── 로그 유틸 ─────────────────────────────────────────────────

    /// <summary>누락 문자를 사람이 알아볼 수 있게 표기. 너무 길면 앞부분만.</summary>
    static string DescribeCodepoints(uint[] unicodes, int maxShown = 20)
    {
        string shown = string.Join(" ", unicodes.Take(maxShown).Select(u =>
        {
            // 서로게이트 구간·범위 밖 값은 ConvertFromUtf32가 던지므로 코드포인트만 표기한다.
            bool printable = u >= 0x20 && u != 0x7F && (u < 0xD800 || u > 0xDFFF) && u <= 0x10FFFF;
            return printable ? $"'{char.ConvertFromUtf32((int)u)}'(U+{u:X4})" : $"U+{u:X4}";
        }));

        return unicodes.Length > maxShown ? $"{shown} … 외 {unicodes.Length - maxShown}자" : shown;
    }

    /// <summary>현재 샘플링 크기·패딩으로 4096 아틀라스 한 장에 몇 자가 들어가는지로 페이지 수를 추정.</summary>
    static int EstimatePages(string assetPath, int glyphCount)
    {
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
        if (font == null) return 0;

        int cell = Mathf.RoundToInt(font.faceInfo.pointSize) + font.atlasPadding * 2;
        if (cell <= 0) return 0;

        int perRow = AtlasSize / cell;
        int perPage = Mathf.Max(1, perRow * perRow);
        return Mathf.CeilToInt(glyphCount / (float)perPage);
    }
}
#endif
