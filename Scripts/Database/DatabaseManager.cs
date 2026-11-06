
using Godot;
using Microsoft.Data.Sqlite;
using System;

public partial class DatabaseManager : Node
{
	private SqliteConnection _connection;

	private long _currentRunId = -1;
	private long _lastFinishedRunId = -1;

	public override void _Ready()
	{
		InitializeDatabase();
	}

	public void InitializeDatabase()
	{
		string databasePath =
			ProjectSettings.GlobalizePath("user://roguebound.db");

		var connectionString = new SqliteConnectionStringBuilder
		{
			DataSource = databasePath
		}.ToString();

		_connection = new SqliteConnection(connectionString);
		_connection.Open();

		CreateTables();

		GD.Print("SQLite database initialized successfully!");
	}

	private void CreateTables()
	{
		string sql = @"
        CREATE TABLE IF NOT EXISTS Runs (
            RunId INTEGER PRIMARY KEY AUTOINCREMENT,
            DatePlayed TEXT NOT NULL,
            WavesSurvived INTEGER DEFAULT 0,
            HighestWave INTEGER DEFAULT 0,
            TotalEnemiesKilled INTEGER DEFAULT 0,
            Result TEXT DEFAULT 'In Progress',
            IsFinalized INTEGER DEFAULT 0
        );

        CREATE TABLE IF NOT EXISTS EnemyKills (
            RunId INTEGER NOT NULL,
            EnemyType TEXT NOT NULL,
            KillCount INTEGER DEFAULT 0,
            PRIMARY KEY (RunId, EnemyType),
            FOREIGN KEY (RunId) REFERENCES Runs(RunId)
        );

        CREATE TABLE IF NOT EXISTS PlayerStatistics (
            Id INTEGER PRIMARY KEY CHECK (Id = 1),
            TotalWavesSurvived INTEGER DEFAULT 0,
            TotalEnemiesKilled INTEGER DEFAULT 0,
            HighestWave INTEGER DEFAULT 0
        );

        INSERT OR IGNORE INTO PlayerStatistics (Id)
        VALUES (1);
		";

		using var command = new SqliteCommand(sql, _connection);
		command.ExecuteNonQuery();
	}

	public long StartRun()
	{
		string sql = @"
        INSERT INTO Runs (DatePlayed)
        VALUES (@date);

        SELECT last_insert_rowid();
		";

		using var command = new SqliteCommand(sql, _connection);

		command.Parameters.AddWithValue(
			"@date",
			DateTime.UtcNow.ToString("o")
		);

		_currentRunId = (long)command.ExecuteScalar();

		GD.Print($"New run started. Run ID: {_currentRunId}");

		return _currentRunId;
	}

	public void RecordEnemyKill(string enemyType)
	{
		if (_currentRunId == -1 ||
			string.IsNullOrWhiteSpace(enemyType))
			return;

		using var transaction = _connection.BeginTransaction();

		string killSql = @"
        INSERT INTO EnemyKills (RunId, EnemyType, KillCount)
        VALUES (@runId, @enemyType, 1)
        ON CONFLICT (RunId, EnemyType)
        DO UPDATE SET KillCount = KillCount + 1;
		";

		using (var command = new SqliteCommand(
			killSql, _connection, transaction))
		{
			command.Parameters.AddWithValue("@runId", _currentRunId);
			command.Parameters.AddWithValue("@enemyType", enemyType);
			command.ExecuteNonQuery();
		}

		string updateSql = @"
        UPDATE Runs
        SET TotalEnemiesKilled = TotalEnemiesKilled + 1
        WHERE RunId = @runId;
		";

		using (var command = new SqliteCommand(
			updateSql, _connection, transaction))
		{
			command.Parameters.AddWithValue("@runId", _currentRunId);
			command.ExecuteNonQuery();
		}

		transaction.Commit();
	}

	public void UpdateWaveProgress(int wavesSurvived, int highestWave)
	{
		if (_currentRunId == -1)
			return;

		string sql = @"
        UPDATE Runs
        SET WavesSurvived = @waves,
            HighestWave = @highest
        WHERE RunId = @runId
        AND IsFinalized = 0;
		";

		using var command = new SqliteCommand(sql, _connection);

		command.Parameters.AddWithValue("@waves", wavesSurvived);
		command.Parameters.AddWithValue("@highest", highestWave);
		command.Parameters.AddWithValue("@runId", _currentRunId);

		command.ExecuteNonQuery();
	}

	public void FinishRun(string result)
	{
		if (_currentRunId == -1)
			return;

		using var transaction = _connection.BeginTransaction();

		string runSql = @"
        UPDATE Runs
        SET Result = @result,
            IsFinalized = 1
        WHERE RunId = @runId
        AND IsFinalized = 0;
		";

		using var runCommand = new SqliteCommand(
			runSql, _connection, transaction);

		runCommand.Parameters.AddWithValue("@result", result);
		runCommand.Parameters.AddWithValue("@runId", _currentRunId);

		int rowsUpdated = runCommand.ExecuteNonQuery();

		if (rowsUpdated == 0)
		{
			transaction.Rollback();
			return;
		}

		string statsSql = @"
        UPDATE PlayerStatistics
        SET TotalWavesSurvived = TotalWavesSurvived +
            (SELECT WavesSurvived FROM Runs WHERE RunId = @runId),

            TotalEnemiesKilled = TotalEnemiesKilled +
            (SELECT TotalEnemiesKilled FROM Runs WHERE RunId = @runId),

            HighestWave = MAX(
                HighestWave,
                (SELECT HighestWave FROM Runs WHERE RunId = @runId)
            )
        WHERE Id = 1;
		";

		using var statsCommand = new SqliteCommand(
			statsSql, _connection, transaction);

		statsCommand.Parameters.AddWithValue("@runId", _currentRunId);
		statsCommand.ExecuteNonQuery();

		transaction.Commit();

		_lastFinishedRunId = _currentRunId;
		_currentRunId = -1;

		GD.Print("Run saved successfully!");
	}

