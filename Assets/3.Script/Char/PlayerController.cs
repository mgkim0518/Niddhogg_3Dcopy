using Mirror;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : NetworkBehaviour
{
    [Header("Look")]
    [SerializeField] private Transform cameraPivot;
    [SerializeField] private float sensitivity = 0.6f;
    [SerializeField] private float minPitch = -80f, maxPitch = 80f;

    [Header("Move")]
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private float runSpeed = 7.5f;
    [SerializeField] private float minAnimMul = 0.8f, maxAnimMul = 3f;
    [SerializeField] private float walkClipSpeed = 1.6f, runClipSpeed = 2.4f;

    [Header("Jump")]
    [SerializeField] private float jumpHeight = 1.2f;
    [SerializeField] private float gravity = -20f;

    private PlayerInputManager playerinput;
    private CharacterController controller;
    private PlayerAnimator anim;
    private Health health;

    
    private float pitch = 0f;
    private float verticalVelocity;
    private bool jumpRequested;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        anim = GetComponent<PlayerAnimator>();
        health = GetComponent<Health>();
    }

    public override void OnStartLocalPlayer()
    {
        playerinput = FindAnyObjectByType<PlayerInputManager>();
        playerinput.Target = this;
        Transform cam = Camera.main.transform;
        cam.SetParent(cameraPivot, false);
        cam.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
    }

    public override void OnStopLocalPlayer()
    {
        if (playerinput != null && playerinput.Target == this)
            playerinput.Target = null;
    }

    private void Update()
    {
        if (!isLocalPlayer) return;

        HandleLook();

        if (health.isDead)
        {
            verticalVelocity = 0f;    
            jumpRequested = false;    
            return;
        }

        HandleMove();
    }

    public void HandleLook()
    {
        Vector2 look = playerinput.Look * sensitivity;
        transform.Rotate(0f, look.x, 0f);
        pitch = Mathf.Clamp(pitch - look.y, minPitch, maxPitch);
        cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);

    }

    public void HandleMove()
    {
        Vector2 input = playerinput.Move;
        Vector3 dir = transform.right * input.x + transform.forward * input.y;
        dir = Vector3.ClampMagnitude(dir, 1f);

        bool is_sprint = playerinput.isSprint;
        float speed = is_sprint ? runSpeed : walkSpeed;
        float clipSpeed = is_sprint ? runClipSpeed : walkClipSpeed;

        Vector3 horizontal = dir * speed;

        float vertical = ApplyGravityAndJump();

        controller.Move((horizontal + Vector3.up * vertical) * Time.deltaTime);
        bool grounded = controller.isGrounded;
        anim.SetMove(transform.InverseTransformDirection(horizontal),
                     grounded, !grounded && verticalVelocity < 0f);
    }

    public void Jump()
    {
        jumpRequested = true;
    }

    private float ApplyGravityAndJump()
    {
        if (controller.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f;

        if (jumpRequested)
        {
            if (controller.isGrounded)
            {
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
                anim.PlayJump();
            }
            jumpRequested = false;
        }

        verticalVelocity += gravity * Time.deltaTime;
        return verticalVelocity;
    }
}