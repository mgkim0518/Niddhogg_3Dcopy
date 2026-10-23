using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

public class Health : NetworkBehaviour, IDamagable
{
    [Header("방패 막기")]
    [Tooltip("정면 기준 막을 수 있는 전체 각도 (120이면 좌우 60도씩)")]
    [SerializeField] float blockAngle = 120f;

    [SyncVar(hook = nameof(OnHpChanged))] int hp = DamageTable.PlayerMaxHp;
    [SyncVar(hook = nameof(OnInvincibleChanged))] public bool invincible;
    [SyncVar(hook = nameof(OnDeadHook))] public bool isDead;
    [SyncVar] public Team team;

    public int HP => hp;
    public event Action<DamageInfo> OnDied;   
    public event Action<bool> OnDeadChanged;
    public event Action OnRevived;

    private PlayerCombat combat;

    void Awake()
    {
        combat = GetComponent<PlayerCombat>();   
    }

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
        OnRevived?.Invoke();
    }

    private bool isBlock(DamageInfo info) 
    {
        if (combat == null || !combat.isBlocking) return false;

        Vector3 facing = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        Vector3 incoming = Vector3.ProjectOnPlane(-info.hitDirection, Vector3.up).normalized;
        if (incoming.sqrMagnitude < 0.0001f) return false;   

        return Vector3.Angle(facing, incoming) <= blockAngle * 0.5f;
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
        Debug.Log($"[{(isServer ? "서버" : "클라")}] {name} isDead: {o} → {n}");
        OnDeadChanged?.Invoke(n);
    }
}
