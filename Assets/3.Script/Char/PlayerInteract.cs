using System;
using UnityEngine;
using Mirror;

[RequireComponent(typeof(Health))]
public class PlayerInteract : NetworkBehaviour
{
    [SerializeField] LayerMask interactMask;
    [SerializeField] float interactRange = 2f;
    [SerializeField] float serverRangeSlack = 1f;
    [SerializeField] float eyeHeight = 1.67f;

    public static event Action<string> OnInteractMessage;

    private PlayerInputManager input;
    private Transform cam;
    private Health health;
    private PlayerInventory inventory;

    void Awake()
    {
        health = GetComponent<Health>();
    }

    public static void ShowMessage(string message)
    {
        OnInteractMessage?.Invoke(message);
    }

    public override void OnStartLocalPlayer()
    {
        cam = Camera.main.transform;
        input = FindFirstObjectByType<PlayerInputManager>();
    }

    void Update()
    {
        if (!isLocalPlayer || input == null)
        {
            return;
        }
        if (health.isDead || PlayerCombat.isInputBlocked) return;
        if (!input.isInteract) return;

        Debug.DrawRay(cam.position, cam.forward * interactRange, Color.cyan, 1f);

        if (!Physics.Raycast(cam.position, cam.forward, out RaycastHit hit,
                interactRange, interactMask, QueryTriggerInteraction.Collide))
            return;

        WorldItem item = hit.collider.GetComponentInParent<WorldItem>();
        if (item != null)
        {
            if (inventory != null) inventory.RequestPickUp(item);
            return;
        }

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
