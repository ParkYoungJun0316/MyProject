using UnityEngine;

/// <summary>
/// 모든 경고 사인의 공용 색 SSOT (2026-09-22 확정).
/// ArrowWarnSign / SpikeLaneWarnMarker(가시·BreakTile·Grid 붕괴·혀·턱) /
/// DropWarnMarker / Breakable / CapacityTile이 전부 여기서 색을 읽는다 — 컴포넌트별 인스펙터
/// 색 필드는 두지 않는다(씬마다 값이 어긋나던 문제의 원천 차단).
///
/// 게임플레이 색(파랑 #2384C4 · 노랑 #DCA524 · 보라 #5900BC · 초록 #4C6C48, PlayerColorUtil)과
/// 겹치지 않게 빨강~주황 대역(색상각 약 0~30°)만 쓴다. 특히 시작색은 노랑 플레이어색과
/// 헷갈리지 않도록 확실히 주황 쪽이다.
///
/// 값은 "화면에 보일 최종색"(sRGB). MaterialPropertyBlock.SetColor가 Linear 변환을 해 주고,
/// WarnMarker 셰이더는 Glow 곱셈을 하지 않으므로 이 값이 그대로 보인다.
/// </summary>
public static class WarnPalette
{
    /// <summary>경고 시작 — 탠저린 #FF9A3D</summary>
    public static readonly Color Start = new Color32(0xFF, 0x9A, 0x3D, 0xFF);

    /// <summary>발동 순간 — 진홍 #E81E2B</summary>
    public static readonly Color End = new Color32(0xE8, 0x1E, 0x2B, 0xFF);

    /// <summary>WarnMarker 셰이더 바깥 테두리 — 진홍의 어두운 톤 #8E0F17</summary>
    public static readonly Color Border = new Color32(0x8E, 0x0F, 0x17, 0xFF);

    /// <summary>CapacityTile처럼 "안전→위험" 의미인 곳의 안전 쪽 색</summary>
    public static readonly Color Safe = Color.white;
}
