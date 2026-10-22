using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

public class PlayerAnimator : NetworkBehaviour
{
    [SerializeField] Animator animator;
    [SerializeField] NetworkAnimator netAnimator;

    [SerializeField] float moveDamp = 0.2f;
    [SerializeField] float comboResetTime = 0.4f;
    const int UpperBodyLayer = 1;

    static readonly int MoveXHash = Animator.StringToHash("MoveX");
    static readonly int MoveZHash = Animator.StringToHash("MoveZ");
    static readonly int SpeedHash = Animator.StringToHash("Speed");
    static readonly int GroundedHash = Animator.StringToHash("Grounded");
    static readonly int FreeFallHash = Animator.StringToHash("FreeFall");
    static readonly int JumpHash = Animator.StringToHash("Jump");
    static readonly int MoveAnimSpeedHash = Animator.StringToHash("MoveAnimSpeed");
    static readonly int WeaponHash = Animator.StringToHash("Weapon");
    static readonly int DeadHash = Animator.StringToHash("Dead");
    static readonly int ThrustHash = Animator.StringToHash("Thrust");
    static readonly int SlashHash = Animator.StringToHash("Slash");
    static readonly int ThrowHash = Animator.StringToHash("Throw");
    static readonly int PunchLHash = Animator.StringToHash("Punch_L");
    static readonly int PunchRHash = Animator.StringToHash("Punch_R");
    static readonly int PickWeaponHash = Animator.StringToHash("PickWeapon");
    static readonly int PickShieldHash = Animator.StringToHash("PickShield");

    private Health health;
    private bool nextRight;
    private float lastPunchTime;

    void Awake()
    {
        health = GetComponent<Health>();
        animator = GetComponent<Animator>();
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

    public void SetMove(Vector3 blend, float animSpeed, bool grounded, bool falling)
    {
        animator.SetFloat(MoveXHash, blend.x, moveDamp, Time.deltaTime);
        animator.SetFloat(MoveZHash, blend.z, moveDamp, Time.deltaTime);
        animator.SetFloat(SpeedHash, blend.magnitude, moveDamp, Time.deltaTime);
        animator.SetFloat(MoveAnimSpeedHash, animSpeed, moveDamp, Time.deltaTime);
        animator.SetBool(GroundedHash, grounded);
        animator.SetBool(FreeFallHash, falling);
        if (grounded) animator.SetBool(JumpHash, false);
    }

    public void PlayJump()
    {
        animator.SetBool(JumpHash, true);
    }

    public void SetWeapon(HeldWeapon weapon)
    {
        animator.SetInteger(WeaponHash, (int)weapon);
    }

    public void PullWeapon(HeldWeapon weapon)
    {
        animator.SetInteger(WeaponHash, (int)weapon);
    }

    public void PlayAttack(AttackKind kind, HeldWeapon weapon)
    {
        if (weapon == HeldWeapon.None)
        {
            if (Time.time - lastPunchTime > comboResetTime) nextRight = false;
            netAnimator.SetTrigger(nextRight ? PunchRHash : PunchLHash);
            nextRight = !nextRight;
            lastPunchTime = Time.time;
            return;
        }
        netAnimator.SetTrigger(kind == AttackKind.Thrust ? ThrustHash : SlashHash);
    }

    public void PlayThrow()
    {
        netAnimator.SetTrigger(ThrowHash);
    }

    public void PlayPickup(bool isShield)
    {
        netAnimator.SetTrigger(isShield ? PickShieldHash : PickWeaponHash);
    }

    void SetDead(bool dead)
    {
        animator.SetBool(DeadHash, dead);
        animator.SetLayerWeight(UpperBodyLayer, dead ? 0f : 1f);
    }
}
