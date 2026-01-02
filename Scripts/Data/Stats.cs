using Godot;
using System;

public class Stats
{
    public int Power { get; set; } = 4; // Default starting power (Reduced from 5)
    public int Control { get; set; } = 10;
    public int Touch { get; set; } = 10;
    public int Consistency { get; set; } = 10;
    public int Focus { get; set; } = 10;
    public int Temper { get; set; } = 10;

    public float Anger { get; set; } = 0.0f; // 0 to 100
    public bool IsRightHanded { get; set; } = true;

    // Hard caps for stats as per calibration (AI/Default Cap: 7, Player Elite: 10)
    public const int STAT_CAP = 10;
}
