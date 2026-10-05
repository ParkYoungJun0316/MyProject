using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 트레일러 T.Stage2 컷 (19.5초 — 사용자 확정: 4색 전부 보여주려고 T2만 12초 규칙에서 뺌).
/// Stage2.2 ColoredMemoryPath(색 기억 길) 인트로를 게임 순서·값 그대로:
///   0~1초 3인칭(LocalPlayerCamera: distance 5, pitch 55, offset y2) — Blue 꿀떡 뒤
///   1~2.5초 ThirdPersonCamera.BlendToPreview(Pivot2, previewDistance 40, previewPitch 85, yaw 0, blend 1.5초, SmoothStep)
///     — 게임처럼 블렌드 첫 프레임에 target이 pivot으로 바뀐다(positionDamping 0)
///   2.5~4.5초 cameraLeadInTime 2초
///   4.5초 ColoredMemoryPath.StartPreview() — 게임 컴포넌트 그대로(노랑→파랑→보라→초록, 각 3초 + 사이 1초)
///   19.5초 마지막 색이 꺼지는 순간(Challenge 시작)에서 끝.
/// </summary>
public static class TrailerShotTStage2
{
    const string Src      = "Assets/Scenes/T.Stage2.unity";
    const string Dst      = "Assets/Scenes/Marketing_T.Stage2.unity";
    const string AssetDir = "Assets/Trailer/T.Stage2";
    const string ShotName = "Trailer_T.Stage2";
    const string Sm       = "Stage2.2/StageManager2.2/";

    // 게임 값 (LocalPlayerCamera.prefab ThirdPersonCamera · MemoryPathIntroController · ColoredMemoryPath)
    const float GameDist = 5f, GamePitch = 55f;
    static readonly Vector3 GameOffset = new Vector3(0f, 2f, 0f);
    const float PreviewDist = 40f, PreviewPitch = 85f, PreviewYaw = 0f, BlendTime = 1.5f;
    const float LeadIn = 2f;
    const float ColorDur = 3f, ColorGap = 1f;
    const int   Colors = 4;

    const float IntroAt   = 1f;
    static float PreviewAt => IntroAt + BlendTime + LeadIn;
    static float Total     => PreviewAt + Colors * ColorDur + (Colors - 1) * ColorGap;

    [MenuItem("Tools/Trailer/T.Stage2 색 기억 길 미리보기")]
    public static void Build()
    {
        Scene scene = TrailerShotKit.PrepStage(Src, Dst, "ColoredMemoryPath", "ColoredMemoryPathTile");
        TrailerShotKit.Find(scene, "Stage2.2").SetActive(true);
        var shot = new TrailerShotKit.Shot(AssetDir, ShotName, Total);

        // ── 꿀떡: 출발 칸(ColorStartZone) 위에서 대기 ──
        Transform group = TrailerShotKit.Group("Kkultteok", shot.Root).transform;
        var zones = new (string key, string zone)[] { ("B", "Blue"), ("P", "Purple"), ("G", "Green"), ("Y", "Yellow") };
        Vector3 local = Vector3.zero;
        foreach (var z in zones)
        {
            Vector3 p = TrailerShotKit.Find(scene, Sm + "StageStartGate2.2/ColorStartZone." + z.zone).transform.position;
            p.y = 0.5f;
            var k = TrailerShotKit.MakeRunner(group, z.key, p, 0f);
            k.Build(shot);
            if (z.key == "B") local = p;
        }

        // ── 카메라: 게임 ThirdPersonCamera 수식(LateUpdate·BlendToPreview) 그대로 ──
        Vector3 pivot = TrailerShotKit.Find(scene, Sm + "ColorMemoryPathIntroController1/Pivot2").transform.position;
        Camera cam = TrailerShotKit.SpawnCamera(shot.Root);
        (Vector3 pos, Vector3 euler) At(float t)
        {
            Vector3 target = local; float dist = GameDist, pitch = GamePitch, yaw = 0f; Vector3 off = GameOffset;
            if (t >= IntroAt)
            {
                float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - IntroAt) / BlendTime));
                target = pivot;
                dist   = Mathf.Lerp(GameDist, PreviewDist, e);
                pitch  = Mathf.Lerp(GamePitch, PreviewPitch, e);
                yaw    = Mathf.Lerp(0f, PreviewYaw, e);
                off    = Vector3.Lerp(GameOffset, Vector3.zero, e);
            }
            Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
            return (target + off + rot * (Vector3.back * dist), new Vector3(pitch, yaw, 0f));
        }
        AnimationClip camClip = shot.NewClip("Camera_Intro");
        TrailerShotKit.SetPosition(camClip, "", Cut(t => At(t).pos.x), Cut(t => At(t).pos.y), Cut(t => At(t).pos.z));
        TrailerShotKit.SetEuler(camClip, "", Cut(t => At(t).euler.x), Cut(t => At(t).euler.y), TrailerShotKit.Const(0f, Total));
        shot.BindWhole(cam.GetComponent<Animator>(), camClip);

        // ── 길 미리보기: 게임 ColoredMemoryPath ──
        var path = TrailerShotKit.Find(scene, Sm + "ColoredFloor").GetComponent<ColoredMemoryPath>();
        path.colorPreviewDuration = ColorDur;
        path.colorPreviewGap      = ColorGap;
        shot.Call(path, nameof(ColoredMemoryPath.StartPreview), PreviewAt);

        shot.AddRecorder(ShotName);
        shot.Save(scene);
        Debug.Log($"[Trailer] {Dst} 생성 완료 ({Total}초) → Recordings/{ShotName}.mp4");
    }

    /// <summary>프레임마다 키 + IntroAt 직전 키 — 블렌드 시작 순간 target이 바뀌는 점프를 보간 없이 끊는다.</summary>
    static AnimationCurve Cut(System.Func<float, float> f)
    {
        AnimationCurve c = TrailerShotKit.Sampled(Total, f);
        float before = IntroAt - 0.001f;
        c.AddKey(new Keyframe(before, f(before)));
        for (int i = 0; i < c.length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(c, i, AnimationUtility.TangentMode.Linear);
            AnimationUtility.SetKeyRightTangentMode(c, i, AnimationUtility.TangentMode.Linear);
        }
        return c;
    }
}
