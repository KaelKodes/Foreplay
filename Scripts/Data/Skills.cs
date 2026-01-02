using Godot;
using System;

public class Skills
{
    public float Driving { get; set; } = 0.0f;
    public float Approach { get; set; } = 0.0f;
    public float Putting { get; set; } = 0.0f;
    public float Chipping { get; set; } = 0.0f;
    public float Pitching { get; set; } = 0.0f;
    public float Lobbing { get; set; } = 0.0f;
    public float Accuracy { get; set; } = 0.0f;
    public float SwingForgiveness { get; set; } = 0.0f;
    public float AngerControl { get; set; } = 0.0f;

    public const float SKILL_CAP = 100.0f;
}
