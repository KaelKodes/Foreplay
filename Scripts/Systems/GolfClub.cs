using Godot;

// Using ClubType from ClubData.cs

public partial class GolfClub : Resource
{
    [Export] public string ClubName = "New Club";
    [Export] public ClubType Type = ClubType.Iron;
    [Export] public float LoftDegrees = 15.0f;
    [Export] public float PowerMultiplier = 1.0f; // Smash Factor
    [Export] public float HeadSpeedMultiplier = 1.0f; // Clubhead Speed (length)
    [Export] public float SweetSpotSize = 1.0f; // Forgivingness multiplier
    [Export] public float SpinMultiplier = 1.0f;
}
