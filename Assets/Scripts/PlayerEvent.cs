using System;
using UnityEngine;

public class PlayerEvents : MonoBehaviour
{
    public event Action<bool> OnBlackWhiteChanged; // true=black
    public event Action<bool> OnDamaged;           // bool isBossAtk
    public event Action OnDied;
    public event Action OnRespawned;

    public void RaiseBlackWhiteChanged(bool isBlack) => OnBlackWhiteChanged?.Invoke(isBlack);
    public void RaiseDamaged(bool isBossAtk) => OnDamaged?.Invoke(isBossAtk);
    public void RaiseDied() => OnDied?.Invoke();
    public void RaiseRespawned() => OnRespawned?.Invoke();
}