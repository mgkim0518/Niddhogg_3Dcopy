using UnityEngine;

public enum Team 
{ 
    None,
    Red,
    Blue 
}
public enum HeldWeapon 
{
    None,
    Sword,
    GreatSword
}
public enum DamageType 
{ 
    Sword, 
    GreatSword, 
    Fist, 
    ThrownSword, 
    ThrownShield 
}
public enum AttackKind : byte 
{
    Thrust,
    Slash
}
public struct DamageInfo
{
    public DamageType type;
    public Team attackerTeam;
    public Vector3 hitDirection;  
}
public class DamageTable
{
    public const int PlayerMaxHp = 5;
    public const int CoreMaxHp = 100;

    public static int DamageValue(DamageType t) => t switch
    {
        DamageType.Fist or DamageType.ThrownShield => 1,
        _ => PlayerMaxHp,                       
    };

}
