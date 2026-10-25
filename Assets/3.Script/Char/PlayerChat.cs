using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

public struct SystemChatMessage : NetworkMessage
{
    public string text;
}


public class PlayerChat : NetworkBehaviour
{
    public const int MaxLength = 80;
    const float Cooldown = 0.5f;

    public static PlayerChat Local;                         
    public static event Action<Team, string> OnChatMessage; 
    public static event Action<string> OnSystemMessage;     

    public const int MaxNameLength = 12;

    // 팀 → 플레이어 이름. 채팅, 나감 알림, 승리 표시가 모두 NameOf를 거치므로 여기만 바꾸면 된다
    static readonly Dictionary<Team, string> names = new Dictionary<Team, string>();

    public static string NameOf(Team team)
    {
        if (names.TryGetValue(team, out string n) && !string.IsNullOrEmpty(n)) return n;
        return team == Team.Red ? "레드" : "블루";   // 이름을 아직 못 받았을 때
    }

    [SyncVar(hook = nameof(OnNameChanged))] public string playerName;

    public static void RaiseSystem(string text) => OnSystemMessage?.Invoke(text);

    [SyncVar] public bool isHostPlayer;

    public static Team OpponentTeam = Team.None;
    public static bool OpponentIsHost;

    Health health;
    double nextSendTime;   

    void Awake() => health = GetComponent<Health>();

    public override void OnStartClient()
    {
        // 접속 시점에 이미 이름이 있으면 바로 등록 (훅이 초기값에 안 불릴 수도 있으므로)
        if (!string.IsNullOrEmpty(playerName)) names[health.team] = playerName;

        if (isOwned) return;   
        OpponentTeam = health.team;
        OpponentIsHost = isHostPlayer;
    }

    public override void OnStopClient()
    {
        if (!isOwned && NetworkClient.isConnected)
        {
            OpponentTeam = Team.None;
            OpponentIsHost = false;
        }
    }

    public override void OnStartLocalPlayer()
    {
        Local = this;
        CmdSetName(ConnectionUI.LocalName);   // 접속 패널에서 입력한 이름을 서버로
    }

    [Command]
    void CmdSetName(string name)
    {
        name = CleanName(name);
        playerName = name.Length > 0 ? name : (health.team == Team.Red ? "레드" : "블루");
        // SyncVar 훅은 클라이언트(호스트 포함)에서만 불린다. 데디케이티드 서버의 나감/재시작 알림에도 이름이 나오도록 직접 등록
        names[health.team] = playerName;
    }

    void OnNameChanged(string oldName, string newName)
    {
        names[health.team] = newName;
    }

    public static string CleanName(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        s = s.Replace("<", "").Replace(">", "").Replace("\n", "").Trim();
        return s.Length > MaxNameLength ? s.Substring(0, MaxNameLength) : s;
    }

    public override void OnStopLocalPlayer()
    {
        if (Local == this) Local = null;
    }

    public void Send(string message)
    {
        message = Clean(message);
        if (message.Length == 0) return;
        CmdSend(message);
    }

    [Command]
    void CmdSend(string message)
    {
        if (NetworkTime.time < nextSendTime) return;
        nextSendTime = NetworkTime.time + Cooldown;

        message = Clean(message);
        if (message.Length == 0) return;

        RpcReceive(health.team, message);
    }

    [ClientRpc]
    void RpcReceive(Team team, string message)
    {
        OnChatMessage?.Invoke(team, message);
    }

    static string Clean(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        s = s.Replace("<", "").Replace(">", "").Trim();
        return s.Length > MaxLength ? s.Substring(0, MaxLength) : s;
    }
}
