using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Mirror;

public struct RestartRequestMessage : NetworkMessage { }
public class CustomNetworkManager: NetworkManager
{
    readonly HashSet<int> restartVotes = new HashSet<int>();

    public override void Awake()
    {
        base.Awake();

        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "-port" && ushort.TryParse(args[i + 1], out ushort port) && transport is PortTransport pt)
            {
                pt.Port = port;
                Debug.Log($"[서버] 포트 {port} 사용");
            }
        }
    }

    public override void OnStartServer()
    {
        restartVotes.Clear();
        NetworkServer.RegisterHandler<RestartRequestMessage>(OnRestartRequest);
    }

    void OnRestartRequest(NetworkConnectionToClient conn, RestartRequestMessage msg)
    {
        if (!PlayerRespawn.MatchOver) return; 

        if (conn is LocalConnectionToClient)
        {
            RestartMatch();
            return;
        }

        if (mode != NetworkManagerMode.ServerOnly) return;   

        if (!restartVotes.Add(conn.connectionId)) return;    

        if (conn.identity != null)
        {
            Team team = conn.identity.GetComponent<Health>().team;
            NetworkServer.SendToAll(new SystemChatMessage
            {
                text = $"{PlayerChat.NameOf(team)}님이 재시작을 원합니다 ({restartVotes.Count}/{NetworkServer.connections.Count})"
            });
        }

        if (restartVotes.Count >= NetworkServer.connections.Count)
            RestartMatch();
    }

    [Server]
    public void RestartMatch()
    {
        restartVotes.Clear();
        ServerChangeScene(SceneManager.GetActiveScene().name);
    }

    public override void OnServerAddPlayer(NetworkConnectionToClient conn)
    {
        Team team = Team.Blue;
        foreach (var c in NetworkServer.connections.Values)
            if (c.identity != null && c.identity.GetComponent<Health>().team == Team.Blue)
                team = Team.Red;

        Transform start = StartPoint.For(team);
        if (start == null) start = RespawnPoint.For(team);
        Vector3 pos = start != null ? start.position : Vector3.zero;
        Quaternion rot = start != null ? start.rotation : Quaternion.identity;
        if (start == null) Debug.LogError($"{team} 팀 RespawnPoint가 씬에 없습니다.");

        GameObject player = Instantiate(playerPrefab, pos, rot);
        player.GetComponent<Health>().team = team;
        player.GetComponent<PlayerChat>().isHostPlayer = conn is LocalConnectionToClient;
        NetworkServer.AddPlayerForConnection(conn, player);
    }

    public override void OnServerDisconnect(NetworkConnectionToClient conn)
    {
        if (conn.identity != null && !(conn is LocalConnectionToClient))
        {
            Team team = conn.identity.GetComponent<Health>().team;
            NetworkServer.SendToAll(new SystemChatMessage
            {
                text = $"{PlayerChat.NameOf(team)}님이 나갔습니다"
            });
        }

        restartVotes.Remove(conn.connectionId);
        base.OnServerDisconnect(conn);

        if (mode == NetworkManagerMode.ServerOnly && NetworkServer.connections.Count == 0)
            RestartMatch();
    }

    public override void OnStartClient()
    {
        NetworkClient.RegisterHandler<SystemChatMessage>(msg => PlayerChat.RaiseSystem(msg.text));
    }

    public override void OnClientSceneChanged()
    {
        PlayerCombat.isInputBlocked = false;
        base.OnClientSceneChanged();
    }

    public override void OnClientDisconnect()
    {
        if (!NetworkServer.active)
        {
            string msg = (PlayerChat.OpponentIsHost && PlayerChat.OpponentTeam != Team.None)
                ? $"{PlayerChat.NameOf(PlayerChat.OpponentTeam)}님이 나갔습니다"
                : "서버와의 연결이 끊어졌습니다";
            PlayerChat.RaiseSystem(msg);
            ConnectionUI.pendingMessage = msg;   
        }

        PlayerChat.OpponentTeam = Team.None;
        PlayerChat.OpponentIsHost = false;

        base.OnClientDisconnect();

        if (!NetworkServer.active)
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
