using System.Text;
using UnityEngine;

/// <summary>
/// Vosk grammar JSON 문자열 생성.
///
/// [테스트 키워드 세트 — 인식률 비교용]
/// Set1 (현재): worcestershire / colonel / anemone / mischievous
///   발음 주의: woos-ter-sher / ker-nel / ah-nem-oh-nee / mis-chih-vus
/// Set2: rural / sixth / squirrel  (3종)
/// Set3: Antidisestablishmentarianism / Floccinaucinihilipilification / Pneumonoultramicroscopicsilicovolcanoconiosis  (3종)
///
/// [확정 키워드 후 원상복구]
/// BuildDemoGrammarJson() 의 배열을 berry/guma/ssuk대체어/danho대체어 로 교체.
/// CheerService.CheerNames 도 동일하게 맞출 것.
/// </summary>
public static class CheerLexiconBuilder
{
    /// <summary>
    /// 데모 고정 4종 grammar JSON.
    /// 결과 예: ["berry","guma","sook","tango","[unk]"]
    ///
    /// [발음 참고]
    ///   berry : B EH R IY        (영어 사전 포함)
    ///   guma  : G UW M AH        (사전 미포함 → 근사 처리)
    ///   sook  : S UH K           (사전 미포함 → 근사 처리, ssuk 대체)
    ///   tango : T AE NG G OW     (영어 사전 포함, 단호 /dan.ho/ 근사 대체)
    /// </summary>
    public static string BuildDemoGrammarJson()
    {
        return BuildGrammarJson(new[] { "berry", "guma", "sook", "tango" });
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
        {
            sb.Append("\"");
            sb.Append(name.ToLower().Trim());
            sb.Append("\",");
        }
        sb.Append("\"[unk]\"]");

        string result = sb.ToString();
        Debug.Log($"[CheerLexiconBuilder] grammar 생성: {result}");
        return result;
    }
}
