using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

public class PlayerCombat : NetworkBehaviour
{
    [Header("판정")]
    [Tooltip("맞을 수 있는 것 + 공격을 막는 것: Player, Core, 벽 레이어")]
    [SerializeField] LayerMask hitMask;
    [Tooltip("베기가 벽 너머로 들어가지 않게 막는 레이어: 벽, 바닥")]
    [SerializeField] LayerMask obstacleMask;
    [SerializeField] float eyeHeight = 1.67f;
    [SerializeField] float thrustRadius = 0.2f;
    [SerializeField] float slashAngle = 100f;
    [Tooltip("(PlayerAttack에서) 휘두르기 시작 후 실제로 맞는 시점까지의 지연. 애니메이션 타격 프레임에 맞춘다")]
    [SerializeField] float hitDelay = 0.15f;

    [Header("사거리")]
    [SerializeField] float fistRange = 1.2f;
    [SerializeField] float swordRange = 2f;
    [SerializeField] float greatSwordRange = 3f;

    [Header("쿨다운(초)")]
    [SerializeField] float fistCooldown = 0.4f;
    [SerializeField] float thrustCooldown = 0.5f;
    [SerializeField] float slashCooldown = 0.7f;

    [Header("디버그")]
    [SerializeField] bool drawDebugRays = true;

    [SyncVar(hook = nameof(OnHeldChanged))] public HeldWeapon held = HeldWeapon.Sword;
    [SyncVar] public bool isHavingShield;
    [SyncVar] public bool isBlocking;

    public static bool isInputBlocked;

    private PlayerInputManager input;
    private Transform cam;
    private Health health;
    private PlayerAnimator anim;
    private double nextAttackTime;                  // 서버 기준 쿨다운
    private float clientNextTime;                   // 클라이언트 쿨다운
    private bool lastSentBlock;
    private readonly Collider[] overlapBuffer = new Collider[16];
    private readonly HashSet<IDamagable> hitThisSwing = new HashSet<IDamagable>();

    void Awake()
    {
        health = GetComponent<Health>();
        anim = GetComponent<PlayerAnimator>();
    }

    public override void OnStartLocalPlayer()
    {
        cam = Camera.main.transform;
        input = FindFirstObjectByType<PlayerInputManager>();
    }

    public override void OnStartServer()
    {
        health.OnDied += OnDiedServer;
    }

    public override void OnStopServer()
    {
        health.OnDied -= OnDiedServer;
        StopAllCoroutines();
    }

    [Server]
    private void OnDiedServer(DamageInfo info)
    {
        isBlocking = false;
        StopAllCoroutines();
    }

    private void Update()
    {
        if (!isLocalPlayer || input == null) return;

        bool isCanAct = !health.isDead && !isInputBlocked;

        if (isCanAct && Time.time >= clientNextTime)
        {
            AttackKind? kind = null;
            if (input.ThrustPressed) kind = AttackKind.Thrust;
            else if (input.SlashPressed) kind = AttackKind.Slash;

            if (kind.HasValue)
            {
                AttackKind k = held == HeldWeapon.None ? AttackKind.Thrust : kind.Value;
                clientNextTime = Time.time + Cooldown(k);
                anim.PlayAttack(k, held);
                CmdAttack(kind.Value, cam.forward);
            }
        }

        bool isCanBlock = isCanAct && isHavingShield && input.BlockHeld;
        if (isCanBlock != lastSentBlock)
        {
            lastSentBlock = isCanBlock;
            CmdSetBlocking(isCanBlock);
        }
    }

    private void OnHeldChanged(HeldWeapon oldV, HeldWeapon newV)
    {
        anim.SetWeapon(newV);
    }

    [Command]
    private void CmdSetBlocking(bool value)
    {
        isBlocking = value && isHavingShield && !health.isDead;
    }

