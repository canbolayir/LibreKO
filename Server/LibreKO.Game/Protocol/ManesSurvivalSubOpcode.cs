namespace LibreKO.Game.Protocol;

public enum ManesSurvivalSubOpcode : byte
{
    SignUp = 1,
    State = 2,
    Notice = 3,
    Progress = 4,
    Bag = 5,
    Pick = 6,
}

public enum ManesSignUpKind : byte
{
    Open = 1,
    Entry = 2,
}

public enum ManesSignUpAction : ushort
{
    Apply = 1,
    Cancel = 2,
}

public enum ManesSignUpResult : short
{
    SameAddress = -4,
    AlreadyJoined = -3,
    Full = -2,
    LevelTooLow = -1,
    Applied = 1,
    Cancelled = 2,
}

public enum ManesStateKind : byte
{
    Start = 1,
    Boss = 2,
    Rank = 3,
    End = 4,
}

public enum ManesNoticeKind : byte
{
    Markers = 1,
    LastBoss = 2,
    Kill = 3,
    Reward = 4,
    Invincible = 5,
}

public enum ManesProgressKind : byte
{
    Exp = 1,
    Level = 2,
    Score = 3,
}

public enum ManesBagKind : byte
{
    Loot = 1,
    Put = 2,
    Discard = 3,
}

public enum ManesPickKind : byte
{
    Offer = 1,
    Choice = 2,
    Passive = 3,
    BonusOffer = 4,
    BonusChoice = 5,
}

public enum ManesPickAction : byte
{
    Skill = 1,
    Potion = 2,
    Skip = 3,
}
