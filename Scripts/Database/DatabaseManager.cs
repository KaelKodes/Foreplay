using Godot;
using System;
using Microsoft.Data.Sqlite;
using System.IO;

public partial class DatabaseManager : Node
{
    private static string _dbPath;

    public override void _Ready()
    {
        _dbPath = Path.Combine(ProjectSettings.GlobalizePath("user://"), "golf_rpg.db");
        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        using (var connection = new SqliteConnection($"Data Source={_dbPath}"))
        {
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText =
            @"
                CREATE TABLE IF NOT EXISTS PlayerStats (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Power INTEGER DEFAULT 4,
                    Control INTEGER DEFAULT 10,
                    Touch INTEGER DEFAULT 10,
                    Consistency INTEGER DEFAULT 10,
                    Focus INTEGER DEFAULT 10,
                    Temper INTEGER DEFAULT 10,
                    IsRightHanded INTEGER DEFAULT 1,
                    Anger REAL DEFAULT 0.0
                );

                CREATE TABLE IF NOT EXISTS PlayerSkills (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Driving REAL DEFAULT 0.0,
                    Approach REAL DEFAULT 0.0,
                    Putting REAL DEFAULT 0.0,
                    Chipping REAL DEFAULT 0.0,
                    Pitching REAL DEFAULT 0.0,
                    Lobbing REAL DEFAULT 0.0,
                    Accuracy REAL DEFAULT 0.0,
                    SwingForgiveness REAL DEFAULT 0.0,
                    AngerControl REAL DEFAULT 0.0
                );

                CREATE TABLE IF NOT EXISTS Clubs (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT,
                    Type TEXT,
                    Tier TEXT,
                    Mastery REAL DEFAULT 0.0,
                    Durability REAL DEFAULT 100.0,
                    Condition TEXT DEFAULT 'Good'
                );

                -- Initialize default records if empty
                INSERT INTO PlayerStats (Id) SELECT 1 WHERE NOT EXISTS (SELECT 1 FROM PlayerStats WHERE Id = 1);
                INSERT INTO PlayerSkills (Id) SELECT 1 WHERE NOT EXISTS (SELECT 1 FROM PlayerSkills WHERE Id = 1);
            ";
            command.ExecuteNonQuery();

            // Migration check for IsRightHanded if table already existed
            try
            {
                command.CommandText = "ALTER TABLE PlayerStats ADD COLUMN IsRightHanded INTEGER DEFAULT 1";
                command.ExecuteNonQuery();
            }
            catch (Exception) { /* Column already exists */ }

            try
            {
                command.CommandText = "ALTER TABLE PlayerStats ADD COLUMN Anger REAL DEFAULT 0.0";
                command.ExecuteNonQuery();
            }
            catch (Exception) { /* Column already exists */ }

            // Reset Power to new calibrated baseline (5)
            try
            {
                command.CommandText = "UPDATE PlayerStats SET Power = 4 WHERE Id = 1";
                command.ExecuteNonQuery();
            }
            catch (Exception) { }
        }
        GD.Print($"Database initialized at: {_dbPath}");
    }

    public static SqliteConnection GetConnection()
    {
        return new SqliteConnection($"Data Source={_dbPath}");
    }
}
