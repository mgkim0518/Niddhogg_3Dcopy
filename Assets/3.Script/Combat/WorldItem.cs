using UnityEngine;

public enum ItemKind
{
    Sword,
    GreatSword,
    Shield,
}

// 바닥에 놓인 주울 수 있는 물건에 붙인다: Sword_World, Greatsword_World, Shield_World.
// 콜라이더는 Interactable 레이어 (PlayerInteract의 interactMask에 걸려야 한다).
// 대검과 방패는 SpecialItem도 같이 붙인다 (10초 뒤 원위치 복귀).
public class WorldItem : MonoBehaviour
{
    public ItemKind kind;
}
