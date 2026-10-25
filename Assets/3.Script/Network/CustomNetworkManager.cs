using UnityEngine;
using Mirror;

public class CustomNetworkManager: NetworkManager
{
    [SerializeField] Transform redStart, blueStart;

    public override void OnServerAddPlayer(NetworkConnectionToClient conn)
    {
        Team team = Team.Blue;
        foreach (var c in NetworkServer.connections.Values)
            if (c.identity != null && c.identity.GetComponent<Health>().team == Team.Blue)
                team = Team.Red;

        Transform start = team == Team.Red ? redStart : blueStart;

        GameObject player = Instantiate(playerPrefab, start.position, start.rotation);
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

        base.OnServerDisconnect(conn);
    }

    public override void OnStartClient()
    {
        NetworkClient.RegisterHandler<SystemChatMessage>(msg => PlayerChat.RaiseSystem(msg.text));
    }

    public override void OnClientDisconnect()
    {
        if (!NetworkServer.active) 
        {
            if (PlayerChat.OpponentIsHost && PlayerChat.OpponentTeam != Team.None)
                PlayerChat.RaiseSystem($"{PlayerChat.NameOf(PlayerChat.OpponentTeam)}님이 나갔습니다");
            else
                PlayerChat.RaiseSystem("서버와의 연결이 끊어졌습니다");
        }

        PlayerChat.OpponentTeam = Team.None;
        PlayerChat.OpponentIsHost = false;

        base.OnClientDisconnect();
    }
}
