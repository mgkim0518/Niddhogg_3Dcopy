using System;
using UnityEngine;
using Mirror;

public class Core : NetworkBehaviour, IDamagable
{
    [SerializeField] Team team = Team.None;
    [SerializeField] float alertInterval = 3f;
    [Range(0f, 1f)] [SerializeField] float damagedDarken = 0.6f;

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
        crystalRenderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
    }

    void ApplyColor(int currentHp)
    {
        if (crystalRenderers == null || team == Team.None) return;
        block ??= new MaterialPropertyBlock();

        float ratio = (float)currentHp / DamageTable.CoreMaxHp;
        Color baseColor = TeamColors.Of(team);
        Color c = Color.Lerp(baseColor * (1f - damagedDarken), baseColor, ratio);
        c.a = 1f;

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
        ApplyColor(hp);
    }

    public override void OnStartClient()
    {
        ApplyColor(hp);
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
        if (info.attackerTeam == team) return;   // 자기 코어는 못 친다

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

    // 금요일에 UI가 생기기 전까지 쓰는 임시 표시. UI를 붙이면 지운다
    void OnGUI()
    {
        if (winner == Team.None || team != Opponent(winner)) return;   
        Team myTeam = Team.None;
        if (NetworkClient.localPlayer != null)
            myTeam = NetworkClient.localPlayer.GetComponent<Health>().team;

        bool iWon = myTeam == winner;
        string text = myTeam == Team.None ? $"{winner} 승리" : (iWon ? "승리!" : "패배");

        GUIStyle style = new GUIStyle(GUI.skin.label) { fontSize = 48, alignment = TextAnchor.MiddleCenter };
        style.normal.textColor = iWon ? new Color(0.95f, 0.8f, 0.3f) : new Color(0.7f, 0.7f, 0.7f);
        GUI.Label(new Rect(0, Screen.height * 0.35f, Screen.width, 80), text, style);

        GUIStyle sub = new GUIStyle(style) { fontSize = 24 };
        sub.normal.textColor = TeamColors.Of(winner);
        GUI.Label(new Rect(0, Screen.height * 0.35f + 70, Screen.width, 40), $"{winner} 팀 승리", sub);
    }
}
