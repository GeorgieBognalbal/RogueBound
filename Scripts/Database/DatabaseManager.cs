using Godot;
using Microsoft.Data.Sqlite;
using System;

public partial class DatabaseManager : Node
{
	private SqliteConnection _connection;
	private long _currentRunId = -1;
	private long _lastFinishedRunId = -1;
	public long CurrentSaveId { get; private set; } = -1;

	public override void _Ready() => InitializeDatabase();

	public void InitializeDatabase()
	{
		string path = ProjectSettings.GlobalizePath("user://roguebound.db");
		_connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path }.ToString());
		_connection.Open();
		using (var pragma = new SqliteCommand("PRAGMA foreign_keys = ON;", _connection)) pragma.ExecuteNonQuery();
		CreateTablesAndMigrate();
		GD.Print("SQLite database initialized successfully!");
	}

	private void CreateTablesAndMigrate()
	{
		Execute(@"
		CREATE TABLE IF NOT EXISTS SaveFiles (
			SaveId INTEGER PRIMARY KEY AUTOINCREMENT,
			Name TEXT NOT NULL COLLATE NOCASE UNIQUE,
			CreatedAt TEXT NOT NULL,
			TotalEnemiesKilled INTEGER NOT NULL DEFAULT 0,
			TutorialCompleted INTEGER NOT NULL DEFAULT 0
		);
		CREATE TABLE IF NOT EXISTS Runs (
			RunId INTEGER PRIMARY KEY AUTOINCREMENT,
			DatePlayed TEXT NOT NULL,
			RoomsCleared INTEGER,
			HighestFloor INTEGER,
			TotalEnemiesKilled INTEGER NOT NULL DEFAULT 0,
			Result TEXT NOT NULL DEFAULT 'In Progress',
			IsFinalized INTEGER NOT NULL DEFAULT 0,
			SaveId INTEGER REFERENCES SaveFiles(SaveId) ON DELETE CASCADE
		);
		CREATE TABLE IF NOT EXISTS EnemyKills (
			RunId INTEGER NOT NULL REFERENCES Runs(RunId) ON DELETE CASCADE,
			EnemyType TEXT NOT NULL,
			KillCount INTEGER NOT NULL DEFAULT 0,
			PRIMARY KEY (RunId, EnemyType)
		);");

		if (!HasColumn("Runs", "SaveId"))
			Execute("ALTER TABLE Runs ADD COLUMN SaveId INTEGER REFERENCES SaveFiles(SaveId) ON DELETE CASCADE;");
		MigrateLegacyData();
		if (!HasColumn("SaveFiles", "TutorialCompleted"))
			MigrateTutorialProgress();
		if (!HasColumn("Runs", "RoomsCleared"))
			Execute("ALTER TABLE Runs ADD COLUMN RoomsCleared INTEGER;");
		if (!HasColumn("Runs", "HighestFloor"))
			Execute("ALTER TABLE Runs ADD COLUMN HighestFloor INTEGER;");
		RemoveWaveColumns();
	}

	private void RemoveWaveColumns()
	{
		string sql = "";
		foreach (var (table, column) in new[]
		{
			("SaveFiles", "TotalWavesSurvived"), ("SaveFiles", "HighestWave"),
			("Runs", "WavesSurvived"), ("Runs", "HighestWave")
		})
		{
			if (HasColumn(table, column))
				sql += $"ALTER TABLE {table} DROP COLUMN {column};";
		}
		if (sql.Length == 0)
			return;

		using var transaction = _connection.BeginTransaction();
		using var command = new SqliteCommand(sql, _connection, transaction);
		command.ExecuteNonQuery();
		transaction.Commit();
	}

	private void MigrateTutorialProgress()
	{
		var settings = new ConfigFile();
		bool tutorialDone = settings.Load("user://roguebound_settings.cfg") == Error.Ok
			&& settings.GetValue("tutorial", "tutorial_done", false).AsBool();

		using var transaction = _connection.BeginTransaction();
		using var command = new SqliteCommand(
			"ALTER TABLE SaveFiles ADD COLUMN TutorialCompleted INTEGER NOT NULL DEFAULT 0;", _connection, transaction);
		command.ExecuteNonQuery();
		if (tutorialDone)
		{
			command.CommandText = @"UPDATE SaveFiles SET TutorialCompleted = 1
				WHERE EXISTS (SELECT 1 FROM Runs WHERE Runs.SaveId = SaveFiles.SaveId);";
			command.ExecuteNonQuery();
		}
		transaction.Commit();
	}

	public bool NeedsTutorial(long saveId)
	{
		using var command = new SqliteCommand(
			"SELECT 1 FROM SaveFiles WHERE SaveId = @id AND TutorialCompleted = 0;", _connection);
		command.Parameters.AddWithValue("@id", saveId);
		return command.ExecuteScalar() != null;
	}

	public void CompleteTutorial(long saveId)
	{
		using var command = new SqliteCommand(
			"UPDATE SaveFiles SET TutorialCompleted = 1 WHERE SaveId = @id;", _connection);
		command.Parameters.AddWithValue("@id", saveId);
		command.ExecuteNonQuery();
	}

	private void MigrateLegacyData()
	{
		long unassignedRuns = ScalarLong("SELECT COUNT(*) FROM Runs WHERE SaveId IS NULL;");
		long oldStatsExists = ScalarLong("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='PlayerStatistics';");
		long oldKills = 0;
		if (oldStatsExists > 0)
		{
			using var cmd = new SqliteCommand("SELECT TotalEnemiesKilled FROM PlayerStatistics WHERE Id=1;", _connection);
			using var reader = cmd.ExecuteReader();
			if (reader.Read()) oldKills = reader.GetInt64(0);
		}

		if (unassignedRuns == 0 && oldKills == 0)
		{
			Execute("DROP TABLE IF EXISTS PlayerStatistics;");
			return;
		}
		long saveId = ScalarLong("SELECT SaveId FROM SaveFiles WHERE Name='Legacy Save' COLLATE NOCASE LIMIT 1;");
		if (saveId == 0)
		{
			using var create = new SqliteCommand("INSERT INTO SaveFiles(Name, CreatedAt, TotalEnemiesKilled) VALUES('Legacy Save', @date, @kills); SELECT last_insert_rowid();", _connection);
			create.Parameters.AddWithValue("@date", DateTime.UtcNow.ToString("o"));
			create.Parameters.AddWithValue("@kills", oldKills);
			saveId = Convert.ToInt64(create.ExecuteScalar());
		}
		using var assign = new SqliteCommand("UPDATE Runs SET SaveId=@saveId WHERE SaveId IS NULL;", _connection);
		assign.Parameters.AddWithValue("@saveId", saveId);
		assign.ExecuteNonQuery();
		Execute("DROP TABLE IF EXISTS PlayerStatistics;");
	}

	private bool HasColumn(string table, string column)
	{
		using var cmd = new SqliteCommand($"PRAGMA table_info({table});", _connection);
		using var reader = cmd.ExecuteReader();
		while (reader.Read()) if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase)) return true;
		return false;
	}

	private void Execute(string sql) { using var cmd = new SqliteCommand(sql, _connection); cmd.ExecuteNonQuery(); }
	private long ScalarLong(string sql)
	{
		using var cmd = new SqliteCommand(sql, _connection);
		object value = cmd.ExecuteScalar();
		return value == null || value == DBNull.Value ? 0 : Convert.ToInt64(value);
	}

	public Godot.Collections.Array<Godot.Collections.Dictionary> GetSaveFiles()
	{
		var saves = new Godot.Collections.Array<Godot.Collections.Dictionary>();
		using var cmd = new SqliteCommand(@"SELECT SaveId, Name, CreatedAt, TotalEnemiesKilled,
			(SELECT MAX(HighestFloor) FROM Runs WHERE Runs.SaveId = SaveFiles.SaveId AND IsFinalized = 1)
			FROM SaveFiles ORDER BY SaveId;", _connection);
		using var reader = cmd.ExecuteReader();
		while (reader.Read())
		{
			var item = new Godot.Collections.Dictionary {
				["id"] = reader.GetInt64(0), ["name"] = reader.GetString(1), ["created"] = reader.GetString(2),
				["total_kills"] = reader.GetInt64(3)
			};
			item["highest_floor"] = reader.IsDBNull(4) ? "—" : reader.GetInt32(4).ToString();
			saves.Add(item);
		}
		return saves;
	}

	public long CreateSaveFile(string name)
	{
		name = (name ?? "").Trim();
		if (name.Length == 0 || name.Length > 32) return -1;
		try
		{
			using var cmd = new SqliteCommand("INSERT INTO SaveFiles(Name, CreatedAt) VALUES(@name, @date); SELECT last_insert_rowid();", _connection);
			cmd.Parameters.AddWithValue("@name", name);
			cmd.Parameters.AddWithValue("@date", DateTime.UtcNow.ToString("o"));
			return Convert.ToInt64(cmd.ExecuteScalar());
		}
		catch (SqliteException) { return -1; }
	}

	public void SelectSaveFile(long saveId) => CurrentSaveId = SaveExists(saveId) ? saveId : -1;
	private bool SaveExists(long id)
	{
		using var cmd = new SqliteCommand("SELECT 1 FROM SaveFiles WHERE SaveId=@id;", _connection);
		cmd.Parameters.AddWithValue("@id", id);
		return cmd.ExecuteScalar() != null;
	}

	public bool DeleteSaveFile(long saveId)
	{
		using var tx = _connection.BeginTransaction();
		using (var kills = new SqliteCommand("DELETE FROM EnemyKills WHERE RunId IN (SELECT RunId FROM Runs WHERE SaveId=@id);", _connection, tx))
		{ kills.Parameters.AddWithValue("@id", saveId); kills.ExecuteNonQuery(); }
		using (var runs = new SqliteCommand("DELETE FROM Runs WHERE SaveId=@id;", _connection, tx))
		{ runs.Parameters.AddWithValue("@id", saveId); runs.ExecuteNonQuery(); }
		using (var cmd = new SqliteCommand("DELETE FROM SaveFiles WHERE SaveId=@id;", _connection, tx))
		{ cmd.Parameters.AddWithValue("@id", saveId); if (cmd.ExecuteNonQuery() == 0) { tx.Rollback(); return false; } }
		tx.Commit();
		bool deleted = true;
		if (deleted && CurrentSaveId == saveId) CurrentSaveId = -1;
		if (deleted && _currentRunId != -1 && _currentRunSaveId == saveId) { _currentRunId = -1; _currentRunSaveId = -1; }
		if (deleted && _lastFinishedSaveId == saveId) { _lastFinishedRunId = -1; _lastFinishedSaveId = -1; }
		return deleted;
	}

	public Godot.Collections.Dictionary GetSaveFileSummary(long saveId)
	{
		var data = new Godot.Collections.Dictionary();
		using (var cmd = new SqliteCommand("SELECT Name, CreatedAt, TotalEnemiesKilled FROM SaveFiles WHERE SaveId=@id;", _connection))
		{
			cmd.Parameters.AddWithValue("@id", saveId);
			using var reader = cmd.ExecuteReader();
			if (!reader.Read()) return data;
			data["name"] = reader.GetString(0); data["created"] = reader.GetString(1);
			data["total_kills"] = reader.GetInt64(2);
		}
		using (var cmd = new SqliteCommand(@"SELECT SUM(RoomsCleared), MAX(HighestFloor)
			FROM Runs WHERE SaveId = @id AND IsFinalized = 1;", _connection))
		{
			cmd.Parameters.AddWithValue("@id", saveId);
			using var reader = cmd.ExecuteReader();
			if (reader.Read())
			{
				data["total_rooms"] = reader.IsDBNull(0) ? "—" : reader.GetInt64(0).ToString();
				data["highest_floor"] = reader.IsDBNull(1) ? "—" : reader.GetInt32(1).ToString();
			}
		}
		using (var cmd = new SqliteCommand("SELECT DatePlayed, TotalEnemiesKilled, Result, RoomsCleared FROM Runs WHERE SaveId=@id AND IsFinalized=1 ORDER BY RunId DESC LIMIT 1;", _connection))
		{
			cmd.Parameters.AddWithValue("@id", saveId);
			using var reader = cmd.ExecuteReader();
			if (reader.Read())
			{
				data["last_date"] = reader.GetString(0);
				data["last_kills"] = reader.GetInt64(1); data["last_result"] = reader.GetString(2);
				data["last_rooms"] = reader.IsDBNull(3) ? "—" : reader.GetInt64(3).ToString();
			}
		}
		return data;
	}

	public long StartRun()
	{
		if (_currentRunId != -1 && _currentRunSaveId == CurrentSaveId) return _currentRunId;
		if (CurrentSaveId == -1 || !SaveExists(CurrentSaveId)) { GD.PushWarning("Select a save file before starting a run."); return -1; }
		using var cmd = new SqliteCommand("INSERT INTO Runs(DatePlayed, SaveId) VALUES(@date, @save); SELECT last_insert_rowid();", _connection);
		cmd.Parameters.AddWithValue("@date", DateTime.UtcNow.ToString("o")); cmd.Parameters.AddWithValue("@save", CurrentSaveId);
		_currentRunId = Convert.ToInt64(cmd.ExecuteScalar()); _currentRunSaveId = CurrentSaveId;
		GD.Print($"New run started for save {CurrentSaveId}. Run ID: {_currentRunId}");
		return _currentRunId;
	}

	private long _currentRunSaveId = -1;
	private long _lastFinishedSaveId = -1;

	public int GetCurrentRunKillCount()
	{
		if (_currentRunId == -1) return 0;
		using var cmd = new SqliteCommand("SELECT TotalEnemiesKilled FROM Runs WHERE RunId=@run AND IsFinalized=0;", _connection);
		cmd.Parameters.AddWithValue("@run", _currentRunId); object value = cmd.ExecuteScalar();
		return value == null || value == DBNull.Value ? 0 : Convert.ToInt32(value);
	}

	public void RecordEnemyKill(string enemyType)
	{
		if (_currentRunId == -1 || string.IsNullOrWhiteSpace(enemyType)) return;
		using var tx = _connection.BeginTransaction();
		using (var cmd = new SqliteCommand("INSERT INTO EnemyKills(RunId, EnemyType, KillCount) VALUES(@run,@type,1) ON CONFLICT(RunId,EnemyType) DO UPDATE SET KillCount=KillCount+1;", _connection, tx))
		{ cmd.Parameters.AddWithValue("@run", _currentRunId); cmd.Parameters.AddWithValue("@type", enemyType.Trim()); cmd.ExecuteNonQuery(); }
		using (var cmd = new SqliteCommand("UPDATE Runs SET TotalEnemiesKilled=TotalEnemiesKilled+1 WHERE RunId=@run AND IsFinalized=0;", _connection, tx))
		{ cmd.Parameters.AddWithValue("@run", _currentRunId); cmd.ExecuteNonQuery(); }
		tx.Commit();
	}

	public void UpdateDungeonProgress(int floor, bool roomCleared = false)
	{
		if (_currentRunId == -1)
			return;
		using var command = new SqliteCommand(@"UPDATE Runs
			SET RoomsCleared = COALESCE(RoomsCleared, 0) + @cleared,
				HighestFloor = MAX(COALESCE(HighestFloor, 0), @floor)
			WHERE RunId = @run AND IsFinalized = 0;", _connection);
		command.Parameters.AddWithValue("@cleared", roomCleared ? 1 : 0);
		command.Parameters.AddWithValue("@floor", floor);
		command.Parameters.AddWithValue("@run", _currentRunId);
		command.ExecuteNonQuery();
	}

	public void FinishRun(string result)
	{
		if (_currentRunId == -1) return;
		using var tx = _connection.BeginTransaction();
		using (var cmd = new SqliteCommand("UPDATE Runs SET Result=@result, IsFinalized=1 WHERE RunId=@run AND IsFinalized=0;", _connection, tx))
		{ cmd.Parameters.AddWithValue("@result", result ?? "Finished"); cmd.Parameters.AddWithValue("@run", _currentRunId); if (cmd.ExecuteNonQuery() == 0) { tx.Rollback(); return; } }
		using (var cmd = new SqliteCommand(@"UPDATE SaveFiles SET
			TotalEnemiesKilled=TotalEnemiesKilled+(SELECT TotalEnemiesKilled FROM Runs WHERE RunId=@run)
			WHERE SaveId=@save;", _connection, tx))
		{ cmd.Parameters.AddWithValue("@run", _currentRunId); cmd.Parameters.AddWithValue("@save", _currentRunSaveId); cmd.ExecuteNonQuery(); }
		tx.Commit(); _lastFinishedRunId = _currentRunId; _lastFinishedSaveId = _currentRunSaveId; _currentRunId = -1; _currentRunSaveId = -1;
	}

	public Godot.Collections.Dictionary GetLastRunSummary()
	{
		var data = new Godot.Collections.Dictionary();
		if (_lastFinishedRunId == -1) return data;
		using var cmd = new SqliteCommand("SELECT RoomsCleared,HighestFloor,TotalEnemiesKilled,Result FROM Runs WHERE RunId=@id;", _connection);
		cmd.Parameters.AddWithValue("@id", _lastFinishedRunId); using var reader = cmd.ExecuteReader();
		if (reader.Read())
		{
			data["rooms"] = reader.IsDBNull(0) ? "—" : reader.GetInt32(0).ToString();
			data["highest_floor"] = reader.IsDBNull(1) ? "—" : reader.GetInt32(1).ToString();
			data["total_kills"] = reader.GetInt32(2); data["result"] = reader.GetString(3);
		}
		return data;
	}

	public Godot.Collections.Array<Godot.Collections.Dictionary> GetLastRunEnemyKills()
	{
		var results = new Godot.Collections.Array<Godot.Collections.Dictionary>(); if (_lastFinishedRunId == -1) return results;
		using var cmd = new SqliteCommand("SELECT EnemyType,KillCount FROM EnemyKills WHERE RunId=@id ORDER BY KillCount DESC;", _connection);
		cmd.Parameters.AddWithValue("@id", _lastFinishedRunId); using var reader = cmd.ExecuteReader();
		while (reader.Read()) results.Add(new Godot.Collections.Dictionary { ["enemy_type"] = reader.GetString(0), ["kills"] = reader.GetInt32(1) });
		return results;
	}

	public Godot.Collections.Dictionary GetLifetimeStatistics()
	{
		if (CurrentSaveId != -1) return GetSaveFileSummary(CurrentSaveId);
		return new Godot.Collections.Dictionary();
	}

	public override void _ExitTree() => _connection?.Dispose();

	public void PrintDatabaseContents()
	{
		using var cmd = new SqliteCommand("SELECT s.Name,r.RunId,r.DatePlayed,r.RoomsCleared,r.HighestFloor,r.TotalEnemiesKilled,r.Result FROM Runs r JOIN SaveFiles s ON s.SaveId=r.SaveId ORDER BY r.RunId DESC;", _connection);
		using var reader = cmd.ExecuteReader();
		while (reader.Read()) GD.Print($"{reader.GetString(0)} | Run {reader.GetInt64(1)} | {reader.GetString(2)} | rooms {reader.GetValue(3)} | floor {reader.GetValue(4)} | kills {reader.GetInt32(5)} | {reader.GetString(6)}");
	}

	public void ClearDatabase()
	{
		Execute("DELETE FROM SaveFiles;");
		_currentRunId = _lastFinishedRunId = CurrentSaveId = _currentRunSaveId = _lastFinishedSaveId = -1;
	}
}
