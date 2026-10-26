using System;
using UnityEngine;
using Mirror;

public class Core : NetworkBehaviour, IDamagable
{
    [SerializeField] Team team = Team.None;
    [SerializeField] float alertInterval = 3f;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");   // URP Lit
    private static readonly int ColorId = Shader.PropertyToID("_Color");           // Built-in Standard
    MaterialPropertyBlock block;

    [SyncVar(hook = nameof(OnHpChanged))] int hp = DamageTable.CoreMaxHp;

    public Team CoreTeam => team;
    public int HP => hp;

    public static event Action<Team, int> OnCoreHpChanged;   
    public static event Action<Team> OnCoreAttacked;           
    public static event Action<Team> OnMatchEnded;

    private Renderer[] crystalRenderers;
    private double nextAlertTime;   
    private static Team winner = Team.None;   

    void Awake()
    {
        if (team == Team.None && transform.parent != null)
        {
            RespawnPoint rp = transform.parent.GetComponentInChildren<RespawnPoint>(true);
            if (rp != null) team = rp.team;
        }
        if (team == Team.None)
            Debug.LogError($"{name}: 코어 팀이 정해지지 않았습니다.");

        Transform c = transform.Find("Cristal");
        crystalRenderers = GetComponentsInChildren<MeshRenderer>(true);
    }

    void ApplyColor()
    {
        if (crystalRenderers == null || team == Team.None) return;
        block ??= new MaterialPropertyBlock();

        Color c = TeamColors.Of(team);

        foreach (Renderer r in crystalRenderers)
        {
            if (r == null) continue;
            r.GetPropertyBlock(block);
            block.SetColor(BaseColorId, c);
            block.SetColor(ColorId, c);
            r.SetPropertyBlock(block);
        }
    }

    public override void OnStartServer()
    {
        PlayerRespawn.MatchOver = false;
        winner = Team.None;
        hp = DamageTable.CoreMaxHp;
        ApplyColor();
    }

    public override void OnStartClient()
    {
        ApplyColor();
        if (!isServer)
        {
            PlayerRespawn.MatchOver = false;
            winner = Team.None;
        }
    }

    [Server]
    public void TakeDamage(DamageInfo info)
    {
        if (PlayerRespawn.MatchOver || hp <= 0) return;
        if (info.attackerTeam == team) return;   

        hp = Mathf.Max(0, hp - DamageTable.DamageValue(info.type));

        if (NetworkTime.time >= nextAlertTime)
        {
            nextAlertTime = NetworkTime.time + alertInterval;
            RpcCoreAttacked();
        }

        if (hp == 0)
        {
            PlayerRespawn.MatchOver = true;
            RpcMatchEnded(Opponent(team));
        }
    }

    static Team Opponent(Team t) => t == Team.Red ? Team.Blue : Team.Red;

    void OnHpChanged(int oldV, int newV)
    {
        OnCoreHpChanged?.Invoke(team, newV);
    }

    [ClientRpc]
    void RpcCoreAttacked()
    {
        OnCoreAttacked?.Invoke(team);
    }

    [ClientRpc]
    void RpcMatchEnded(Team winTeam)
    {
        PlayerRespawn.MatchOver = true;
        winner = winTeam;
        PlayerInputManager input = FindFirstObjectByType<PlayerInputManager>();
        if (input != null) input.SetGameplayEnabled(false);

        Debug.Log($"[경기 종료] {winTeam} 승리");
        OnMatchEnded?.Invoke(winTeam);
    }
}
