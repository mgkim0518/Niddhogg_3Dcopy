using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputManager : MonoBehaviour
{
    [SerializeField] private PlayerInput input_l;
    [SerializeField] private string gameplayMap = "Player";

    public PlayerController Target;

    private InputAction lookAction, moveAction, sprintAction;
    private InputAction thrustAction, slashAction, blockAction;


    private void Awake()
    {
        lookAction = input_l.actions["Look"];
        moveAction = input_l.actions["Move"];
        sprintAction = input_l.actions["Sprint"];

        thrustAction = input_l.actions["Thrust"];   
        slashAction = input_l.actions["Slash"];     
        blockAction = input_l.actions["Block"];
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

    public bool ThrustPressed => thrustAction.WasPressedThisFrame();
    public bool SlashPressed => slashAction.WasPressedThisFrame();
    public bool BlockHeld => blockAction.IsPressed();

    public void SetGameplayEnabled(bool enabled)
    {
        InputActionMap map = input_l.actions.FindActionMap(gameplayMap, true);
        if (enabled) map.Enable();
        else map.Disable();
    }
}
