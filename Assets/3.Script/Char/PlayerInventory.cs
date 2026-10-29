using System;
using System.Collections;
using UnityEngine;
using Mirror;

[RequireComponent(typeof(PlayerCombat), typeof(Health))]
public class PlayerInventory : NetworkBehaviour
{
    [Header("손에 든 모델 (NetworkIdentity가 없어야 한다)")]
    [SerializeField] GameObject swordModel;        // WeaponSocket/Sword_Held
    [SerializeField] GameObject greatSwordModel;   // WeaponSocket/Greatsword_Held
    [SerializeField] GameObject shieldModel;       // ShieldSocket/Shield

    [Header("손 위치 (던질 때 보이는 출발점)")]
    [SerializeField] Transform weaponSocket;       
    [SerializeField] Transform shieldSocket;       

    [Header("던지기 (휠 클릭)")]
    [SerializeField] float throwSpeed = 18f;
    [Tooltip("크로스라인보다 살짝 위로 던져 포물선을 만든다 (도)")]
    [SerializeField] float throwUpAngle = 4f;
    [Tooltip("던지기 애니메이션에서 손을 놓는 시점까지의 지연")]
    [SerializeField] float releaseDelay = 0.2f;
    [SerializeField] float throwCooldown = 0.6f;
    [SerializeField] float eyeHeight = 1.67f;
    [Tooltip("눈 앞 얼마나 떨어진 곳에서 출발할지 (자기 몸에 걸리지 않게)")]
    [SerializeField] float throwStartForward = 0.6f;

    [Header("규칙")]
    [SerializeField] float dropHeight = 0.02f;
    [SerializeField] float pickupRange = 2f;
    [SerializeField] float serverRangeSlack = 1f;

    PlayerCombat combat;
    Health health;
    PlayerAnimator anim;

    SpecialItem carriedSpecial;

    HeldWeapon shownHeld = (HeldWeapon)(-1);
    bool shownShield;

    PlayerInputManager input;
    Transform cam;
    float clientNextThrow;

    double serverNextThrow;
    bool throwing;

    public const int SocketWeapon = 0;
    public const int SocketShield = 1;
    public Transform GetSocket(int index) => index == SocketShield ? shieldSocket : weaponSocket;

    void Awake()
    {
        combat = GetComponent<PlayerCombat>();
        health = GetComponent<Health>();
        anim = GetComponent<PlayerAnimator>();
    }

    public override void OnStartServer()
    {
        health.OnDied += OnDiedServer;
        health.OnRevived += OnRevivedServer;
    }

    public override void OnStopServer()
    {
        health.OnDied -= OnDiedServer;
        health.OnRevived -= OnRevivedServer;
        if (!NetworkServer.isLoadingScene)
        {
            DropAll(); 
        }
    }

    public override void OnStartLocalPlayer()
    {
        cam = Camera.main.transform;
        input = FindFirstObjectByType<PlayerInputManager>();
    }

    public void RequestPickUp(WorldItem item)
    {
        if (!isLocalPlayer || item == null) return;

        string reason = CheckPickUp(item.kind, combat.held);
        if (reason != null)
        {
            PlayerInteract.ShowMessage(reason);
            return;
        }
        NetworkIdentity id = item.GetComponentInParent<NetworkIdentity>();
        if (id != null) CmdPickUp(id);
    }

    static string CheckPickUp(ItemKind kind, HeldWeapon held)
    {
        if (kind == ItemKind.Sword && held == HeldWeapon.Sword)
        {
            return "이미 검을 들고 있습니다";
        }
        return null;
    }

