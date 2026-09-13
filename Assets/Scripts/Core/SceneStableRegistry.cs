using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// ClientRpc가 씬 오브젝트를 int ID로 지목할 때 쓰는 stable ID 레지스트리.
/// ID = 씬 계층 경로(+sibling index) 정렬 순서 — Host/Client가 같은 씬 파일을 로드하면 항상 같다.
/// Awake/활성화 순서는 Host/Client 간 달라질 수 있어 쓰지 않는다(ArrowTrap 2026-07-27).
/// 재구성 기준은 Scene.handle(같은 씬 리로드도 handle이 바뀜). "마지막 인스턴스 OnDestroy에서 리셋"은
/// 새 씬 Awake가 이전 씬 OnDestroy보다 먼저 올 수 있어 쓰면 안 된다(2026-09-13 Client 경고 사인 소실).
/// </summary>
public sealed class SceneStableRegistry<T> where T : Component
{
    readonly Dictionary<int, T> _byId = new Dictionary<int, T>();
    readonly Dictionary<T, int> _idOf = new Dictionary<T, int>();
    int _sceneHandle = int.MinValue;

    /// <summary>현재 씬 세대에서 배정된 전체 개수. 개별 파괴로 줄지 않는다(NV 슬롯 개수 고정용).</summary>
    public int BuiltCount { get; private set; }

    /// <summary>Awake에서 호출. 씬 배치 오브젝트가 아니면(런타임 Instantiate 등) -1.</summary>
    public int Register(T self)
    {
        int handle = self.gameObject.scene.handle;
        if (_sceneHandle != handle) Build(handle);
        return _idOf.TryGetValue(self, out int id) ? id : -1;
    }

    public T Get(int id) => _byId.TryGetValue(id, out T t) ? t : null;

    /// <summary>OnDestroy에서 호출. 이전 세대 인스턴스가 새 세대의 같은 ID를 지우지 않도록 자기 자신일 때만 제거.</summary>
    public void Unregister(T self, int id)
    {
        if (!_byId.TryGetValue(id, out T t) || !ReferenceEquals(t, self)) return;
        _byId.Remove(id);
        _idOf.Remove(self);
    }

    void Build(int handle)
    {
        _sceneHandle = handle;
        _byId.Clear();
        _idOf.Clear();

        // 아직 언로드 안 된 이전 씬 인스턴스도 검색되므로 이번 씬 소속만 남긴다.
        T[] all = UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(c => c.gameObject.scene.handle == handle)
            .OrderBy(c => GetHierarchyPath(c.transform), StringComparer.Ordinal)
            .ToArray();

        for (int i = 0; i < all.Length; i++)
        {
            _byId[i] = all[i];
            _idOf[all[i]] = i;
        }
        BuiltCount = all.Length;
    }

    // 같은 이름 형제가 있으면 이름만으로는 동률 → FindObjectsByType 반환 순서(프로세스 간 비보장)에
    // 의존하게 되므로 sibling index를 붙여 완전히 결정적으로 만든다.
    static string GetHierarchyPath(Transform t)
    {
        string path = t.name + "#" + t.GetSiblingIndex().ToString("D4");
        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "#" + t.GetSiblingIndex().ToString("D4") + "/" + path;
        }
        return path;
    }
}
