using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// "겹쳐 놓은 변형판 N개 중 시드로 골라 활성화한다"의 공통 뼈대.
/// 구현은 두 가지 — 1회짜리 <see cref="StageVariantPicker"/>, 라운드용 <see cref="StageVariantRoundPicker"/>.
///
/// [이 컴포넌트가 하는 일 / 하지 않는 일]
///  하는 일   : 후보 수집 · 시드 기반 뽑기 · 앵커로 이동 · 활성화 토글
///  하지 않는 일: **결과를 네트워크에 알리는 것.** 뽑은 결과를 NV에 박는 것은 그 스테이지의
///                디렉터 몫이다(T5는 `T5RoundState`에 넣어야 하고, T4는 넣을 필요가 없다).
///                여기서 NV까지 건드리면 "어느 변형판인가"의 답이 두 군데 생긴다.
///
/// [왜 전 머신이 같은 것을 고르는가]
///  입력이 `NetworkSessionData.Seed`(방 전체 공유) + salt뿐이고, 후보 순서는 같은 씬 파일에서
///  이름순으로 정렬해 얻으므로 전 머신이 같은 배열·같은 난수를 얻는다. RPC도 NV도 필요 없다.
///
/// [salt — 계층 경로 자동 배정]
///  시드는 방 전체에 하나뿐이라 그대로 쓰면 시드를 쓰는 모든 컴포넌트가 같은 난수를 뽑는다.
///  그래서 각자 고유한 덧칠 값(salt)을 섞는데, **인스펙터에 손으로 넣으면 사람이 실수한다**
///  (실제로 '전 인스턴스 동일 색' 버그를 낸 이력이 있다). 그래서 `SceneStableRegistry`로
///  씬 계층 경로 순서에 따라 번호를 자동 배정해 섞는다 — `WallLineRandomizer`·`Breakable`과 같은 방식.
///
///  ⚠️ 오브젝트 이름이나 부모를 바꾸면 번호가 달라져 그 판의 뽑기 결과가 통째로 바뀐다.
///     어차피 런마다 시드가 바뀌므로 문제는 아니지만, "아까 그 배치 다시" 는 되지 않는다.
///
/// [⚠️ 변형판은 씬에 '비활성'으로 저장할 것]
///  `CapacityTile.Awake()`처럼 휴지 위치를 기준으로 무언가를 만드는 컴포넌트가 안에 있으면,
///  Awake를 마친 뒤에 판을 옮길 때 그 기준만 원래 자리에 남는다. 비활성 오브젝트는 Awake가
///  돌지 않으므로 **'선택 → 이동 → 활성화'** 순서를 지키면 안전하다(ActivateOnly가 그 순서다).
///  활성 상태로 저장돼 있으면 Awake에서 강제로 꺼서 막는다.
/// </summary>
[DefaultExecutionOrder(-200)]
public abstract class StageVariantPickerBase : MonoBehaviour
{
    [Header("변형판 후보")]
    [Tooltip("후보 루트들. 비워두면 variantsRoot의 자식 중 childNamePrefix로 시작하는 것을 이름순으로 수집한다.\n" +
             "전부 씬에 '비활성'으로 저장할 것.")]
    [SerializeField] Transform[] variants = new Transform[0];

    [Tooltip("자동 수집할 때 볼 부모. 비우면 이 오브젝트 자신.")]
    [SerializeField] Transform variantsRoot;

    [Tooltip("자동 수집 접두사. 예: \"Map_\". 비어 있으면 자동 수집을 하지 않는다.")]
    [SerializeField] string childNamePrefix = "";

    [Header("배치")]
    [Tooltip("고른 변형판을 옮겨 놓을 위치. 비우면 놓인 자리에서 그대로 활성화한다.")]
    [SerializeField] Transform anchor;

    [Tooltip("활성 상태로 저장된 변형판을 Awake에서 강제로 끈다(위 ⚠️ 주석). 보통 켜 둘 것.")]
    [SerializeField] bool forceInactiveOnAwake = true;

    // 다른 파일의 salt: 0x050AD5E7, 0x43484153, 0x5716D000, 0x4D4F5554, 0x5B1DE000, 0x52554E52,
    //                  0x434F4C57, 0x574C525A, 0x4D430001, 0x53504852, 0x54354D50, 0x54355255, 0x5435434C
    const int VariantSeedSalt = unchecked((int)0x56415249); // "VARI"
    const int IndexMix        = unchecked((int)0x9E3779B9); // 황금비 상수 — WallLineRandomizer와 동일

