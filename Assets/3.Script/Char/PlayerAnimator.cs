using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

public class PlayerAnimator : NetworkBehaviour
{
    [SerializeField] Animator animator;
    [SerializeField] NetworkAnimator netAnimator;

    static readonly int MoveXHash = Animator.StringToHash("MoveX");
    static readonly int MoveZHash = Animator.StringToHash("MoveZ");
    static readonly int SpeedHash = Animator.StringToHash("Speed");
    static readonly int GroundedHash = Animator.StringToHash("Grounded");
    static readonly int FreeFallHash = Animator.StringToHash("FreeFall");
    static readonly int JumpHash = Animator.StringToHash("Jump");
    static readonly int ThrustHash = Animator.StringToHash("Thrust");
    static readonly int SlashHash = Animator.StringToHash("Slash");
    static readonly int DeadHash = Animator.StringToHash("Dead");

    Health health;

    void Awake()
    {
        health = GetComponent<Health>();
        if (animator == null) animator = GetComponent<Animator>();
    }

    public override void OnStartClient()
    {
        health.OnDeadChanged += SetDead;
        SetDead(health.isDead);
    }

    public override void OnStopClient()
    {
        health.OnDeadChanged -= SetDead;
    }

    public void SetMove(Vector3 localVelocity, bool grounded, bool falling)
    {
        animator.SetFloat(MoveXHash, localVelocity.x, 0.1f, Time.deltaTime);
        animator.SetFloat(MoveZHash, localVelocity.z, 0.1f, Time.deltaTime);
        animator.SetFloat(SpeedHash, localVelocity.magnitude, 0.1f, Time.deltaTime);
        animator.SetBool(GroundedHash, grounded);
        animator.SetBool(FreeFallHash, falling);
        if (grounded) animator.SetBool(JumpHash, false);
    }

    public void PlayJump()
    {
        animator.SetBool(JumpHash, true);
    }

    public void PlayAttack(AttackKind kind)
    {
        netAnimator.SetTrigger(kind == AttackKind.Thrust ? ThrustHash : SlashHash);
    }

    void SetDead(bool dead)
    {
        animator.SetBool(DeadHash, dead);
    }
}
