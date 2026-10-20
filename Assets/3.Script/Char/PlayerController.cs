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
    [SerializeField] private float walkSpeed = 4f;
    [SerializeField] private float runSpeed = 7f;

    [Header("Jump")]
    [SerializeField] private float jumpHeight = 1.2f;
    [SerializeField] private float gravity = -20f;

    private PlayerInputManager playerinput;
    private CharacterController controller;
    private Animator animator;
    private Health health;

    private float pitch = 0f;
    private float verticalVelocity;
    private bool jumpRequested;

    private static readonly int MoveXHash = Animator.StringToHash("MoveX");
    private static readonly int MoveZHash = Animator.StringToHash("MoveZ");
    private static readonly int GroundedHash = Animator.StringToHash("Grounded");
    private static readonly int JumpHash = Animator.StringToHash("Jump");
    private static readonly int FreeFallHash = Animator.StringToHash("FreeFall");
    private static readonly int SpeedHash = Animator.StringToHash("Speed");

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();
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
            verticalVelocity = 0f;    // 공중에서 죽었을 때 낙하 속도가 부활 후까지 남지 않게
            jumpRequested = false;    // 죽은 동안 누른 점프가 부활하자마자 나가지 않게
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

        float speed = walkSpeed;

        if (playerinput.isSprint)
        {
            speed = runSpeed;
        }

        Vector3 horizontal = dir * speed;

        float vertical = ApplyGravityAndJump();

        Vector3 velocity = horizontal + Vector3.up * vertical;
        controller.Move(velocity * Time.deltaTime);
        UpdateAnimator(horizontal);
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
                animator.SetBool(JumpHash, true);
            }
            jumpRequested = false;
        }

        verticalVelocity += gravity * Time.deltaTime;
        return verticalVelocity;
    }
    private void UpdateAnimator(Vector3 horizontal)
    {
        Vector3 local = transform.InverseTransformDirection(horizontal);
        animator.SetFloat(MoveXHash, local.x, 0.1f, Time.deltaTime);
        animator.SetFloat(MoveZHash, local.z, 0.1f, Time.deltaTime);

        bool grounded = controller.isGrounded;
        animator.SetBool(GroundedHash, grounded);
        animator.SetBool(FreeFallHash, !grounded && verticalVelocity < 0f);

        if (grounded) animator.SetBool(JumpHash, false);

        animator.SetFloat(SpeedHash, horizontal.magnitude, 0.1f, Time.deltaTime);
    }
}