using System;
using UnityEngine;

public class PlayerEvents : MonoBehaviour
{
    public event Action<bool> OnBlackWhiteChanged; // true=black
    public event Action OnDamaged;
    public event Action OnDied;
    public event Action OnInstantKilled;           // 즉사 판정 (doDie 애니와 동시)
    public event Action OnFallDeath;               // 추락 사망 애니 시작 시점
    public event Action OnRespawned;
    public event Action OnHealed;
    public event Action<int> OnUniqueColorChanged; // -1=해제, 그 외=고유색 활성
    public event Action<PlayerColorType> OnColorTypeChanged; // 네트워크 색 동기화 완료 시

    public void RaiseBlackWhiteChanged(bool isBlack)         => OnBlackWhiteChanged?.Invoke(isBlack);
    public void RaiseDamaged()                               => OnDamaged?.Invoke();
    public void RaiseDied()                                  => OnDied?.Invoke();
    public void RaiseInstantKilled()                         => OnInstantKilled?.Invoke();
    public void RaiseFallDeath()                             => OnFallDeath?.Invoke();
    public void RaiseRespawned()                             => OnRespawned?.Invoke();
    public void RaiseHealed()                                => OnHealed?.Invoke();
    public void RaiseUniqueColorChanged(int colorIndex)      => OnUniqueColorChanged?.Invoke(colorIndex);
    public void RaiseColorTypeChanged(PlayerColorType type)  => OnColorTypeChanged?.Invoke(type);
}