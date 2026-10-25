using System;
using System.Collections;
using UnityEngine;
using Mirror;

[RequireComponent(typeof(Health))]
public class PlayerRespawn : NetworkBehaviour
{
    [SerializeField] float respawnDelay = 3f;
    [SerializeField] float invincibleDuration = 1.5f;
    [SerializeField] float teleportLead = 0.3f;

    [Header("죽어 있는 동안 끌 것 (무기 모델은 넣지 말 것: 인벤토리가 관리)")]
    [Tooltip("다른 플레이어 화면에서 끌 것: 3인칭 몸, 히트박스")]
    [SerializeField] GameObject[] hideForOthers;
    [Tooltip("내 화면에서 끌 것: 1인칭 팔")]
    [SerializeField] GameObject[] hideForSelf;

    public static bool MatchOver;
    public event Action OnRespawnedClient;

    Health health;
    CharacterController controller;

    void Awake()
    {
        health = GetComponent<Health>();
        controller = GetComponent<CharacterController>();
    }

    public override void OnStartServer()
    {
        health.OnDied += HandleDied;
    }

    public override void OnStopServer()
    {
        health.OnDied -= HandleDied;
        StopAllCoroutines();
    }

    [Server]
    void HandleDied(DamageInfo info)
    {
        if (!isClient && controller) controller.enabled = false;
        StartCoroutine(RespawnRoutine());
    }

    [Server]
    IEnumerator RespawnRoutine()
    {
        yield return new WaitForSeconds(respawnDelay);

        Transform point = RespawnPoint.For(health.team);
        if (point == null)
        {
            Debug.LogError($"{health.team} 팀 리스폰 지점이 씬에 없습니다.");
            yield break;
        }

        ServerTeleport(point.position, point.rotation);

        yield return new WaitForSeconds(teleportLead);
        if (MatchOver) yield break;

        health.invincible = true;
        health.ResetHp();
        if (!isClient && controller) controller.enabled = true;

        yield return new WaitForSeconds(invincibleDuration);
        health.invincible = false;
    }

    [Server]
    void ServerTeleport(Vector3 pos, Quaternion rot)
    {
        Teleport(pos, rot);
        TargetTeleport(connectionToClient, pos, rot);
    }

    [TargetRpc]
    void TargetTeleport(NetworkConnectionToClient target, Vector3 pos, Quaternion rot)
    {
        Teleport(pos, rot);
    }

    void Teleport(Vector3 pos, Quaternion rot)
    {
        bool wasEnabled = controller && controller.enabled;
        if (controller) controller.enabled = false;

        transform.SetPositionAndRotation(pos, rot);

        if (controller) controller.enabled = wasEnabled;
    }

    public override void OnStartClient()
    {
        health.OnDeadChanged += HandleDeadChanged;
        if (health.isDead) SetAlive(false);
    }

    public override void OnStopClient()
    {
        health.OnDeadChanged -= HandleDeadChanged;
    }

    void HandleDeadChanged(bool dead)
    {
        SetAlive(!dead);
        if (!dead) OnRespawnedClient?.Invoke();
    }

    void SetAlive(bool alive)
    {
        GameObject[] targets = isLocalPlayer ? hideForSelf : hideForOthers;
        foreach (GameObject go in targets)
            if (go) go.SetActive(alive);

        if (controller) controller.enabled = alive;
    }
}
