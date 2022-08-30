namespace Darkages.Enums;

public enum MonsterEnums
{
    Pure,
    Elemental,
    Physical
}

public enum MonsterType
{
    None,
    Physical,
    Magical,
    GodlyStr,
    GodlyInt,
    GodlyWis,
    GodlyCon,
    GodlyDex,
    Above99P,
    Above99M,
    Forsaken,
    Boss
}

[Flags]
public enum MoodQualifer
{
    Idle = 1,
    Aggressive = 2,
    Unpredicable = 4,
    Neutral = 8,
    VeryAggressive = 16
}