	// Retrieves the last completed run.
	public Godot.Collections.Dictionary GetLastRunSummary()
	{
		var data = new Godot.Collections.Dictionary();

		if (_lastFinishedRunId == -1)
			return data;

		string sql = @"
        SELECT WavesSurvived, HighestWave,
               TotalEnemiesKilled, Result
        FROM Runs
        WHERE RunId = @runId;
		";

		using var command = new SqliteCommand(sql, _connection);
		command.Parameters.AddWithValue("@runId", _lastFinishedRunId);

		using var reader = command.ExecuteReader();

		if (reader.Read())
		{
			data["waves"] = reader.GetInt32(0);
			data["highest_wave"] = reader.GetInt32(1);
			data["total_kills"] = reader.GetInt32(2);
			data["result"] = reader.GetString(3);
		}

		return data;
	}

	// Retrieves enemy kills grouped by type for the last run.
	public Godot.Collections.Array<Godot.Collections.Dictionary>
		GetLastRunEnemyKills()
	{
		var results =
			new Godot.Collections.Array<Godot.Collections.Dictionary>();

		if (_lastFinishedRunId == -1)
			return results;

		string sql = @"
        SELECT EnemyType, KillCount
        FROM EnemyKills
        WHERE RunId = @runId
        ORDER BY KillCount DESC;
		";

		using var command = new SqliteCommand(sql, _connection);
		command.Parameters.AddWithValue("@runId", _lastFinishedRunId);

		using var reader = command.ExecuteReader();

		while (reader.Read())
		{
			var item = new Godot.Collections.Dictionary();

			item["enemy_type"] = reader.GetString(0);
			item["kills"] = reader.GetInt32(1);

			results.Add(item);
		}

		return results;
	}

	// Retrieves lifetime statistics.
	public Godot.Collections.Dictionary GetLifetimeStatistics()
	{
		var data = new Godot.Collections.Dictionary();

		string sql = @"
        SELECT TotalWavesSurvived,
               TotalEnemiesKilled,
               HighestWave
        FROM PlayerStatistics
        WHERE Id = 1;
		";

		using var command = new SqliteCommand(sql, _connection);
		using var reader = command.ExecuteReader();

		if (reader.Read())
		{
			data["total_waves"] = reader.GetInt32(0);
			data["total_kills"] = reader.GetInt32(1);
			data["highest_wave"] = reader.GetInt32(2);
		}

		return data;
	}

	public override void _ExitTree()
	{
		_connection?.Dispose();
	}





	// FOR TESTING / DEBUGGING HEREEEEE!!!	

	public void PrintDatabaseContents()
	{
		GD.Print("\n========== ROGUEBOUND DATABASE ==========");

		// 1. Display all runs.
		GD.Print("\n--- RUN HISTORY ---");

		string runsSql = @"
		SELECT RunId, DatePlayed, WavesSurvived,
			HighestWave, TotalEnemiesKilled, Result
		FROM Runs
		ORDER BY RunId DESC;
		";

		using (var command = new SqliteCommand(runsSql, _connection))
		using (var reader = command.ExecuteReader())
		{
			while (reader.Read())
			{
				GD.Print(
					$"Run ID: {reader.GetInt64(0)} | " +
					$"Date: {reader.GetString(1)} | " +
					$"Waves: {reader.GetInt32(2)} | " +
					$"Highest Wave: {reader.GetInt32(3)} | " +
					$"Kills: {reader.GetInt32(4)} | " +
					$"Result: {reader.GetString(5)}"
				);
			}
		}

		// 2. Display enemy kills for every run.
		GD.Print("\n--- ENEMY KILL BREAKDOWN ---");

		string killsSql = @"
		SELECT RunId, EnemyType, KillCount
		FROM EnemyKills
		ORDER BY RunId DESC, EnemyType;
		";

		using (var command = new SqliteCommand(killsSql, _connection))
		using (var reader = command.ExecuteReader())
		{
			while (reader.Read())
			{
				GD.Print(
					$"Run {reader.GetInt64(0)} | " +
					$"{reader.GetString(1)}: " +
					$"{reader.GetInt32(2)} kills"
				);
			}
		}

		// 3. Display lifetime statistics.
		GD.Print("\n--- LIFETIME STATISTICS ---");

		var stats = GetLifetimeStatistics();

		GD.Print($"Total Waves Survived: {stats["total_waves"]}");
		GD.Print($"Total Enemies Killed: {stats["total_kills"]}");
		GD.Print($"Highest Wave: {stats["highest_wave"]}");

		GD.Print("\n=========================================\n");
	}

	public void ClearDatabase()
	{
		string sql = @"
			DELETE FROM EnemyKills;
			DELETE FROM Runs;

			UPDATE sqlite_sequence
			SET seq = 0
			WHERE name = 'Runs';

			UPDATE PlayerStatistics
			SET TotalWavesSurvived = 0,
				TotalEnemiesKilled = 0,
				HighestWave = 0
			WHERE Id = 1;
		";

		using var command = new SqliteCommand(sql, _connection);
		command.ExecuteNonQuery();

		_currentRunId = -1;
		_lastFinishedRunId = -1;

		GD.Print("RogueBound database cleared successfully!");
	}
}
