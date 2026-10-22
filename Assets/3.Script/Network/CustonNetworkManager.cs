using UnityEngine;
using Mirror;

public class CustonNetworkManager: NetworkManager
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
        NetworkServer.AddPlayerForConnection(conn, player);
    }
}
