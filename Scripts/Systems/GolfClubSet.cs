using Godot;
using Godot.Collections;

public partial class GolfClubSet : Resource
{
    [Export] public string ClubSetName = "New Set";
    [Export] public Array<GolfClub> Clubs = new Array<GolfClub>();
}
