using System;
using UnityEngine;
using Mirror;

[RequireComponent(typeof(PlayerCombat), typeof(Health))]
public class PlayerInventory : NetworkBehaviour
{
    [Header("손에 든 모델 (NetworkIdentity가 없어야 한다)")]
    [SerializeField] GameObject swordModel;        // WeaponSocket/Sword_Held
    [SerializeField] GameObject greatSwordModel;   // WeaponSocket/Greatsword_Held
    [SerializeField] GameObject shieldModel;       // ShieldSocket/Shield

    [Header("규칙")]
    [Tooltip("떨어뜨릴 때 플레이어 발밑에서 얼마나 띄울지")]
    [SerializeField] float dropHeight = 0.3f;
    [SerializeField] float pickupRange = 2f;
    [SerializeField] float serverRangeSlack = 1f;

    PlayerCombat combat;
    Health health;
    PlayerAnimator anim;

    SpecialItem carriedSpecial;

    HeldWeapon shownHeld = (HeldWeapon)(-1);
    bool shownShield;

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
        DropAll(); 
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
