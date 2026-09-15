#if UNITY_EDITOR
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;

/// <summary>
/// UI TMP 폰트 Asset Table. 라틴·키릴 = Dialogue와 같은 Fredoka-Bold,
/// ko/ja/zh = NotoFontRoster Static 메인.
/// Menu: Tools / Setup UI Font Localization
/// </summary>
public static class SetupUiFontLocalization
{
    const string TableName = "UIFont";
    const string TableFolder = "Assets/Localization/AssetTables";
    const string EntryKey = "TMP.Font";

    const string FredokaPath = "Assets/Font/Fredoka-Bold SDF.asset";
    const string NotoKrPath = "Assets/Font/Noto/NotoSansKR-Regular SDF.asset";
    const string NotoJpPath = "Assets/Font/Noto/NotoSansJP-Regular SDF.asset";
    const string NotoScPath = "Assets/Font/Noto/NotoSansSC-Regular SDF.asset";
    const string NotoTcPath = "Assets/Font/Noto/NotoSansTC-Regular SDF.asset";

    static readonly Dictionary<string, string> LocaleToFontPath = new Dictionary<string, string>
    {
        { "en", FredokaPath },
        { "de", FredokaPath },
        { "fr", FredokaPath },
        { "es", FredokaPath },
        { "es-419", FredokaPath },
        { "pt", FredokaPath },
        { "pt-BR", FredokaPath },
        { "pl", FredokaPath },
        { "ru", FredokaPath },
        { "ko", NotoKrPath },
        { "ja", NotoJpPath },
        { "zh-Hans", NotoScPath },
        { "zh-Hant", NotoTcPath },
    };

    [MenuItem("Tools/Setup UI Font Localization")]
    public static void Run()
    {
        AssetTableCollection collection = EnsureCollection();
        SharedTableData.SharedTableEntry shared = collection.SharedData.GetEntry(EntryKey)
            ?? collection.SharedData.AddKey(EntryKey);
        collection.SetEntryAssetType(EntryKey, typeof(TMP_FontAsset));

        int filled = 0;
        foreach (AssetTable table in collection.AssetTables)
        {
            string code = table.LocaleIdentifier.Code;
            string path;
            if (!LocaleToFontPath.TryGetValue(code, out path))
                path = FredokaPath;

            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (font == null)
            {
                Debug.LogError("[SetupUiFontLocalization] 폰트 없음: " + path);
                continue;
            }

            collection.AddAssetToTable(table, shared.Id, font, false);
            EditorUtility.SetDirty(table);
            filled++;
        }

        EditorUtility.SetDirty(collection.SharedData);
        EditorUtility.SetDirty(collection);
        AssetDatabase.SaveAssets();
        Debug.Log("[SetupUiFontLocalization] " + TableName + " / " + EntryKey + " locales=" + filled);
    }

    static AssetTableCollection EnsureCollection()
    {
        AssetTableCollection existing = LocalizationEditorSettings.GetAssetTableCollection(TableName);
        if (existing != null) return existing;

        if (!AssetDatabase.IsValidFolder("Assets/Localization"))
            AssetDatabase.CreateFolder("Assets", "Localization");
        if (!AssetDatabase.IsValidFolder(TableFolder))
            AssetDatabase.CreateFolder("Assets/Localization", "AssetTables");

        var locales = LocalizationEditorSettings.GetLocales();
        return LocalizationEditorSettings.CreateAssetTableCollection(TableName, TableFolder, locales);
    }
}
#endif
