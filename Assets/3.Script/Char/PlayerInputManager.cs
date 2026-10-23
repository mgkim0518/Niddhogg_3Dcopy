using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputManager : MonoBehaviour
{
    [SerializeField] private PlayerInput input_l;
    [SerializeField] private string gameplayMap = "PlayerControls";

    public PlayerController Target;

    private InputAction lookAction, moveAction, sprintAction;
    private InputAction thrustAction, slashAction, throwAction, blockAction;
    private InputAction interactAction;


    private void Awake()
    {
        lookAction = input_l.actions["Look"];
        moveAction = input_l.actions["Move"];
        sprintAction = input_l.actions["Sprint"];
        interactAction = input_l.actions["Interact"];

        thrustAction = input_l.actions["Thrust"];   
        slashAction = input_l.actions["Slash"];     
        blockAction = input_l.actions["Block"];
        throwAction = input_l.actions["Throw"];
    }

    public Vector2 Look => lookAction.ReadValue<Vector2>();
    public Vector2 Move => moveAction.ReadValue<Vector2>();
    public bool isSprint => sprintAction.IsPressed();

    public void OnJump(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed)
        {
            return;
        }
        if (Target == null)
        {
            return;
        }

        Target.Jump();
    }

    public bool isThrust => thrustAction.WasPressedThisFrame();
    public bool isSlash => slashAction.WasPressedThisFrame();
    public bool isBlocking => blockAction.IsPressed();
    public bool isThrow => throwAction.WasPressedThisFrame();
    public bool isInteract => interactAction.WasPressedThisFrame();

    //테스트용 코드
    public bool isBlockPressed => blockAction.WasPressedThisFrame();

    public void SetGameplayEnabled(bool enabled)
    {
        InputActionMap map = input_l.actions.FindActionMap(gameplayMap, true);
        if (enabled) map.Enable();
        else map.Disable();
    }
}
