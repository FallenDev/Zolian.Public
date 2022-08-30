namespace Darkages.Enums;

public static class SpriteMaker
{
    public static string BodySpriteValue(BodySprite e)
    {
        return e switch
        {
            BodySprite.None => "None",
            BodySprite.Male => "Male",
            BodySprite.Female => "Female",
            BodySprite.MaleGhost => "MaleGhost",
            BodySprite.FemaleGhost => "FemaleGhost",
            BodySprite.MaleInvis => "MaleInvis",
            BodySprite.FemaleInvis => "FemaleInvis",
            BodySprite.MaleJester => "MaleJester",
            BodySprite.MaleHead => "MaleHead",
            BodySprite.FemaleHead => "FemaleHead",
            BodySprite.BlankMale => "BlankMale",
            BodySprite.BlankFemale => "BlankFemale",
            _ => "None"
        };
    }

    public static string RestingValue(RestPosition e)
    {
        return e switch
        {
            RestPosition.Standing => "Standing",
            RestPosition.RestPosition1 => "RestPosition1",
            RestPosition.RestPosition2 => "RestPosition2",
            RestPosition.MaximumChill => "MaximumChill",
            _ => "Standing"
        };
    }
}

public static class PlayerActivity
{
    public static string ActivityValue(ActivityStatus e)
    {
        return e switch
        {
            ActivityStatus.Awake => "Awake",
            ActivityStatus.DoNotDisturb => "DoNotDisturb",
            ActivityStatus.DayDreaming => "DayDreaming",
            ActivityStatus.NeedGroup => "NeedGroup",
            ActivityStatus.Grouped => "Grouped",
            ActivityStatus.LoneHunter => "LoneHunter",
            ActivityStatus.GroupHunter => "GroupHunter",
            ActivityStatus.NeedHelp => "NeedHelp",
            _ => "Awake"
        };
    }
}

public static class AislingFlagStrings
{
    public static string AislingFlag(AislingFlags e)
    {
        return e switch
        {
            AislingFlags.Normal => "Normal",
            AislingFlags.Ghost => "Ghost",
            _ => "Normal"
        };
    }
}

public static class GroupStatusString
{
    public static string GroupStrings(GroupStatus e)
    {
        return e switch
        {
            GroupStatus.NotAcceptingRequests => "NotAcceptingRequests",
            GroupStatus.AcceptingRequests => "AcceptingRequests",
            _ => "NotAcceptingRequests"
        };
    }
}

public static class AnimalFormStrings
{
    public static string AnimalValue(AnimalForm e)
    {
        return e switch
        {
            AnimalForm.None => "None",
            AnimalForm.Draco => "Draco",
            AnimalForm.Kelberoth => "Kelberoth",
            AnimalForm.WhiteBat => "WhiteBat",
            AnimalForm.Scorpion => "Scorpion",
            _ => "None"
        };
    }
}
