using System;
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

    // 닉네임이 생기면 여기만 바꾸면 된다
    public static string NameOf(Team team) => team == Team.Red ? "레드" : "블루";

    public static void RaiseSystem(string text) => OnSystemMessage?.Invoke(text);

    [SyncVar] public bool isHostPlayer;

    public static Team OpponentTeam = Team.None;
    public static bool OpponentIsHost;

    Health health;
    double nextSendTime;   

    void Awake() => health = GetComponent<Health>();

    public override void OnStartClient()
    {
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

    public override void OnStartLocalPlayer() => Local = this;

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
