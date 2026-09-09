#if UNITY_EDITOR
/// <summary>
/// Noto 폰트 5종의 명단 SSOT — "원본 폰트 파일 ↔ Static 메인 SDF ↔ Dynamic 채팅 폴백 ↔ 담당 로케일"
/// 매핑을 여기 한 곳에만 둔다.
///
/// [왜 분리했나]
/// 예전엔 <see cref="NotoFontStaticBaker"/>와 <see cref="ChatFallbackFontSetup"/>이 각자 자기 목록을
/// 하드코딩하고 있었다. 폰트를 추가·개명하면 두 파일을 같이 고쳐야 하고, 한쪽만 고치면
/// "메인은 구워졌는데 채팅 폴백은 없는" 식으로 조용히 어긋난다.
///
/// [로케일 코드의 의미]
/// Localization 로케일 코드(= StringTable 파일 접미사). 이 폰트가 그 로케일의 문자를 책임진다는 뜻.
/// 실제로 어떤 로케일이 존재하는지는 이 배열이 아니라 StringTables 폴더가 SSOT다 —
/// 베이커가 폴더를 훑어서 여기 배정되지 않은 로케일이 있으면 경고한다.
/// </summary>
static class NotoFontRoster
{
    public sealed class Entry
    {
        public string sourceFontPath;  // 원본 .otf/.ttf (Font 에셋)
        public string mainAssetPath;   // Static 메인 SDF — 사전 베이킹된 UI 텍스트용
        public string chatAssetPath;   // Dynamic 채팅 폴백 — 런타임에 굽는 사용자 입력용
        public string[] localeCodes;   // 이 폰트가 담당하는 로케일 코드
        public (int from, int to)[] scriptRanges; // 담당 스크립트의 유니코드 구간

        public string MainLabel => System.IO.Path.GetFileNameWithoutExtension(mainAssetPath);
        public string ChatLabel => System.IO.Path.GetFileNameWithoutExtension(chatAssetPath);
    }

    public static readonly Entry[] Entries =
    {
        // 12개 로케일 중 9개(라틴·키릴)를 이 폰트가 담당
        new Entry
        {
            sourceFontPath = "Assets/Font/Noto/NotoSans-Regular.ttf",
            mainAssetPath  = "Assets/Font/Noto/NotoSans-Regular SDF.asset",
            chatAssetPath  = "Assets/Font/Noto/Chat/NotoSans-Regular Chat SDF.asset",
            localeCodes    = new[] { "en", "de", "fr", "es", "es-419", "pt", "pt-BR", "pl", "ru" },
            scriptRanges   = new[]
            {
                (0x0100, 0x017F), // Latin Extended-A (폴란드어 ł ż 등)
                (0x0400, 0x045F), // 키릴 (러시아어)
            },
        },
        new Entry
        {
            sourceFontPath = "Assets/Font/Noto/NotoSansKR-Regular.otf",
            mainAssetPath  = "Assets/Font/Noto/NotoSansKR-Regular SDF.asset",
            chatAssetPath  = "Assets/Font/Noto/Chat/NotoSansKR-Regular Chat SDF.asset",
            localeCodes    = new[] { "ko" },
            scriptRanges   = new[]
            {
                (0x3000, 0x303F), // CJK 문장부호
                (0x3131, 0x318E), // 한글 호환 자모
            },
        },
        new Entry
        {
            sourceFontPath = "Assets/Font/Noto/NotoSansJP-Regular.otf",
            mainAssetPath  = "Assets/Font/Noto/NotoSansJP-Regular SDF.asset",
            chatAssetPath  = "Assets/Font/Noto/Chat/NotoSansJP-Regular Chat SDF.asset",
            localeCodes    = new[] { "ja" },
            scriptRanges   = new[]
            {
                (0x3000, 0x303F), // CJK 문장부호
                (0x3041, 0x309F), // 히라가나
                (0x30A0, 0x30FF), // 가타카나
            },
        },
        new Entry
        {
            sourceFontPath = "Assets/Font/Noto/NotoSansSC-Regular.otf",
            mainAssetPath  = "Assets/Font/Noto/NotoSansSC-Regular SDF.asset",
            chatAssetPath  = "Assets/Font/Noto/Chat/NotoSansSC-Regular Chat SDF.asset",
            localeCodes    = new[] { "zh-Hans" },
            scriptRanges   = new[] { (0x3000, 0x303F) },
        },
        new Entry
        {
            sourceFontPath = "Assets/Font/Noto/NotoSansTC-Regular.otf",
            mainAssetPath  = "Assets/Font/Noto/NotoSansTC-Regular SDF.asset",
            chatAssetPath  = "Assets/Font/Noto/Chat/NotoSansTC-Regular Chat SDF.asset",
            localeCodes    = new[] { "zh-Hant" },
            scriptRanges   = new[] { (0x3000, 0x303F) },
        },
    };
}
#endif
