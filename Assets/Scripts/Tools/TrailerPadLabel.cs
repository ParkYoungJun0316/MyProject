using UnityEngine;

/// <summary>
/// 트레일러 촬영 전용(Marketing_ 씬): 버텨야 하는 압력 발판의 "현재/필요 + 남은 초" 표시.
/// 모양은 게임 WorldCountLabel(SSOT), 내용 규칙은 PressurePadCountUI.Show(latchOnFullyOpen 분기) 그대로 —
/// 남은 초는 게임 DoorController.OpenTimeRemaining, 고정 여부는 IsLatched를 읽는다.
/// 인원만 Timeline Signal(SetCount)로 받는다. 받을 때 발판 OnCountChanged도 쏴서 PadOccupancyFeedback 물결이 게임처럼 켜진다.
/// </summary>
public class TrailerPadLabel : MonoBehaviour
{
    public PressurePad pad;
    public DoorController door;
    public Vector3 offset = new Vector3(0f, 1.8f, 0f);
    public float fontSize = 10f;
    public int required = 4;

    static readonly Color HoldTimerColor = new Color32(255, 176, 32, 255);
    static readonly Color LatchedColor   = new Color32(90, 220, 110, 255);

    WorldCountLabel _label;
    int _count;

    void Start()
    {
        Vector3 basePos = pad.transform.position;
        Renderer body = pad.GetComponentInChildren<Renderer>();
        if (body != null) basePos.y = body.bounds.max.y;
        _label = WorldCountLabel.Create(pad.transform, basePos + offset, fontSize);
        Show();
    }

    public void SetCount(float count)
    {
        _count = Mathf.RoundToInt(count);
        pad.OnCountChanged?.Invoke(_count, required);
        Show();
    }

    void Update() => Show();

    void Show()
    {
        if (_label == null) return;
        if (door.IsLatched) { _label.Set(required, required, LatchedColor); return; }
        int seconds = Mathf.Max(1, Mathf.CeilToInt(door.OpenTimeRemaining));
        _label.Set(_count, required, Color.white, seconds.ToString(), HoldTimerColor);
    }
}
