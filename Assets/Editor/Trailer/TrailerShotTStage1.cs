using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 트레일러 T.Stage1 컷 (12초). 넷이 +Z 복도를 달리고 바로 뒤에서 볼더가 굴러온다.
/// 값은 게임 그대로: 꿀떡 speed 10(Kkultteok.prefab), 볼더 WaypointMover.initialSpeed 8 · SpinRoller 10 rad/s ·
/// 지름 80 · 중심 y 20(BoulderSpawner 웨이포인트 y). 볼더가 느려서 간격은 3m → 27m로 벌어진다(사용자 확정: 게임 속도 그대로).
/// 카메라: 무리 중심을 따라가며 앞(+Z 20)에서 뒤를 보다가 바로 위(20)로 — (0,0,20) → (0,20,0).
/// 시작 장벽(Barrier.F/B)은 게임에서 출발 때 터져 없어지므로 숨긴다.
/// </summary>
public static class TrailerShotTStage1
{
    const string Src      = "Assets/Scenes/T.Stage1.unity";
    const string Dst      = "Assets/Scenes/Marketing_T.Stage1.unity";
    const string AssetDir = "Assets/Trailer/T.Stage1";
    const string ShotName = "Trailer_T.Stage1";
    const float  Total    = 12f;
    const float  GroundY  = 0.5f;

    // 게임 값
    const float RunSpeed     = 10f;
    const float BoulderSpeed = 8f;
    const float BoulderSpin  = 10f * Mathf.Rad2Deg; // rad/s → °/s
    const float BoulderY     = 20f;
    const float BoulderR     = 40f;

    const float StartZ   = 30f;  // 무리 중심 시작 z (앞 장벽 17.1 너머)
    const float StartGap = 3f;   // 맨 뒤 꿀떡 ↔ 볼더 앞면(바닥 높이)
    const float CamRadius = 20f;
    const float PivotY    = 1.5f;

    [MenuItem("Tools/Trailer/T.Stage1 볼더 추격")]
    public static void Build()
    {
        Scene scene = TrailerShotKit.PrepStage(Src, Dst);
        TrailerShotKit.HideByPrefix(scene, "Barrier.");
        var shot = new TrailerShotKit.Shot(AssetDir, ShotName, Total);

        System.Func<float, Vector3> pivot = t => new Vector3(0f, PivotY, StartZ + RunSpeed * t);
        TrailerShotKit.AddFollowOrbitCamera(shot, pivot, CamRadius, 0f, 90f, 1f, 180f);

        // ── 꿀떡: 마름모 대형(간격 3.5m+)으로 직진, 좌우로 살짝 흔들림 ──
        var plan = new (string key, Vector2 offset, float swayAmp, float swayFreq, float phase)[]
        {
            ("B", new Vector2( 0f,    3f), 0.6f, 0.35f, 0.0f),
            ("P", new Vector2( 3.5f,  0f), 0.8f, 0.28f, 1.3f),
            ("G", new Vector2(-3.5f,  0f), 0.7f, 0.31f, 2.6f),
            ("Y", new Vector2( 0f,   -3f), 0.5f, 0.40f, 4.1f),
        };
        Transform group = TrailerShotKit.Group("Kkultteok", shot.Root).transform;
        float rearZ = float.MaxValue;
        for (int i = 0; i < plan.Length; i++)
        {
            var p = plan[i];
            Vector3 At(float t) => new Vector3(p.offset.x + p.swayAmp * Mathf.Sin(p.phase + t * p.swayFreq * Mathf.PI * 2f),
                                               GroundY, StartZ + p.offset.y + RunSpeed * t);
            var k = TrailerShotKit.MakeRunner(group, p.key, At(0f), 0f);
            k.StartWith("Run", i * 0.17f);
            var keys = new List<(float, Vector3, float)>();
            for (float t = 0f; t <= Total + 0.001f; t += 0.25f)
            {
                Vector3 d = At(t + 0.05f) - At(t);
                keys.Add((t, At(t), TrailerShotKit.Yaw(d)));
            }
            k.Follow(keys);
            k.Build(shot);
            rearZ = Mathf.Min(rearZ, StartZ + p.offset.y);
        }

        // ── 볼더: 게임 Boulder.prefab 그대로(로직 제거), +Z 8m/s 직진 + X축 회전 ──
        float frontAtGround = Mathf.Sqrt(BoulderR * BoulderR - (BoulderY - GroundY) * (BoulderY - GroundY));
        float startCenterZ  = rearZ - StartGap - frontAtGround;
        Animator boulderRig = TrailerShotKit.Rig("Boulder", shot.Root, new Vector3(0f, BoulderY, startCenterZ), Quaternion.identity);
        TrailerShotKit.Spawn("Assets/Prefab/식도/Boulder.prefab", "Model", boulderRig.transform, boulderRig.transform.position, Quaternion.identity);
        AnimationClip bClip = shot.NewClip("Boulder_Roll");
        TrailerShotKit.SetPosition(bClip, "", TrailerShotKit.Const(0f, Total), TrailerShotKit.Const(BoulderY, Total),
            TrailerShotKit.Sampled(Total, t => startCenterZ + BoulderSpeed * t));
        TrailerShotKit.SetEuler(bClip, "", TrailerShotKit.Sampled(Total, t => BoulderSpin * t), TrailerShotKit.Const(0f, Total), TrailerShotKit.Const(0f, Total));
        shot.BindWhole(boulderRig, bClip);

        shot.AddRecorder(ShotName);
        shot.Save(scene);
        Debug.Log($"[Trailer] {Dst} 생성 완료 (볼더 중심 z {startCenterZ:F1}, 앞면 간격 {StartGap}m → {StartGap + (RunSpeed - BoulderSpeed) * Total}m) → Recordings/{ShotName}.mp4");
    }
}
