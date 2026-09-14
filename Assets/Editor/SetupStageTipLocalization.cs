#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization.Tables;

/// <summary>
/// StageTip String Table 생성·채우기. SSOT는 Assets/Docs/StageTipTranslations.md.
/// Menu: Tools / Setup StageTip Localization
/// </summary>
public static class SetupStageTipLocalization
{
    const string TableName = "StageTip";
    const string TableFolder = "Assets/Localization/StringTables";
    const string DocPath = "Assets/Docs/StageTipTranslations.md";

    [MenuItem("Tools/Setup StageTip Localization")]
    public static void Run()
    {
        Dictionary<string, Dictionary<string, string>> entries = ParseDoc();
        if (entries.Count == 0)
        {
            Debug.LogError("[SetupStageTipLocalization] 키를 하나도 못 읽었습니다 — " + DocPath);
            return;
        }

        StringTableCollection collection = EnsureCollection();
        FillTranslations(collection, entries);
        AssetDatabase.SaveAssets();
        Debug.Log($"[SetupStageTipLocalization] {TableName} {entries.Count} keys filled from {DocPath}.");
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
            Debug.LogError("[SetupStageTipLocalization] 문서 없음: " + DocPath);
            return result;
        }

        string currentKey = null;
        foreach (string raw in File.ReadAllLines(DocPath))
        {
            string line = raw.TrimEnd();
            Match header = Regex.Match(line, @"^## (Tip\.\S+)\s*$");
            if (header.Success)
            {
                currentKey = header.Groups[1].Value;
                if (!result.ContainsKey(currentKey))
                    result[currentKey] = new Dictionary<string, string>();
                continue;
            }

            if (currentKey == null) continue;
            if (line.StartsWith("## "))
            {
                currentKey = null;
                continue;
            }

            Match entry = Regex.Match(line, @"^- ([A-Za-z0-9-]+): (.*)$");
            if (!entry.Success) continue;

            string locale = entry.Groups[1].Value;
            string value = entry.Groups[2].Value.Replace(@"\n", "\n");
            result[currentKey][locale] = value;
        }

        return result;
    }
}
#endif
