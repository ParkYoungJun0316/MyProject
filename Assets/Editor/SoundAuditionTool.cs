#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Play Mode에서 실제 SFXManager / BGMManager 파이프라인으로 SFX·BGM을 한 번에 훑어 듣기 위한 도구.
/// Menu: Tools / Sound Audition Tool
///
/// [왜 필요한가]
///   Player 사운드(Hit/Death/ColorChange/Buff/Run)는 spatialBlend = 0 고정이라 거리와 무관하게
///   항상 같은 크기로 들림. 반면 함정 등 3D SFX(Play(id, worldPosition))는 AudioSource.PlayClipAtPoint의
///   Unity 기본 3D 감쇠(minDistance 1m / maxDistance 500m / Logarithmic)를 그대로 씀 — 스테이지를
///   실제로 걸어다니지 않고도 "가상 거리" 슬라이더로 이 감쇠를 바로 확인할 수 있게 한다.
///   재생은 SFXLibrary.GetClip / SFXManager.Play / BGMManager.PlayClip을 그대로 호출하므로
///   옵션 메뉴 볼륨 슬라이더(GameSettingsManager)와 SFXLibrary.VolumeOverride가 실제로 반영된다.
///
/// [사용 방법]
///   1. Play Mode 진입(SFXManager/BGMManager가 있는 씬, 예: 0.Title).
///   2. SFX는 "2D"(플레이어류) / "3D"(가상 거리 슬라이더 적용) 버튼으로 즉시 재생.
///   3. BGM은 Assets/Audio/BGM의 클립 목록에서 바로 크로스페이드 재생 — zoneClips 설정 여부와 무관.
/// </summary>
public class SoundAuditionTool : EditorWindow
{
    float _distance = 15f;
    Vector2 _scroll;
    string[] _bgmClipPaths;
    bool _bgmListDirty = true;

    [MenuItem("Tools/Sound Audition Tool")]
    static void Open()
    {
        SoundAuditionTool window = GetWindow<SoundAuditionTool>("Sound Audition");
        window.minSize = new Vector2(360, 480);
    }

    void OnEnable() => EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    void OnDisable() => EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;

    void OnPlayModeStateChanged(PlayModeStateChange _) => Repaint();

    void OnGUI()
    {
        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Play Mode에서만 실제 볼륨 파이프라인으로 재생됩니다.", MessageType.Warning);
            if (GUILayout.Button("▶ Play Mode 시작"))
                EditorApplication.isPlaying = true;
            return;
        }

        DrawVolumeStatus();
        EditorGUILayout.Space();
        _distance = EditorGUILayout.Slider("3D 가상 거리 (m, 리스너 정면 기준)", _distance, 0f, 100f);
        EditorGUILayout.Space();

        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        DrawSfxSection();
        EditorGUILayout.Space(12);
        DrawBgmSection();
        EditorGUILayout.EndScrollView();
    }

    void DrawVolumeStatus()
    {
        GameSettingsManager settings = GameSettingsManager.Instance;
        if (settings == null)
        {
            EditorGUILayout.HelpBox("GameSettingsManager가 씬에 없습니다 — 옵션 볼륨 슬라이더가 반영되지 않습니다(1배로 폴백).", MessageType.Info);
            return;
        }
        EditorGUILayout.LabelField(
            $"Master {settings.MasterVolume:0.00}   ·   BGM {settings.BgmVolume:0.00}   ·   SFX {settings.SfxVolume:0.00}",
            EditorStyles.miniLabel);
    }

    void DrawSfxSection()
    {
        EditorGUILayout.LabelField("SFX", EditorStyles.boldLabel);

        SFXManager sfx = SFXManager.Instance;
        if (sfx == null)
        {
            EditorGUILayout.HelpBox("SFXManager.Instance가 없습니다 (씬에 배치돼 있는지 확인).", MessageType.Warning);
            return;
        }

        AudioListener listener = FindObjectOfType<AudioListener>();

        foreach (SFXId id in Enum.GetValues(typeof(SFXId)))
        {
            if (id == SFXId.None) continue;

            AudioClip clip = sfx.GetClip(id);
            bool has = clip != null;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(has ? id.ToString() : $"{id}  (미배정)", GUILayout.MinWidth(180));

            using (new EditorGUI.DisabledScope(!has))
            {
                if (GUILayout.Button("2D", GUILayout.Width(40)))
                    sfx.Play(id);

                using (new EditorGUI.DisabledScope(listener == null))
                {
                    if (GUILayout.Button("3D", GUILayout.Width(40)))
                    {
                        Vector3 pos = listener.transform.position + listener.transform.forward * _distance;
                        sfx.Play(id, pos);
                    }
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        if (listener == null)
            EditorGUILayout.HelpBox("씬에 AudioListener가 없어 3D 재생 버튼이 비활성화됩니다.", MessageType.Info);
    }

    void DrawBgmSection()
    {
        EditorGUILayout.LabelField("BGM  (Assets/Audio/BGM 폴더 전체, zoneClips 설정과 무관)", EditorStyles.boldLabel);

        BGMManager bgm = BGMManager.Instance;
        if (bgm == null)
        {
            EditorGUILayout.HelpBox("BGMManager.Instance가 없습니다 (씬에 배치돼 있는지 확인).", MessageType.Warning);
            return;
        }

        if (_bgmListDirty)
        {
            _bgmClipPaths = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Audio/BGM" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(p => p)
                .ToArray();
            _bgmListDirty = false;
        }

        if (_bgmClipPaths == null || _bgmClipPaths.Length == 0)
        {
            EditorGUILayout.HelpBox("Assets/Audio/BGM에 오디오 클립이 없습니다.", MessageType.Info);
            return;
        }

        foreach (string path in _bgmClipPaths)
        {
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null) continue;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(clip.name, GUILayout.MinWidth(180));
            if (GUILayout.Button("크로스페이드 재생", GUILayout.Width(120)))
                bgm.PlayClip(clip);
            EditorGUILayout.EndHorizontal();
        }
    }
}
#endif
