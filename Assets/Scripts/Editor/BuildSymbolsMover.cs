using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// 빌드 직후 배포 금지 폴더(IL2CPP <c>*_BackUpThisFolder_ButDontShipItWithYourGame</c>,
/// Burst <c>*_BurstDebugInformation_DoNotShip</c>)를 빌드 폴더 밖으로 옮긴다.
///
/// [왜]
/// 빌드 폴더(SteamPipe content/KKUL-TTEOK!_Build)는 depot 스크립트가 통째로 올린다 — 그대로 두면
/// 디버그 심볼이 Steam에 같이 배포된다. 지우지 않고 옮기는 이유는 네이티브 크래시 덤프를 읽을 때
/// 그 빌드의 심볼이 필요하기 때문(빌드마다 새로 생기고 다시 만들 수 없음).
///
/// [어디로]
/// 빌드 폴더 한 단계 위, <c>&lt;빌드폴더이름&gt;_Symbols/&lt;버전&gt;_&lt;빌드시각&gt;/</c>.
/// 예) content/KKUL-TTEOK!_Build_Symbols/1.0.0_20260928-153000/
/// depot 스크립트의 LocalPath가 "KKUL-TTEOK!_Build/*"라 이 위치는 업로드되지 않는다.
/// 오래된 것은 자동으로 지우지 않는다 — 최근 2~3개만 남기고 직접 정리.
/// </summary>
public class BuildSymbolsMover : IPostprocessBuildWithReport
{
    static readonly string[] DoNotShipSuffixes =
    {
        "_BackUpThisFolder_ButDontShipItWithYourGame",
        "_BurstDebugInformation_DoNotShip",
    };

    public int callbackOrder => int.MaxValue;

    public void OnPostprocessBuild(BuildReport report)
    {
        if (report.summary.platformGroup != BuildTargetGroup.Standalone) return;

        string exePath = report.summary.outputPath;
        string buildDir = Path.GetDirectoryName(exePath);
        string parentDir = Path.GetDirectoryName(buildDir);
        if (string.IsNullOrEmpty(buildDir) || string.IsNullOrEmpty(parentDir)) return;

        string exeName = Path.GetFileNameWithoutExtension(exePath);
        string destRoot = Path.Combine(parentDir, Path.GetFileName(buildDir) + "_Symbols",
                                       $"{PlayerSettings.bundleVersion}_{DateTime.Now:yyyyMMdd-HHmmss}");

        foreach (string suffix in DoNotShipSuffixes)
        {
            string src = Path.Combine(buildDir, exeName + suffix);
            if (!Directory.Exists(src)) continue;

            string dest = Path.Combine(destRoot, exeName + suffix);
            try
            {
                Directory.CreateDirectory(destRoot);
                Directory.Move(src, dest);
                Debug.Log($"[BuildSymbolsMover] 배포 금지 폴더를 빌드 밖으로 이동 — {src} → {dest}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[BuildSymbolsMover] 이동 실패 — 업로드 전에 직접 빼야 함: {src}\n{e}");
            }
        }
    }
}
