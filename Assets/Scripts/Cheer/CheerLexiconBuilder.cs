using System.Text;
using UnityEngine;

/// <summary>
/// Vosk grammar JSON 문자열 생성.
///
/// [데모 Must]
/// 고정 4종(berry/guma/ssuk/danho) + [unk] grammar을 반환.
/// → VoskRecognizer(model, sampleRate, grammar) 에 직접 전달.
///
/// [발음 참고 테이블] — 데모는 모델 기본 발음 사용. 정식에서 G2P 추가 예정.
///   berry  : B EH R IY        (영어 사전 포함)
///   guma   : G UW M AH        (사전 미포함 → 모델이 근사 처리)
///   ssuk   : S UH K           (사전 미포함 → 모델이 근사 처리)
///   danho  : D AE N HH OW     (사전 미포함 → 모델이 근사 처리)
///
/// [정식 확장 포인트]
/// BuildGrammarJson(names)에 자기 이름 제외 후 호출.
/// lexicon API(set_grm_with_lexicon)는 VoskPINVOKE 확장 시 별도 적용.
/// </summary>
public static class CheerLexiconBuilder
{
    /// <summary>
    /// 데모 고정 4종 grammar JSON 반환.
    /// 결과 예: ["berry","guma","ssuk","danho","[unk]"]
    /// </summary>
    public static string BuildDemoGrammarJson()
    {
        return BuildGrammarJson(new[] { "berry", "guma", "ssuk", "danho" });
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
