using UnityEngine;
using Mirror;

// 던질 수 있는 월드 아이템(Sword_World, Shield_World, Greatsword_World) 루트에 붙인다.
// NetworkIdentity, WorldItem과 같은 오브젝트.
//
// 동작
//   서버: 눈 위치에서 크로스라인 방향으로 진짜 궤적을 계산하고, 맞았는지 판정한다.
//   클라이언트: 서버가 보낸 시작점, 속도, 시작 시각으로 같은 궤적을 직접 계산해서 그린다.
//              처음 blendTime 동안은 던진 사람의 손 위치에서 시작해 진짜 궤적으로 합류한다.
//   땅/벽에 닿으면 멈추고 다시 주울 수 있게 된다.
public class ThrownItem : NetworkBehaviour
{
    [Header("비행")]
    [Tooltip("맞는 대상과 막는 것: Player, Wall, Ground (Core를 넣으면 코어도 맞는다)")]
    [SerializeField] LayerMask hitMask;
    [Tooltip("땅을 찾을 때 쓰는 레이어: Wall, Ground")]
    [SerializeField] LayerMask groundMask;
    [SerializeField] float radius = 0.15f;
    [SerializeField] float maxFlightTime = 3f;
    [Tooltip("손 위치에서 진짜 궤적으로 합류하는 시간")]
    [SerializeField] float blendTime = 0.12f;

    [Header("모양")]
    [Tooltip("날아갈 때 진행 방향 기준 회전 보정 (모델 축에 맞춰 조정)")]
    [SerializeField] Vector3 flightRotationOffset;
    [Tooltip("초당 회전 각도. 방패를 원반처럼 돌리려면 720 정도")]
    [SerializeField] float spinSpeed;
    [Tooltip("땅에 놓일 때 높이 보정")]
    [SerializeField] float restHeight = 0.05f;

    [SyncVar(hook = nameof(OnFlyingChanged))] bool isFlying;
    [SyncVar(hook = nameof(OnRestChanged))] Vector3 restPos;
    [SyncVar(hook = nameof(OnRestRotChanged))] Quaternion restRot = Quaternion.identity;

    public bool IsFlying => isFlying;

    // 비행 상태 (서버와 클라이언트 모두)
    Vector3 startPos;
    Vector3 velocity;
    double launchTime;
    Vector3 handOffset;   // 클라이언트 전용: 손 위치 - 시작점
    float spin;
    bool clientFlying;    // 클라이언트 전용: RPC를 받은 즉시 날리기 시작 (SyncVar 도착을 기다리지 않음)

    // 서버 전용
    DamageInfo damage;
    Transform thrower;
    Vector3 lastPos;
    SpecialItem special;
    Collider[] colliders;

    void Awake()
    {
        special = GetComponent<SpecialItem>();
        colliders = GetComponentsInChildren<Collider>(true);
    }

    public override void OnStartServer()
    {
        restPos = transform.position;
        restRot = transform.rotation;
    }

    // ---------- 서버 ----------

    [Server]
    public void Launch(Vector3 start, Vector3 vel, Transform from, DamageType type, Team team, int handSocket)
    {
        startPos = start;
        velocity = vel;
        launchTime = NetworkTime.time;
        thrower = from;
        lastPos = start;
        spin = 0f;
        damage = new DamageInfo { type = type, attackerTeam = team, hitDirection = vel.normalized };

        isFlying = true;
        transform.position = start;
        SetCollidersEnabled(false);   // 날아가는 동안은 줍기 레이캐스트에 안 걸리게

        uint throwerId = from.GetComponent<NetworkIdentity>().netId;
        RpcLaunch(start, vel, launchTime, throwerId, handSocket);
    }

