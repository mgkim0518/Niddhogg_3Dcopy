using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

public class Health : NetworkBehaviour, IDamagable
{
    [SyncVar(hook = nameof(OnHpChanged))] int hp = DamageTable.PlayerMaxHp;
    [SyncVar(hook = nameof(OnInvincibleChanged))] public bool invincible;
    [SyncVar] public bool isDead;
    [SyncVar] public Team team;

    public int HP => hp;
    public event Action<DamageInfo> OnDied;   // 서버에서만 발생
    public event Action<bool> OnDeadChanged;     // 모든 클라이언트(호스트 포함)에서 발생

    [Server]
    public void TakeDamage(DamageInfo info)
    {
        if (isDead || invincible || info.attackerTeam == team)
        {
            return;
        }
        if (isBlock(info))
        {
            return;
        }

        hp = Mathf.Max(0, hp - DamageTable.DamageValue(info.type));
        if (hp == 0)
        {
            isDead = true;
            OnDied?.Invoke(info);
        }
    }

    [Server] public void ResetHp() 
    {
        hp = DamageTable.PlayerMaxHp;
        isDead = false; 
    }

    private bool isBlock(DamageInfo info) 
    { 
        /* 방패 들고 Q 누르는 중 + 정면 각도면 true */ 
        return false; 
    }

    private void OnHpChanged(int oldV, int newV) 
    { 
        /* 로컬 플레이어면 체력바 갱신 */ 
    }
    private void OnInvincibleChanged(bool o, bool n) 
    { 
        /* 반투명 머티리얼 on/off */ 
    }

    private void OnDeadHook(bool o, bool n)
    {
        OnDeadChanged?.Invoke(n);
    }
}
