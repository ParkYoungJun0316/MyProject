using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 머리 소품 선택 창 — CostumeDesign.md. Tutorial·Interlude의 <see cref="CostumeSignboard"/>가 여닫는다.
///
/// [배치] TutorialCheerNameUI와 같은 방식 — 씬 HUD Canvas 자식으로 1개, 기본 비활성. 이 GameObject
/// 자체가 활성/비활성으로 토글된다.
///
/// [칸은 직접 만들지 않는다 — 소품 목록 SSOT는 PlayerCostume]
/// 소품 종류·순서·아이콘은 플레이어 프리팹의 <see cref="PlayerCostume"/> 배열 한 곳에만 있다. 이 창은
/// 처음 열릴 때 내 플레이어(<see cref="PlayerCostume.Local"/>)의 목록을 읽어 cellTemplate을 복제한다
/// (0번 = 없음, 그 뒤는 배열 순서). 누르면 즉시 Host에 요청한다(확정 버튼 없음). 아이콘만 써서 번역 문자열이 없다.
///
/// [입력·커서] TutorialCheerNameUI와 동일 규칙 — 열려 있는 동안 이동·채팅 잠금(IsOpen 확인),
/// Esc는 이 창이 먼저 소비(EscMenuController가 ConsumedEscThisFrame 확인), 커서는
/// CursorUnlockRequestUtil에 요청만 한다.
/// </summary>
public class CostumePanelUI : MonoBehaviour
{
    [Header("칸 (런타임 생성)")]
    [SerializeField] Transform headGrid;
    [SerializeField] Transform glassesGrid;
    [Tooltip("복제 원본 — 비활성으로 둔다.")]
    [SerializeField] CostumeCell cellTemplate;
    [Tooltip("0번(없음) 칸에 쓰는 아이콘.")]
    [SerializeField] Sprite noneIcon;

    [Header("닫기")]
    [SerializeField] Button closeButton;

    [Header("커서")]
    [Tooltip("창 닫을 때 커서를 다시 잠글지 여부(TutorialCheerNameUI와 동일).")]
    [SerializeField] bool lockCursorOnClose = true;

    /// <summary>창이 열려있는 동안 true — Player.cs 이동 잠금·채팅 잠금 등에 사용.</summary>
    public static bool IsOpen { get; private set; }

    /// <summary>이번 프레임에 Esc로 이 창이 막 닫혔는지 — EscMenuController 이중 소비 방지.</summary>
    public static bool ConsumedEscThisFrame => s_escClosedFrame == Time.frameCount;
    static int s_escClosedFrame = -1;

    readonly List<CostumeCell> _headCells = new();
    readonly List<CostumeCell> _glassesCells = new();
    bool _built;

    // 연달아 누를 때 Host 확정값이 돌아오기 전 값으로 다른 칸을 덮지 않도록, 창이 직접 들고 있는 선택값.
    int _head;
    int _glasses;

    void Awake()
    {
        if (closeButton != null)
            closeButton.onClick.AddListener(Close);
    }

    void OnEnable()
    {
        IsOpen = true;
        CursorUnlockRequestUtil.Request(this);

        _head = 0;
        _glasses = 0;
        if (PlayerCostume.Local != null)
        {
            BuildCells(PlayerCostume.Local);
            PlayerCostume.Local.GetCurrent(out _head, out _glasses);
        }
        RefreshMarks();
    }

    void OnDisable()
    {
        IsOpen = false;

        // 씬 통째 언로드 중이면 목록 제거만 — TutorialCheerNameUI.OnDisable과 같은 이유.
        if (!gameObject.scene.isLoaded)
        {
            CursorUnlockRequestUtil.Forget(this);
            return;
        }
        CursorUnlockRequestUtil.Release(this, lockCursorOnClose);
    }

    public void Open()
    {
        if (gameObject.activeSelf) return;
        gameObject.SetActive(true);
    }

    public void Close()
    {
        if (!gameObject.activeSelf) return;
        gameObject.SetActive(false); // 커서 Release는 OnDisable에서 처리
    }

    public void Toggle()
    {
        if (gameObject.activeSelf) Close();
        else Open();
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            s_escClosedFrame = Time.frameCount;
            Close();
        }
    }

    /// <summary>목록은 모든 플레이어 인스턴스가 같은 프리팹 값이라 한 번만 만든다.</summary>
    void BuildCells(PlayerCostume source)
    {
        if (_built || cellTemplate == null) return;
        _built = true;

        for (int i = 0; i <= source.HeadCount; i++)
        {
            int number = i;
            AddCell(headGrid, _headCells, i == 0 ? noneIcon : source.GetHeadIcon(i),
                    () => Select(number, _glasses));
        }

        for (int i = 0; i <= source.GlassesCount; i++)
        {
            int number = i;
            AddCell(glassesGrid, _glassesCells, i == 0 ? noneIcon : source.GetGlassesIcon(i),
                    () => Select(_head, number));
        }
    }

    void AddCell(Transform grid, List<CostumeCell> cells, Sprite icon, UnityEngine.Events.UnityAction onClick)
    {
        CostumeCell cell = Instantiate(cellTemplate, grid);
        if (cell.icon != null) cell.icon.sprite = icon;
        if (cell.button != null) cell.button.onClick.AddListener(onClick);
        cell.gameObject.SetActive(true);
        cells.Add(cell);
    }

    void Select(int head, int glasses)
    {
        _head = head;
        _glasses = glasses;
        RefreshMarks();

        if (PlayerCostume.Local != null)
            PlayerCostume.Local.RequestSet(head, glasses);
    }

    void RefreshMarks()
    {
        SetMark(_headCells, _head);
        SetMark(_glassesCells, _glasses);
    }

    static void SetMark(List<CostumeCell> cells, int selected)
    {
        for (int i = 0; i < cells.Count; i++)
        {
            GameObject mark = cells[i].selectedMark;
            if (mark == null) continue;
            bool on = i == selected;
            if (mark.activeSelf != on) mark.SetActive(on);
        }
    }
}
