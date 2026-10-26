using System.Collections.Generic;
using Mirror;
using UnityEngine;

// Player 프리팹에 붙인다. readyMark에는 머리 위 체크 오브젝트(SpriteRenderer + Billboard)를 연결
public class PlayerReady : NetworkBehaviour
{
    public static readonly List<PlayerReady> All = new List<PlayerReady>();
    public static PlayerReady Local;

    [SyncVar(hook = nameof(OnReadyChanged))] public bool isReady;
    [SerializeField] GameObject readyMark;

    public override void OnStartClient()
    {
        All.Add(this);
        MatchFlow.OnPhaseChanged += HandlePhase;
        Refresh();
    }

    public override void OnStopClient()
    {
        All.Remove(this);
        MatchFlow.OnPhaseChanged -= HandlePhase;
    }

    public override void OnStartLocalPlayer() => Local = this;
    public override void OnStopLocalPlayer() { if (Local == this) Local = null; }

    void OnReadyChanged(bool _, bool now) => Refresh();
    void HandlePhase(GamePhase _) => Refresh();

    void Refresh()
    {
        bool lobby = MatchFlow.Instance == null || MatchFlow.Instance.phase != GamePhase.Playing;
        if (readyMark) readyMark.SetActive(isReady && lobby);
    }

    [Command]
    public void CmdSetReady()
    {
        if (MatchFlow.Instance == null || MatchFlow.Instance.phase != GamePhase.Lobby) return;
        isReady = true;
        MatchFlow.Instance.ServerCheckAllReady();
    }
}