    static readonly SceneStableRegistry<StageVariantPickerBase> s_registry = new SceneStableRegistry<StageVariantPickerBase>();

    readonly List<Transform> _resolved = new List<Transform>();
    int _netIndex = -1;

    /// <summary>후보 개수.</summary>
    public int Count => _resolved.Count;

    /// <summary>index번째 변형판. 범위 밖이면 null.</summary>
    public Transform Get(int index) =>
        index >= 0 && index < _resolved.Count ? _resolved[index] : null;

    protected virtual void Awake()
    {
        _netIndex = s_registry.Register(this);
        ResolveVariants();

        if (!forceInactiveOnAwake) return;

        foreach (Transform v in _resolved)
        {
            if (v == null || !v.gameObject.activeSelf) continue;

            Debug.LogWarning(
                $"[{GetType().Name}] 변형판 '{v.name}'이 활성 상태로 저장돼 있다 — 비활성으로 저장할 것. " +
                "(활성 상태로 두면 이동 후 휴지 위치 기준 컴포넌트가 원래 자리에 남는다.)", v);
            v.gameObject.SetActive(false);
        }
    }

    protected virtual void OnDestroy() => s_registry.Unregister(this, _netIndex);

    void ResolveVariants()
    {
        _resolved.Clear();

        if (variants != null && variants.Length > 0)
        {
            foreach (Transform v in variants)
                if (v != null) _resolved.Add(v);
            return;
        }

        if (string.IsNullOrEmpty(childNamePrefix)) return;

        Transform root = variantsRoot != null ? variantsRoot : transform;
        foreach (Transform child in root)
            if (child.name.StartsWith(childNamePrefix)) _resolved.Add(child);

        // 전 머신이 같은 순서를 얻어야 인덱스가 같은 것을 가리킨다.
        _resolved.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
    }

    /// <summary>
    /// 후보 중 count개를 **중복 없이** 시드로 뽑는다. 후보보다 많이 요구하면 순환해서 채운다
    /// (후보가 1개뿐인 씬에서도 라운드가 굴러가게 — 그 경우 같은 판이 반복된다).
    /// </summary>
    protected int[] DrawIndices(int count)
    {
        var result = new int[Mathf.Max(0, count)];
        if (_resolved.Count == 0 || result.Length == 0) return result;

        var pool = new List<int>(_resolved.Count);
        for (int i = 0; i < _resolved.Count; i++) pool.Add(i);

        System.Random rng = CreateRng();
        for (int i = pool.Count - 1; i > 0; i--)
        {
            int j = rng.Next(0, i + 1);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }

        for (int i = 0; i < result.Length; i++) result[i] = pool[i % pool.Count];
        return result;
    }

    /// <summary>이 인스턴스 전용 난수. 씬 계층 경로 번호를 섞어 인스턴스마다 다른 시퀀스를 준다.</summary>
    protected System.Random CreateRng() =>
        new System.Random(NetworkSessionData.Seed ^ VariantSeedSalt ^ (_netIndex * IndexMix));

    /// <summary>
    /// index번째만 활성화하고 나머지는 전부 끈다.
    /// ⚠️ 순서 고정 — **옮긴 다음에 켠다.** 켠 다음에 옮기면 휴지 위치 기준으로 무언가를 만드는
    /// 컴포넌트(CapacityTile 등)가 옮기기 전 자리를 기준으로 삼아 버린다.
    /// </summary>
    public Transform ActivateOnly(int index)
    {
        Transform chosen = Get(index);

        for (int i = 0; i < _resolved.Count; i++)
        {
            Transform v = _resolved[i];
            if (v == null) continue;

            if (i != index)
            {
                if (v.gameObject.activeSelf) v.gameObject.SetActive(false);
                continue;
            }

            if (anchor != null)
                v.SetPositionAndRotation(anchor.position, anchor.rotation);
            if (!v.gameObject.activeSelf) v.gameObject.SetActive(true);
        }

        return chosen;
    }

    /// <summary>후보 전부 비활성화.</summary>
    public void DeactivateAll()
    {
        foreach (Transform v in _resolved)
            if (v != null && v.gameObject.activeSelf) v.gameObject.SetActive(false);
    }
}