    [Command]
    void CmdPickUp(NetworkIdentity target)
    {
        if (target == null || health.isDead) return;

        WorldItem item = target.GetComponent<WorldItem>();
        if (item == null) return;

        
        float maxDist = pickupRange + serverRangeSlack + 1f;
        if ((target.transform.position - transform.position).sqrMagnitude > maxDist * maxDist) return;

        if (CheckPickUp(item.kind, combat.held) != null) return;

        bool ok = item.kind switch
        {
            ItemKind.Sword => PickUpSword(target.gameObject),
            ItemKind.GreatSword => PickUpSpecial(target.GetComponent<SpecialItem>(), ItemKind.GreatSword),
            ItemKind.Shield => PickUpSpecial(target.GetComponent<SpecialItem>(), ItemKind.Shield),
            _ => false,
        };

        if (ok) TargetPickedUp(connectionToClient, item.kind == ItemKind.Shield);
    }

    [TargetRpc]
    void TargetPickedUp(NetworkConnectionToClient target, bool isShield)
    {
        if (anim != null) anim.PlayPickup(isShield);
    }

    [Server]
    bool PickUpSword(GameObject swordObj)
    {
        if (WorldSwordManager.Instance == null) return false;

        if (!WorldSwordManager.Instance.TryPickUp(swordObj)) return false; 

        if (combat.held == HeldWeapon.GreatSword) DropSpecial();
        combat.held = HeldWeapon.Sword;
        return true;
    }

    [Server]
    bool PickUpSpecial(SpecialItem special, ItemKind kind)
    {
        if (special == null) return false;
        if (!special.TryPickUp()) return false;  

        if (kind == ItemKind.GreatSword)
        {
            if (combat.held == HeldWeapon.Sword) DropSword();
            if (combat.isHavingShield) DropSpecial();
            combat.held = HeldWeapon.GreatSword;
        }
        else // Shield
        {
            if (combat.held == HeldWeapon.GreatSword)
            {
                DropSpecial();
                combat.held = HeldWeapon.None;   
            }
            combat.isHavingShield = true;
        }

        carriedSpecial = special;
        return true;
    }

    Vector3 DropPos(float sideOffset = 0f) =>
        transform.position + Vector3.up * dropHeight + transform.right * sideOffset;

    [Server]
    void DropSword()
    {
        if (combat.held != HeldWeapon.Sword) return;
        combat.held = HeldWeapon.None;
        if (WorldSwordManager.Instance != null)
            WorldSwordManager.Instance.SpawnSword(DropPos(0.4f), transform.rotation);
    }

    [Server]
    void DropSpecial()
    {
        if (carriedSpecial != null)
            carriedSpecial.Drop(DropPos(-0.4f));
        carriedSpecial = null;

        if (combat.held == HeldWeapon.GreatSword) combat.held = HeldWeapon.None;
        combat.isHavingShield = false;
        combat.isBlocking = false;
    }

    [Server]
    void DropAll()
    {
        DropSword();
        DropSpecial();
    }

    [Server]
    void OnDiedServer(DamageInfo info) => DropAll();

    [Server]
    void OnRevivedServer()
    {
        combat.held = HeldWeapon.Sword;
    }

    enum ThrowChoice { None, Shield, Sword, GreatSword }

    ThrowChoice ChooseThrow()
    {
        if (combat.isHavingShield) return ThrowChoice.Shield;
        if (combat.held == HeldWeapon.Sword) return ThrowChoice.Sword;
        if (combat.held == HeldWeapon.GreatSword) return ThrowChoice.GreatSword;
        return ThrowChoice.None;
    }

    void Update()
    {
        if (!isLocalPlayer || input == null) return;
        if (health.isDead || PlayerCombat.isInputBlocked) return;
        if (!input.isThrow || Time.time < clientNextThrow) return;

        ThrowChoice choice = ChooseThrow();
        if (choice == ThrowChoice.None)
        {
            PlayerInteract.ShowMessage("던질 것이 없습니다");
            return;
        }

        clientNextThrow = Time.time + throwCooldown;
        if (anim != null) anim.PlayThrow();   // NetworkAnimator가 클라이언트 권한이라 로컬에서 보낸다
        CmdThrow(cam.forward);
    }

