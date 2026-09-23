#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization.Tables;

/// <summary>
/// StageTitle String Table 생성·채우기. SSOT는 Assets/Docs/StageTitleBanner.md §3·§4의 표.
/// 표 헤더 `| 키 | ko | en |` 처럼 첫 칸이 "키"인 줄이 열(로케일) 순서를 정하고,
/// 그 아래 `` | `Title.…` | … | `` 줄이 값이다. 표가 여러 개여도 같은 키에 로케일이 합쳐진다.
/// Menu: Tools / Setup StageTitle Localization
/// </summary>
public static class SetupStageTitleLocalization
{
    const string TableName = StageTitleBannerUI.TableName;
    const string TableFolder = "Assets/Localization/StringTables";
    const string DocPath = "Assets/Docs/StageTitleBanner.md";

    [MenuItem("Tools/Setup StageTitle Localization")]
    public static void Run()
    {
        Dictionary<string, Dictionary<string, string>> entries = ParseDoc();
        if (entries.Count == 0)
        {
            Debug.LogError("[SetupStageTitleLocalization] 키를 하나도 못 읽었습니다 — " + DocPath);
            return;
        }

        StringTableCollection collection = EnsureCollection();
        FillTranslations(collection, entries);
        AssetDatabase.SaveAssets();
        Debug.Log($"[SetupStageTitleLocalization] {TableName} {entries.Count} keys filled from {DocPath}.");
    }

    static StringTableCollection EnsureCollection()
    {
        StringTableCollection existing = LocalizationEditorSettings.GetStringTableCollection(TableName);
        if (existing != null) return existing;

        if (!AssetDatabase.IsValidFolder("Assets/Localization"))
            AssetDatabase.CreateFolder("Assets", "Localization");
        if (!AssetDatabase.IsValidFolder(TableFolder))
            AssetDatabase.CreateFolder("Assets/Localization", "StringTables");

        var locales = LocalizationEditorSettings.GetLocales();
        return LocalizationEditorSettings.CreateStringTableCollection(TableName, TableFolder, locales);
    }

    static void FillTranslations(
        StringTableCollection collection,
        Dictionary<string, Dictionary<string, string>> entries)
    {
        SharedTableData shared = collection.SharedData;
        foreach (var kv in entries)
        {
            string key = kv.Key;
            SharedTableData.SharedTableEntry sharedEntry = shared.GetEntry(key) ?? shared.AddKey(key);

            foreach (StringTable table in collection.StringTables)
            {
                string code = table.LocaleIdentifier.Code;
                if (!kv.Value.TryGetValue(code, out string value))
                {
                    if (code == "pt")
                        kv.Value.TryGetValue("pt-BR", out value);
                    if (string.IsNullOrEmpty(value))
                        kv.Value.TryGetValue("en", out value);
                    if (string.IsNullOrEmpty(value))
                        kv.Value.TryGetValue("ko", out value);
                    if (string.IsNullOrEmpty(value))
                        value = key;
                }

                StringTableEntry entry = table.GetEntry(sharedEntry.Id);
                if (entry == null) table.AddEntry(sharedEntry.Id, value);
                else entry.Value = value;

                EditorUtility.SetDirty(table);
            }
        }

        EditorUtility.SetDirty(shared);
        EditorUtility.SetDirty(collection);
    }

    static Dictionary<string, Dictionary<string, string>> ParseDoc()
    {
        var result = new Dictionary<string, Dictionary<string, string>>();
        if (!File.Exists(DocPath))
        {
            Debug.LogError("[SetupStageTitleLocalization] 문서 없음: " + DocPath);
            return result;
        }

        string[] columns = null;
        foreach (string raw in File.ReadAllLines(DocPath))
        {
            string line = raw.Trim();
            if (!line.StartsWith("|"))
            {
                columns = null; // 표가 끝났다
                continue;
            }

            string[] cells = SplitRow(line);
            if (cells.Length == 0) continue;

            if (cells[0] == "키")
            {
                columns = cells;
                continue;
            }

            if (columns == null) continue;

            string key = cells[0].Trim('`');
            if (!key.StartsWith("Title.")) continue;

            if (!result.TryGetValue(key, out var locales))
                result[key] = locales = new Dictionary<string, string>();

            for (int i = 1; i < cells.Length && i < columns.Length; i++)
                if (!string.IsNullOrEmpty(cells[i]))
                    locales[columns[i]] = cells[i];
        }

        return result;
    }

    static string[] SplitRow(string line)
    {
        string inner = line.Trim().Trim('|');
        string[] cells = inner.Split('|');
        for (int i = 0; i < cells.Length; i++)
            cells[i] = cells[i].Trim();
        return cells;
    }
}
#endif
