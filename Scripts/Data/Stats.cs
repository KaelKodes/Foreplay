using Godot;
using System;

public class Stats
{
    public int Power { get; set; } = 5;
    public int Control { get; set; } = 10;
    public int Touch { get; set; } = 10;
    public int Consistency { get; set; } = 10;
    public int Focus { get; set; } = 10;
    public int Temper { get; set; } = 10;

    public float Anger { get; set; } = 0.0f; // 0 to 100
    public bool IsRightHanded { get; set; } = true;

    // Hard caps for stats as per calibration (Amateur 5 -> Elite 8)
    public const int STAT_CAP = 10;
}
