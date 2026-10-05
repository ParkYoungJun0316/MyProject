using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

/// <summary>
/// 트레일러 녹화 확인용: Marketing_ 씬을 Play하면 SessionState "TrailerCaptureTimes"(예: "2.7,5.6")에
/// 적힌 Timeline 시각마다 촬영 카메라 화면을 Assets/Screenshots/Trailer/&lt;씬&gt;_&lt;초&gt;.png로 저장한다.
/// 녹화(Recorder)와 같은 Play에서 돌아 영상과 같은 프레임을 본다. 목록이 비면 아무것도 안 한다.
/// </summary>
[InitializeOnLoad]
static class TrailerPreviewCapture
{
    const string Key = "TrailerCaptureTimes";
    const string Dir = "Assets/Screenshots/Trailer";

    static PlayableDirector _director;
    static Camera _camera;
    static float[] _times;
    static int _next;

    static TrailerPreviewCapture()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    static void OnPlayModeChanged(PlayModeStateChange change)
    {
        if (change != PlayModeStateChange.EnteredPlayMode) return;
        if (!SceneManager.GetActiveScene().name.StartsWith("Marketing_")) return;

        string raw = SessionState.GetString(Key, "");
        if (string.IsNullOrEmpty(raw)) return;

        string[] parts = raw.Split(',');
        _times = new float[parts.Length];
        for (int i = 0; i < parts.Length; i++)
            _times[i] = float.Parse(parts[i], CultureInfo.InvariantCulture);
        System.Array.Sort(_times);
        _next = 0;

        GameObject shot = GameObject.Find("TrailerShot");
        _director = shot != null ? shot.GetComponent<PlayableDirector>() : null;
        Transform cam = shot != null ? shot.transform.Find("TrailerCamera") : null;
        _camera = cam != null ? cam.GetComponent<Camera>() : null;
        if (_director == null || _camera == null) return;

        Directory.CreateDirectory(Dir);
        EditorApplication.update += Tick;
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying || _director == null)
        {
            EditorApplication.update -= Tick;
            return;
        }

        while (_next < _times.Length && _director.time >= _times[_next])
        {
            Capture(_director.time);
            _next++;
        }
        if (_next >= _times.Length) EditorApplication.update -= Tick;
    }

    static void Capture(double t)
    {
        var rt  = new RenderTexture(960, 540, 24);
        var tex = new Texture2D(960, 540, TextureFormat.RGB24, false);
        RenderTexture prev = _camera.targetTexture;
        _camera.targetTexture = rt;
        _camera.Render();
        _camera.targetTexture = prev;
        RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
        tex.Apply();
        RenderTexture.active = null;

        string name = $"{SceneManager.GetActiveScene().name}_{t.ToString("0.00", CultureInfo.InvariantCulture)}.png";
        File.WriteAllBytes(Path.Combine(Dir, name), tex.EncodeToPNG());
        Object.Destroy(rt);
        Object.Destroy(tex);
    }
}
