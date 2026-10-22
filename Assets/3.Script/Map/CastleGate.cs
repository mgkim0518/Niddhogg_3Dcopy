using UnityEngine;
using Mirror;

// Castle 프리팹 안의 Gate 오브젝트에 붙인다 (NetworkIdentity 필요).
public class CastleGate : NetworkBehaviour
{
    [Header("구성")]
    [SerializeField] Transform door;
    [Tooltip("문 아래 공간. Is Trigger 체크된 BoxCollider")]
    [SerializeField] BoxCollider doorZone;
    [SerializeField] GateButton[] buttons;

    [Header("움직임")]
    [Tooltip("열렸을 때 문이 올라가는 높이 (문짝 높이 정도)")]
    [SerializeField] float openHeight = 3f;
    [SerializeField] float openSpeed = 4f;    
    [SerializeField] float closeSpeed = 2f;   

    [Header("타이머")]
    [SerializeField] float openDuration = 10f;
    [Tooltip("닫히는 중에 문 아래에 플레이어가 있으면 다시 올리고 이만큼 기다린다")]
    [SerializeField] float blockedRetryDelay = 1f;
    [SerializeField] LayerMask playerMask;

    [SyncVar(hook = nameof(OnOpenChanged))] bool isOpen;

    public bool IsOpen => isOpen;

    Vector3 closedLocalPos;
    Vector3 openLocalPos;
    double closeAt;   
    readonly Collider[] zoneBuffer = new Collider[4];

    void Awake()
    {
        closedLocalPos = door.localPosition;
        openLocalPos = closedLocalPos + Vector3.up * openHeight;

        for (int i = 0; i < buttons.Length; i++)
            buttons[i].Init(this, i);
    }

    public override void OnStartClient()
    {
        door.localPosition = isOpen ? openLocalPos : closedLocalPos;
        SetButtonsLit(!isOpen);
    }

    [Server]
    public bool ServerPress()
    {
        if (isOpen) return false;

        isOpen = true;
        closeAt = NetworkTime.time + openDuration;
        return true;
    }

    [ServerCallback]
    void Update()
    {
        if (isOpen)
        {
            if (NetworkTime.time >= closeAt)
                isOpen = false;
        }
        else if (!IsClosed() && IsZoneBlocked())
        {
            isOpen = true;
            closeAt = NetworkTime.time + blockedRetryDelay;
        }
    }

    bool IsClosed() => (door.localPosition - closedLocalPos).sqrMagnitude < 0.0001f;

    bool IsZoneBlocked()
    {
        Transform t = doorZone.transform;
        Vector3 center = t.TransformPoint(doorZone.center);
        Vector3 half = Vector3.Scale(doorZone.size, t.lossyScale) * 0.5f;
        int count = Physics.OverlapBoxNonAlloc(
            center, half, zoneBuffer, t.rotation, playerMask, QueryTriggerInteraction.Ignore);
        return count > 0;
    }

    void LateUpdate()
    {
        Vector3 target = isOpen ? openLocalPos : closedLocalPos;
        float speed = isOpen ? openSpeed : closeSpeed;
        door.localPosition = Vector3.MoveTowards(door.localPosition, target, speed * Time.deltaTime);
    }

    void OnOpenChanged(bool oldValue, bool newValue)
    {
        SetButtonsLit(!newValue);
        // 여기에 문 소리 재생
    }

    void SetButtonsLit(bool lit)
    {
        foreach (GateButton b in buttons)
            if (b) b.SetLit(lit);
    }

    public GateButton GetButton(int index) =>
        index >= 0 && index < buttons.Length ? buttons[index] : null;

    void OnDrawGizmosSelected()
    {
        if (doorZone == null) return;
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.4f);
        Gizmos.matrix = doorZone.transform.localToWorldMatrix;
        Gizmos.DrawCube(doorZone.center, doorZone.size);
    }
}