    [Command]
    void CmdThrow(Vector3 dir)
    {
        if (health.isDead || throwing) return;
        if (NetworkTime.time < serverNextThrow) return;
        if (dir.sqrMagnitude < 0.01f) return;
        if (ChooseThrow() == ThrowChoice.None) return;

        serverNextThrow = NetworkTime.time + throwCooldown;
        StartCoroutine(ThrowAfterDelay(dir.normalized));
    }

    [Server]
    IEnumerator ThrowAfterDelay(Vector3 dir)
    {
        throwing = true;
        if (releaseDelay > 0f) yield return new WaitForSeconds(releaseDelay);
        throwing = false;

        if (health.isDead) yield break;   // 던지기 전에 죽었다 (이미 DropAll로 떨어졌다)

        ThrowChoice choice = ChooseThrow();   // 지연 동안 바뀌었을 수 있어 다시 고른다
        if (choice == ThrowChoice.None) yield break;

        // 판정용 진짜 궤적: 눈 앞에서 크로스라인 방향 (살짝 위로)
        Vector3 eye = transform.position + Vector3.up * eyeHeight;
        Vector3 start = eye + dir * throwStartForward;
        Vector3 right = Vector3.Cross(Vector3.up, dir);
        Vector3 throwDir = right.sqrMagnitude > 0.0001f
            ? Quaternion.AngleAxis(-throwUpAngle, right.normalized) * dir
            : dir;
        Vector3 velocity = throwDir * throwSpeed;

        ThrownItem projectile = null;
        DamageType type;
        int socket;

        switch (choice)
        {
            case ThrowChoice.Shield:
                {
                    SpecialItem shield = carriedSpecial;
                    carriedSpecial = null;
                    combat.isHavingShield = false;
                    combat.isBlocking = false;
                    if (shield == null) yield break;
                    shield.ReleaseForThrow();
                    projectile = shield.GetComponent<ThrownItem>();
                    type = DamageType.ThrownShield;
                    socket = SocketShield;
                    break;
                }
            case ThrowChoice.GreatSword:
                {
                    SpecialItem great = carriedSpecial;
                    carriedSpecial = null;
                    combat.held = HeldWeapon.None;
                    if (great == null) yield break;
                    great.ReleaseForThrow();
                    projectile = great.GetComponent<ThrownItem>();
                    type = DamageType.ThrownSword;
                    socket = SocketWeapon;
                    break;
                }
            default: // Sword
                {
                    combat.held = HeldWeapon.None;
                    if (WorldSwordManager.Instance == null) yield break;
                    GameObject sword = WorldSwordManager.Instance.SpawnSword(start, Quaternion.LookRotation(dir));
                    projectile = sword != null ? sword.GetComponent<ThrownItem>() : null;
                    type = DamageType.ThrownSword;
                    socket = SocketWeapon;
                    break;
                }
        }

        if (projectile == null)
        {
            Debug.LogError("던질 아이템에 ThrownItem이 없습니다.");
            yield break;
        }
        projectile.Launch(start, velocity, transform, type, health.team, socket);
    }

    void LateUpdate()
    {
        bool alive = !health.isDead;
        HeldWeapon held = alive ? combat.held : HeldWeapon.None;
        bool shield = alive && combat.isHavingShield;
        if (held == shownHeld && shield == shownShield) return;

        shownHeld = held;
        shownShield = shield;
        if (swordModel) swordModel.SetActive(held == HeldWeapon.Sword);
        if (greatSwordModel) greatSwordModel.SetActive(held == HeldWeapon.GreatSword);
        if (shieldModel) shieldModel.SetActive(shield);
    }

    //---테스트용 (제출 전에 삭제 필요)----

    [ContextMenu("Test/대검 들기")]
    void TestGiveGreatSword()
    {
        if (!isServer) return;
        combat.held = HeldWeapon.GreatSword;   
    }

    [ContextMenu("Test/칼 들기")]
    void TestGiveSword()
    {
        if (!isServer) return;
        combat.held = HeldWeapon.Sword;
    }
}
