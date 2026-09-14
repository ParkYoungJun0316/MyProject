using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Vosk grammar JSON 문자열 생성.
///
/// [실측 — 2026-08, 모델 words.txt 직접 확인 (CheerAndTutorialDesign.md §5.2)]
/// berry / guma / sook / dan : 모델 사전에 이미 등재됨 → 변형 불필요 (과거 "사전 미포함" 주석은 오기, 실측으로 정정).
/// (구 기본값 "hobak"은 사전 미등재라 대체 단어 "dan"을 썼었으나, 2026-08-25 기본 CheerName 자체를
///  "dan"으로 교체하면서 hobak/VariantMap 항목은 완전히 제거됨.)
///
/// [테스트 키워드 세트 — 인식률 비교용]
/// Set1 (현재): worcestershire / colonel / anemone / mischievous
///   발음 주의: woos-ter-sher / ker-nel / ah-nem-oh-nee / mis-chih-vus
/// Set2: rural / sixth / squirrel  (3종)
/// Set3: Antidisestablishmentarianism / Floccinaucinihilipilification / Pneumonoultramicroscopicsilicovolcanoconiosis  (3종)
///
/// [확정 키워드 후 원상복구]
/// BuildDemoGrammarJson() 의 배열을 berry/guma/sook/dan 으로 유지.
/// CheerService.CheerNames 도 동일하게 맞출 것.
/// </summary>
public static class CheerLexiconBuilder
{
    // [2026-09-14] §5.2 B 대체 단어 매핑(VariantMap/ResolveVariant) 삭제 — 개인 CheerName이 음성 인식
    // 대상에서 빠져 grammar는 TeamCheerWord 1단어뿐이다. 인식 단어를 이름으로 되돌리는 변환이 남아 있으면
    // 변형 단어가 팀워드와 겹칠 때 매칭이 절대 성립하지 않는다.

    /// <summary>
    /// 데모 기본 4종 grammar JSON (커스텀 미설정 시 폴백용).
    /// 결과 예: ["berry","guma","sook","dan","[unk]"]
    ///
    /// 커스텀 이름이 있을 때는 BuildGrammarJson(세션이름[]) 을 사용할 것.
    /// </summary>
    public static string BuildDemoGrammarJson()
    {
        return BuildGrammarJson(new[] { "berry", "guma", "sook", "dan" });
    }

    /// <summary>
    /// 전달받은 이름 배열로 grammar JSON 생성.
    /// [unk] 는 자동으로 끝에 추가됨.
    /// </summary>
    public static string BuildGrammarJson(string[] cheerNames)
    {
        if (cheerNames == null || cheerNames.Length == 0)
        {
            Debug.LogWarning("[CheerLexiconBuilder] cheerNames 비어 있음 — [unk]만 포함");
            return "[\"[unk]\"]";
        }

        var sb = new StringBuilder();
        sb.Append("[");
        foreach (var name in cheerNames)
            sb.Append("\"").Append(name.ToLower().Trim()).Append("\",");
        sb.Append("\"[unk]\"]");

        string result = sb.ToString();
        Debug.Log($"[CheerLexiconBuilder] grammar 생성: {result}");
        return result;
    }

    /// <summary>
    /// CheerAndTutorialDesign.md §5.2 A — word가 현재 로드된 Vosk 모델 사전(words.txt)에
    /// 있는지 확인. 모델 사전에 없는 단어는 grammar에 넣어도 인식이 잘 안 될 수 있음(경고용).
    /// 강제 차단이 아니라 로비 UI 경고 표시 용도.
    ///
    /// 모델이 아직 로드되지 않았으면 오탐(false-positive 경고) 방지를 위해 true를 반환한다.
    /// </summary>
    public static bool IsKnownWord(string word)
    {
        if (string.IsNullOrEmpty(word)) return true;

        var model = VoskModelLoader.GetSharedModel();
        if (model == null) return true;

        return model.vosk_model_find_word(word.ToLower().Trim()) >= 0;
    }
}
