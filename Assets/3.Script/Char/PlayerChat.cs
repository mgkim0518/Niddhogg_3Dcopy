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
    // 닉네임이 생기면 여기만 바꾸면 된다
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
        CmdSetName(ConnectionUI.LocalName);
        Local = this;
    }

    [Command]
    void CmdSetName(string name)
    {
        name = CleanName(name);
        playerName = name.Length > 0 ? name : (health.team == Team.Red ? "레드" : "블루");
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