    [Command]
    private void CmdAttack(AttackKind kind, Vector3 dir)
    {
        if (health.isDead || isBlocking)
        {
            return;
        }
        if (NetworkTime.time < nextAttackTime)
        {
            return;
        }
        if (dir.sqrMagnitude < 0.01f)
        {
            return;
        }

        dir.Normalize();

        if (held == HeldWeapon.None)
        {
            kind = AttackKind.Thrust;
        }

        nextAttackTime = NetworkTime.time + Cooldown(kind);

        RpcPlayAttack(kind, held);
        StartCoroutine(HitAfterDelay(kind, dir));
    }
    IEnumerator HitAfterDelay(AttackKind kind, Vector3 dir)   
    {
        if (hitDelay > 0f) yield return new WaitForSeconds(hitDelay);
        if (health.isDead || isBlocking) yield break;

        Vector3 eye = transform.position + Vector3.up * eyeHeight;

        DamageInfo info = new DamageInfo
        {
            type = CurrentDamageType(),
            attackerTeam = health.team,
            hitDirection = dir,
        };

        if (kind == AttackKind.Thrust) Thrust(eye, dir, info);
        else Slash(eye, dir, info);
    }

    [Server]
    private void Thrust(Vector3 eye, Vector3 dir, DamageInfo info)
    {
        RaycastHit[] hits = Physics.SphereCastAll(
            eye, thrustRadius, dir, Range(), hitMask, QueryTriggerInteraction.Ignore);

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.transform.IsChildOf(transform))
            {
                continue;
            }

            DrawDebug(eye, dir * hit.distance, Color.red);
            hit.collider.GetComponentInParent<IDamagable>()?.TakeDamage(info);
            return;
        }

        DrawDebug(eye, dir * Range(), Color.yellow);
    }

    [Server]
    private void Slash(Vector3 eye, Vector3 dir, DamageInfo info)
    {
        hitThisSwing.Clear();

        Vector3 flatDir = Vector3.ProjectOnPlane(dir, Vector3.up);
        if (flatDir.sqrMagnitude < 0.0001f)
        {
            flatDir = transform.forward;
        }
        flatDir.Normalize();

        int count = Physics.OverlapSphereNonAlloc(
            eye, Range(), overlapBuffer, hitMask, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++)
        {
            Collider col = overlapBuffer[i];
            if (col.transform.IsChildOf(transform)) continue;

            IDamagable target = col.GetComponentInParent<IDamagable>();
            if (target == null)
            {
                continue;
            }
            if (hitThisSwing.Contains(target))
            {
                continue;
            }

            Vector3 closest = col.ClosestPoint(eye);
            Vector3 flatTo = Vector3.ProjectOnPlane(closest - eye, Vector3.up);
            if (flatTo.sqrMagnitude > 0.0001f && Vector3.Angle(flatDir, flatTo) > slashAngle * 0.5f)
            {
                continue;
            }

            if (Physics.Linecast(eye, closest, obstacleMask, QueryTriggerInteraction.Ignore))
            {
                continue;   // 벽 너머
            }

            hitThisSwing.Add(target);
            DrawDebug(eye, closest - eye, Color.red);
            target.TakeDamage(info);
        }

        // 베기 범위 양 끝 표시
        DrawDebug(eye, Quaternion.Euler(0, -slashAngle * 0.5f, 0) * flatDir * Range(), Color.yellow);
        DrawDebug(eye, Quaternion.Euler(0, slashAngle * 0.5f, 0) * flatDir * Range(), Color.yellow);
    }


    [ClientRpc]
    void RpcPlayAttack(AttackKind kind, HeldWeapon weapon)
    {
        // 바람 소리 등. 맞았을 때의 소리는 Health의 hp hook에서.
    }

    private void DrawDebug(Vector3 from, Vector3 vec, Color color)
    {
        if (drawDebugRays) Debug.DrawRay(from, vec, color, 1f);
    }

    private float Range() => held switch
    {
        HeldWeapon.Sword => swordRange,
        HeldWeapon.GreatSword => greatSwordRange,
        _ => fistRange,
    };

    private float Cooldown(AttackKind kind)
    {
        if (held == HeldWeapon.None)
        {
            return fistCooldown;
        }
        return kind == AttackKind.Thrust ? thrustCooldown : slashCooldown;
    }

    private DamageType CurrentDamageType() => held switch
    {
        HeldWeapon.Sword => DamageType.Sword,
        HeldWeapon.GreatSword => DamageType.GreatSword,
        _ => DamageType.Fist,
    };
}
