using Godot;
using System;

public enum ClubType
{
    Driver,
    Wood,
    Hybrid,
    Iron,
    Wedge,
    Putter
}

public enum ClubTier
{
    Beginner,
    Intermediate,
    Pro
}

public class ClubData
{
    public int Id { get; set; }
    public string Name { get; set; }
    public ClubType Type { get; set; }
    public ClubTier Tier { get; set; }
    public float Mastery { get; set; } = 0.0f;
    public float Durability { get; set; } = 100.0f;
    public string Condition { get; set; } = "Good";

    // Power and Control multipliers based on Tier
    public float GetPowerMultiplier()
    {
        return Tier switch
        {
            ClubTier.Beginner => 0.8f,
            ClubTier.Intermediate => 1.0f,
            ClubTier.Pro => 1.2f,
            _ => 1.0f
        };
    }

    public float GetForgivenessMultiplier()
    {
        return Tier switch
        {
            ClubTier.Beginner => 1.2f,
            ClubTier.Intermediate => 1.0f,
            ClubTier.Pro => 0.8f,
            _ => 1.0f
        };
    }
}