    [ServerCallback]
    void Update()
    {
        if (!isFlying) return;

        float t = (float)(NetworkTime.time - launchTime);
        Vector3 pos = TrajectoryAt(t);
        Vector3 step = pos - lastPos;

        if (step.sqrMagnitude > 0.000001f)
        {
            RaycastHit[] hits = Physics.SphereCastAll(lastPos, radius, step.normalized, step.magnitude,
                hitMask, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (RaycastHit hit in hits)
            {
                if (thrower != null && hit.collider.transform.IsChildOf(thrower)) continue;   // 던진 사람은 통과

                IDamagable target = hit.collider.GetComponentInParent<IDamagable>();
                if (target != null)
                {
                    target.TakeDamage(damage);
                    Land(DropToGround(hit.point));   // 맞힌 자리 바닥에 떨어진다
                }
                else
                {
                    // 벽이나 땅. hit.point가 0이면 시작부터 겹친 경우
                    Vector3 p = hit.point == Vector3.zero ? lastPos : hit.point + hit.normal * restHeight;
                    Land(p);
                }
                return;
            }
        }

        lastPos = pos;
        if (t >= maxFlightTime) Land(DropToGround(pos));
    }

    [Server]
    Vector3 DropToGround(Vector3 from)
    {
        if (Physics.Raycast(from + Vector3.up * 0.2f, Vector3.down, out RaycastHit hit, 50f, groundMask,
                QueryTriggerInteraction.Ignore))
            return hit.point + Vector3.up * restHeight;
        return from;
    }

    [Server]
    void Land(Vector3 pos)
    {
        isFlying = false;
        clientFlying = false;   // 호스트 화면
        thrower = null;

        Quaternion rot = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        transform.SetPositionAndRotation(pos, rot);
        restPos = pos;
        restRot = rot;
        SetCollidersEnabled(true);

        if (special != null) special.Drop(pos);
    }

    void SetCollidersEnabled(bool on)
    {
        foreach (Collider c in colliders) if (c) c.enabled = on;
    }

    Vector3 TrajectoryAt(float t) => startPos + velocity * t + 0.5f * Physics.gravity * t * t;
    Vector3 VelocityAt(float t) => velocity + Physics.gravity * t;

    // ---------- 클라이언트 ----------

    [ClientRpc]
    void RpcLaunch(Vector3 start, Vector3 vel, double time, uint throwerId, int handSocket)
    {
        startPos = start;
        velocity = vel;
        launchTime = time;
        spin = 0f;
        handOffset = Vector3.zero;

        // 내 화면에서 보이는 던진 사람의 손 위치에서 출발
        if (NetworkClient.spawned.TryGetValue(throwerId, out NetworkIdentity id))
        {
            PlayerInventory inv = id.GetComponent<PlayerInventory>();
            Transform hand = inv != null ? inv.GetSocket(handSocket) : null;
            if (hand != null) handOffset = hand.position - start;
        }

        clientFlying = true;
        SetCollidersEnabled(false);
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true)) r.enabled = true;
        transform.position = start + handOffset;
    }

    void OnFlyingChanged(bool _, bool flying)
    {
        if (!flying) clientFlying = false;
    }

    void LateUpdate()
    {
        if (!clientFlying) return;

        float t = Mathf.Max(0f, (float)(NetworkTime.time - launchTime));
        float blend = blendTime > 0f ? Mathf.Clamp01(t / blendTime) : 1f;
        Vector3 pos = TrajectoryAt(t) + handOffset * (1f - blend);

        Vector3 v = VelocityAt(t);
        spin += spinSpeed * Time.deltaTime;
        Quaternion look = v.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(v) : transform.rotation;
        transform.SetPositionAndRotation(pos, look * Quaternion.Euler(flightRotationOffset) * Quaternion.Euler(0f, spin, 0f));
    }

    void OnRestChanged(Vector3 _, Vector3 pos)
    {
        clientFlying = false;
        if (isServer) return;
        transform.position = pos;
        SetCollidersEnabled(true);
    }

    void OnRestRotChanged(Quaternion _, Quaternion rot)
    {
        if (isServer) return;
        transform.rotation = rot;
    }

    public override void OnStartClient()
    {
        if (isServer) return;
        if (!isFlying)
        {
            transform.SetPositionAndRotation(restPos, restRot);
        }
    }
}
