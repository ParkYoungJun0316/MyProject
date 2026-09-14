using System.Text;
using UnityEngine;

/// <summary>
/// Vosk grammar JSON 문자열 생성 + 모델 사전 등재 확인.
///
/// [2026-09-14] grammar는 TeamCheerWord 1단어 + [unk]뿐 — 호출자는 CheerKeywordEngine.OwnerGrammarWords.
/// 개인 CheerName용 대체 단어 매핑(VariantMap/ResolveVariant)은 삭제됨 — 인식 단어를 이름으로 되돌리는
/// 변환이 남아 있으면 변형 단어가 팀워드와 겹칠 때 매칭이 절대 성립하지 않는다.
/// </summary>
public static class CheerLexiconBuilder
{
    /// <summary>
    /// 전달받은 단어 배열로 grammar JSON 생성.
    /// [unk] 는 자동으로 끝에 추가됨 — 후보 단어가 아닌 소리를 억지로 후보에 맞추지 않게 하는 흡수용.
    /// </summary>
    public static string BuildGrammarJson(string[] words)
    {
        if (words == null || words.Length == 0)
        {
            Debug.LogWarning("[CheerLexiconBuilder] words 비어 있음 — [unk]만 포함");
            return "[\"[unk]\"]";
        }

        var sb = new StringBuilder();
        sb.Append("[");
        foreach (var word in words)
            sb.Append("\"").Append(word.ToLower().Trim()).Append("\",");
        sb.Append("\"[unk]\"]");

        string result = sb.ToString();
        Debug.Log($"[CheerLexiconBuilder] grammar 생성: {result}");
        return result;
    }

    /// <summary>
    /// word가 로드된 Vosk 모델 사전(words.txt)에 있는지 확인. 사전에 없는 단어는 grammar에 넣어도
    /// 인식되지 않는다 — CheerService.TrySetTeamCheerWord가 "unknown"으로 거절하는 데 쓴다(2026-09-15).
    ///
    /// 모델이 아직 로드되지 않았거나 로드에 실패했으면 검사할 수 없으므로 true를 반환한다.
    /// </summary>
    public static bool IsKnownWord(string word)
    {
        if (string.IsNullOrEmpty(word)) return true;

        var model = VoskModelLoader.GetSharedModel();
        if (model == null) return true;

        return model.vosk_model_find_word(word.ToLower().Trim()) >= 0;
    }
}
