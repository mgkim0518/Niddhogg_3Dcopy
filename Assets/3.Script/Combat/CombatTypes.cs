using UnityEngine;

public enum Team 
{ 
    None,
    Red,
    Blue 
}
public static class TeamColors
{
    public static Color Red = new Color(0.80f, 0.28f, 0.25f);
    public static Color Blue = new Color(0.28f, 0.45f, 0.82f);
    public static readonly Color None = new Color(0.6f, 0.6f, 0.6f);

    public static Color Of(Team t) => t switch
    {
        Team.Red => Red,
        Team.Blue => Blue,
        _ => None,
    };
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
    public const int CoreMaxHp = 10;

    public static int DamageValue(DamageType t) => t switch
    {
        DamageType.Fist or DamageType.ThrownShield => 1,
        _ => PlayerMaxHp,                       
    };

}
