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
        DropAll();   // 접속이 끊겨도 특수 아이템이 사라지지 않게
    }

    // ---------- 클라이언트 → 서버 ----------

    // PlayerInteract가 로컬 플레이어에서 부른다
    public void RequestPickUp(WorldItem item)
    {
        if (!isLocalPlayer || item == null) return;

        string reason = CheckPickUp(item.kind, combat.held, combat.isHavingShield);
        if (reason != null)
        {
            PlayerInteract.ShowMessage(reason);
            return;
        }
        NetworkIdentity id = item.GetComponentInParent<NetworkIdentity>();
        if (id != null) CmdPickUp(id);
    }

    // 주울 수 없는 경우만 이유를 돌려준다. 교체(떨어뜨리고 줍기)는 허용
    static string CheckPickUp(ItemKind kind, HeldWeapon held, bool hasShield)
    {
        switch (kind)
        {
            case ItemKind.Sword:
                if (held == HeldWeapon.Sword) return "이미 검을 들고 있습니다";
                break;
            case ItemKind.GreatSword:
                if (held == HeldWeapon.GreatSword) return "이미 대검을 들고 있습니다";
                break;
            case ItemKind.Shield:
                if (hasShield) return "이미 방패를 들고 있습니다";
                break;
        }
        return null;
    }

    [Command]
    void CmdPickUp(NetworkIdentity target)
    {
        if (target == null || health.isDead) return;

        WorldItem item = target.GetComponent<WorldItem>();
        if (item == null) return;

        // 클라이언트를 믿지 않고 서버에서 거리를 다시 잰다
        float maxDist = pickupRange + serverRangeSlack + 1f;   // 발 위치 기준이라 여유를 더 준다
        if ((target.transform.position - transform.position).sqrMagnitude > maxDist * maxDist) return;

        if (CheckPickUp(item.kind, combat.held, combat.isHavingShield) != null) return;

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
        // NetworkAnimator가 클라이언트 권한이라 트리거는 로컬 플레이어가 보낸다
        if (anim != null) anim.PlayPickup(isShield);
    }

    // ---------- 서버: 줍기 ----------

    [Server]
    bool PickUpSword(GameObject swordObj)
    {
        if (WorldSwordManager.Instance == null) return false;

        if (!WorldSwordManager.Instance.TryPickUp(swordObj)) return false;   // 그 사이 누가 먼저 주웠다

        // 대검을 들고 있었다면 내려놓는다 (검과 대검은 같이 못 든다). 방패는 그대로
        if (combat.held == HeldWeapon.GreatSword) DropSpecial();
        combat.held = HeldWeapon.Sword;
        return true;
    }

    [Server]
    bool PickUpSpecial(SpecialItem special, ItemKind kind)
    {
        if (special == null) return false;
        if (!special.TryPickUp()) return false;   // 이미 누가 들고 있다

        if (kind == ItemKind.GreatSword)
        {
            // 양손 무기: 검과 방패를 모두 내려놓는다
            if (combat.held == HeldWeapon.Sword) DropSword();
            if (combat.isHavingShield) DropSpecial();
            combat.held = HeldWeapon.GreatSword;
        }
        else // Shield
        {
            // 대검과는 같이 못 든다. 검과는 같이 든다
            if (combat.held == HeldWeapon.GreatSword)
            {
                DropSpecial();
                combat.held = HeldWeapon.None;   // 맨손 + 방패
            }
            combat.isHavingShield = true;
        }

        carriedSpecial = special;
        return true;
    }

    // ---------- 서버: 떨어뜨리기 ----------

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

    // 들고 있는 대검이나 방패를 내려놓는다 (10초 뒤 SpecialItem이 원위치로 돌려보낸다)
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

    // ---------- 모든 클라이언트: 손에 든 모델 ----------

    void LateUpdate()
    {
        // SyncVar 값을 보고 바뀌었을 때만 모델을 갱신한다 (늦게 접속해도 맞춰진다)
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
}
