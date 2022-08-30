namespace Darkages.Enums
{
    [Flags]
    public enum Class
    {
        Peasant = 0,
        Berserker = 1,
        Defender = 2,
        Assassin = 3,
        Cleric = 4,
        Arcanus = 5,
        Monk = 6,
        DualBash = 7,
        DualCast = 8,
        Racial = 9
    }

    [Flags]
    public enum Race
    {
        UnDecided = 0,
        Human = 1,
        HalfElf = 2,
        HighElf = 3,
        DarkElf = 4,
        WoodElf = 5,
        Orc = 6,
        Dwarf = 7,
        Halfling = 8,
        Dragonkin = 9,
        HalfBeast = 10,
        Fish = 11
    }

    public enum ClassStage
    {
        Class = 0,
        Master = 1,
        Dedicated = 2,
        Sub = 3,
        Forsaken = 4
    }

    [Flags]
    public enum RacialAfflictions
    {
        Normal = 1,
        Lycanisim = 1 << 1,
        Vampirisim = 1 << 2,
        Plagued = 1 << 3, // -500 hp -500 mp -5 all stats
        TheShakes = 1 << 4, // -5 Con, -5 Dex, -5% damage
        Stricken = 1 << 5, // -1500 mp, -10 Wis, -10 Regen
        Rabies = 1 << 6, // Death if not cured in an hour
        LockJoint = 1 << 7, // -10% damage
        NumbFall = 1 << 8, // -20 dmg, -20 hit
        Hallowed = Plagued | Stricken | Rabies
    }

    [Flags]
    public enum SubClassDragonkin
    {
        Red = 0,
        Blue = 1,
        Green = 2,
        Black = 3,
        White = 4,
        Brass = 5,
        Bronze = 6,
        Copper = 7,
        Gold = 8,
        Silver = 9
    }

    public static class ClassStrings
    {
        public static string ClassValue(Class c)
        {
            return c switch
            {
                Class.Peasant => "Peasant",
                Class.Berserker => "Berserker",
                Class.Defender => "Defender",
                Class.Assassin => "Assassin",
                Class.Cleric => "Cleric",
                Class.Arcanus => "Arcanus",
                Class.Monk => "Monk",
                Class.DualBash => "DualBash",
                Class.DualCast => "DualCast",
                _ => "Peasant"
            };
        }

        public static string RaceValue(Race r)
        {
            return r switch
            {
                Race.UnDecided => "UnDecided",
                Race.Human => "Human",
                Race.HalfElf => "Half-Elf",
                Race.HighElf => "High Elf",
                Race.DarkElf => "Drow",
                Race.WoodElf => "Wood Elf",
                Race.Orc => "Orc",
                Race.Dwarf => "Dwarf",
                Race.Halfling => "Halfling",
                Race.Dragonkin => "Dragonkin",
                Race.HalfBeast => "Half-Beast",
                Race.Fish => "Fish",
                _ => "UnDecided"
            };
        }

        public static string AfflictionValue(RacialAfflictions a)
        {
            return a switch
            {
                RacialAfflictions.Normal => "Normal",
                RacialAfflictions.Lycanisim => "Lycanisim",
                RacialAfflictions.Vampirisim => "Vampirisim",
                RacialAfflictions.Plagued => "Zombified",
                RacialAfflictions.TheShakes => "Diseased",
                RacialAfflictions.Stricken => "Diseased",
                RacialAfflictions.Rabies => "Diseased",
                RacialAfflictions.LockJoint => "Diseased",
                RacialAfflictions.NumbFall => "Diseased",
                RacialAfflictions.Hallowed => "Hallowed",
                _ => "Normal"
            };
        }

        public static string SubRaceDragonkinValue(SubClassDragonkin s)
        {
            return s switch
            {
                SubClassDragonkin.Red => "Red",
                SubClassDragonkin.Blue => "Blue",
                SubClassDragonkin.Green => "Green",
                SubClassDragonkin.Black => "Black",
                SubClassDragonkin.White => "White",
                SubClassDragonkin.Brass => "Brass",
                SubClassDragonkin.Bronze => "Bronze",
                SubClassDragonkin.Copper => "Copper",
                SubClassDragonkin.Gold => "Gold",
                SubClassDragonkin.Silver => "Silver",
                _ => "Red"
            };
        }
    }
}