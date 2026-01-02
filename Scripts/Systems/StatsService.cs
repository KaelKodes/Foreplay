using Godot;
using System;

public partial class StatsService : Node
{
    private Stats _playerStats = new Stats();

    public Stats PlayerStats => _playerStats;

    public void LoadStats()
    {
        try
        {
            using (var connection = DatabaseManager.GetConnection())
            {
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText = "SELECT Power, Control, Touch, IsRightHanded, Anger FROM PlayerStats WHERE Id = 1";
                using (var reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        _playerStats.Power = reader.GetInt32(0);
                        _playerStats.Control = reader.GetInt32(1);
                        _playerStats.Touch = reader.GetInt32(2);
                        _playerStats.IsRightHanded = reader.GetInt32(3) == 1;
                        _playerStats.Anger = reader.IsDBNull(4) ? 0 : (float)reader.GetDouble(4);
                    }
                }
            }
        }
        catch (Exception e)
        {
            GD.PrintErr($"StatsService: Failed to load stats: {e.Message}");
        }
    }

    public void SavePlayerProgress()
    {
        try
        {
            using (var connection = DatabaseManager.GetConnection())
            {
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText = "UPDATE PlayerStats SET Anger = @anger WHERE Id = 1";
                command.Parameters.AddWithValue("@anger", _playerStats.Anger);
                command.ExecuteNonQuery();
            }
        }
        catch (Exception e)
        {
            GD.PrintErr($"StatsService: Failed to save progress: {e.Message}");
        }
    }

    public void UpdateAnger(float accuracyError)
    {
        float error = Math.Abs(accuracyError);
        // Tolerance: +/- 5 units from 25
        if (error > 5.0f)
        {
            _playerStats.Anger += 1.0f * (error - 5.0f);
        }
        else
        {
            _playerStats.Anger -= 5.0f;
        }
        _playerStats.Anger = Mathf.Clamp(_playerStats.Anger, 0, 100);
        SavePlayerProgress();
    }
}
