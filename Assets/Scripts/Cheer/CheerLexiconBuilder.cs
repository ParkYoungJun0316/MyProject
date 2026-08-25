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
    /// <summary>
    /// §5.2 B — 고정 기본 CheerName 중 모델 사전에 없는 이름에 대한 대체(비슷한 소리) 단어 매핑.
    /// key=원래 CheerName(소문자), value=사전에 실제 등재된 대체 단어 목록(소문자).
    /// grammar에는 원래 이름 + 대체 단어를 모두 넣고, 대체 단어가 인식되면
    /// <see cref="ResolveVariant"/>로 원래 CheerName으로 되돌린다.
    /// 고정 4종(berry/guma/sook/dan) 전부 사전 등재 확인됨 → 현재 빈 테이블.
    /// </summary>
    static readonly Dictionary<string, string[]> VariantMap = new();

    /// <summary>
    /// 인식된 단어가 §5.2 B 대체 단어 목록에 있으면 원래 CheerName으로 되돌린다.
    /// 해당 없으면 입력을 그대로 반환.
    /// </summary>
    public static string ResolveVariant(string recognizedWord)
    {
        if (string.IsNullOrEmpty(recognizedWord)) return recognizedWord;

        foreach (var kv in VariantMap)
        {
            foreach (var variant in kv.Value)
            {
                if (variant == recognizedWord) return kv.Key;
            }
        }
        return recognizedWord;
    }

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
    /// 고정 기본 CheerName(§5.2 B VariantMap 등록분)은 대체 단어도 함께 포함된다.
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
            string lower = name.ToLower().Trim();
            sb.Append("\"").Append(lower).Append("\",");

            if (VariantMap.TryGetValue(lower, out var variants))
            {
                foreach (var variant in variants)
                    sb.Append("\"").Append(variant).Append("\",");
            }
        }
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
