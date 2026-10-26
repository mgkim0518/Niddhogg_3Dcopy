using System;
using Mirror;
using UnityEngine;

public enum GamePhase { Lobby, Countdown, Playing }

// PlayScene에 빈 오브젝트(MatchFlow)를 만들고 NetworkIdentity와 함께 붙인다
public class MatchFlow : NetworkBehaviour
{
    public const float CountdownSeconds = 3f;

    public static MatchFlow Instance { get; private set; }
    public static event Action<GamePhase> OnPhaseChanged;

    // 씬에 MatchFlow가 없으면(테스트 씬 등) 막지 않는다
    public static bool IsPlaying => Instance == null || Instance.phase == GamePhase.Playing;

    [SyncVar(hook = nameof(OnPhaseHook))] public GamePhase phase = GamePhase.Lobby;
    [SyncVar] public double countdownEndTime;

    public float Remaining => Mathf.Max(0f, (float)(countdownEndTime - NetworkTime.time));

    void Awake() => Instance = this;
    void OnDestroy() { if (Instance == this) Instance = null; }

    void OnPhaseHook(GamePhase oldP, GamePhase newP) => OnPhaseChanged?.Invoke(newP);

    [Server]
    public void ServerCheckAllReady()
    {
        if (phase != GamePhase.Lobby) return;

        int ready = 0;
        foreach (NetworkConnectionToClient conn in NetworkServer.connections.Values)
        {
            if (conn.identity == null) return;
            if (!conn.identity.GetComponent<PlayerReady>().isReady) return;
            ready++;
        }
        if (ready < 2) return;

        countdownEndTime = NetworkTime.time + CountdownSeconds;
        phase = GamePhase.Countdown;
        Invoke(nameof(ServerStartPlay), CountdownSeconds);
    }

    [Server] void ServerStartPlay() => phase = GamePhase.Playing;

    // 카운트다운 중에 한 명이 나가면 로비로 되돌린다
    [Server]
    public void ServerResetToLobby()
    {
        if (phase != GamePhase.Countdown) return;
        CancelInvoke(nameof(ServerStartPlay));

        foreach (NetworkConnectionToClient conn in NetworkServer.connections.Values)
            if (conn.identity != null) conn.identity.GetComponent<PlayerReady>().isReady = false;

        phase = GamePhase.Lobby;
    }
}
