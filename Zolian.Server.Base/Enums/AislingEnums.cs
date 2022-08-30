namespace Darkages.Enums;

public enum BodySprite : byte
{
    None = 0,
    Male = 16,
    Female = 32,
    MaleGhost = 48,
    FemaleGhost = 64,
    MaleInvis = 80,
    FemaleInvis = 96,
    MaleJester = 112,
    MaleHead = 128,
    FemaleHead = 144,
    BlankMale = 160,
    BlankFemale = 176
}

public enum SkinColor : byte
{
    Basic = 0x00,
    White = 0x01,
    Cocoa = 0x02,
    Orc = 0x03,
    Yellow = 0x04,
    Tan = 0x05,
    Grey = 0x06,
    LightBlue = 0x07,
    Orange = 0x08,
    Purple = 0x09
}

public enum Gender
{
    Male = 1,
    Female = 2,
    Both = 255
}

public enum RestPosition : byte
{
    Standing = 0x00,
    RestPosition1 = 0x01,
    RestPosition2 = 0x02,
    MaximumChill = 0x03
}

public enum GroupStatus
{
    NotAcceptingRequests = 0,
    AcceptingRequests = 1
}

public enum LegendIcon
{
    Community = 0,
    Warrior = 1,
    Rogue = 2,
    Wizard = 3,
    Priest = 4,
    Monk = 5,
    Heart = 6,
    Victory = 7
}

public enum AislingFlags
{
    Normal = 0,
    Ghost = 1
}

public enum AnimalForm : byte
{
    None = 0,
    Draco = 1,
    Kelberoth = 2,
    WhiteBat = 3,
    Scorpion = 4
}

public enum Mail : byte
{
    None = 0,
    Parcel = 1,
    Letter = 16
}

public enum NameDisplayStyle : byte
{
    GreyHover = 0x00,
    RedAlwaysOn = 0x01,
    GreenHover = 0x02,
    GreyAlwaysOn = 0x03
}

public enum ActivityStatus : byte
{
    Awake = 0,
    DoNotDisturb = 1,
    DayDreaming = 2,
    NeedGroup = 3,
    Grouped = 4,
    LoneHunter = 5,
    GroupHunter = 6,
    NeedHelp = 7
}
