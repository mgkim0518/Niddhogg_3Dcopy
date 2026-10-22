using System;
using UnityEngine;
using Mirror;

// Player 프리팹에 붙인다. 크로스라인으로 버튼을 보고 E를 누르면 서버에 요청한다.
// 씬 오브젝트(Gate)에는 클라이언트 권한이 없으므로 Command는 플레이어 쪽에 둔다.
// 금요일 아이템 줍기도 같은 레이캐스트에 붙이면 된다.
[RequireComponent(typeof(Health))]
public class PlayerInteract : NetworkBehaviour
{
    [SerializeField] LayerMask interactMask;
    [SerializeField] float interactRange = 2f;
    [SerializeField] float serverRangeSlack = 1f;
    [SerializeField] float eyeHeight = 1.67f;

    // 오류창 UI(금요일)가 구독할 이벤트. 로컬 플레이어에서만 발생
    public static event Action<string> OnInteractMessage;

    PlayerInputManager input;
    Transform cam;
    Health health;

    void Awake()
    {
        health = GetComponent<Health>();
    }

    public override void OnStartLocalPlayer()
    {
        cam = Camera.main.transform;
        input = FindFirstObjectByType<PlayerInputManager>();
    }

    void Update()
    {
        if (!isLocalPlayer || input == null) return;
        if (health.isDead || PlayerCombat.isInputBlocked) return;
        if (!input.isInteract) return;

        Debug.DrawRay(cam.position, cam.forward * interactRange, Color.cyan, 1f);

        if (!Physics.Raycast(cam.position, cam.forward, out RaycastHit hit,
                interactRange, interactMask, QueryTriggerInteraction.Collide))
            return;

        GateButton button = hit.collider.GetComponentInParent<GateButton>();
        if (button == null || button.Gate == null) return;   

        if (button.Gate.IsOpen)
        {
            OnInteractMessage?.Invoke("이미 열려 있습니다");
            return;
        }

        CmdPressGateButton(button.Gate, button.Index);
    }

    [Command]
    void CmdPressGateButton(CastleGate gate, int buttonIndex)
    {
        if (gate == null || health.isDead) return;

        GateButton button = gate.GetButton(buttonIndex);
        if (button == null) return;

        Vector3 eye = transform.position + Vector3.up * eyeHeight;
        float maxDist = interactRange + serverRangeSlack;
        if ((button.transform.position - eye).sqrMagnitude > maxDist * maxDist) return;

        if (!gate.ServerPress())
            TargetInteractMessage(connectionToClient, "이미 열려 있습니다");
    }

    [TargetRpc]
    void TargetInteractMessage(NetworkConnectionToClient target, string message)
    {
        OnInteractMessage?.Invoke(message);
    }
}
