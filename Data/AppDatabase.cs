namespace minprofil.Data;

using Microsoft.Data.Sqlite;

// Enkelt datalager mot en SQLite-fil. Skapas och seedas vid uppstart.
public class AppDatabase
{
    private readonly string _connectionString;

    public AppDatabase(IConfiguration configuration)
    {
        var path = configuration.GetValue<string>("DatabasePath") ?? "minprofil.db";
        _connectionString = $"Data Source={path}";
    }

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    public void EnsureCreatedAndSeeded()
    {
        using var connection = Open();

        var create = connection.CreateCommand();
        create.CommandText =
            @"CREATE TABLE IF NOT EXISTS Users (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Username TEXT NOT NULL UNIQUE,
                Password TEXT NOT NULL,
                DisplayName TEXT NOT NULL,
                Presentation TEXT NOT NULL DEFAULT '',
                IsAdmin INTEGER NOT NULL DEFAULT 0
            );";
        create.ExecuteNonQuery();

        var count = connection.CreateCommand();
        count.CommandText = "SELECT COUNT(*) FROM Users";
        var existing = (long)(count.ExecuteScalar() ?? 0L);
        if (existing > 0)
        {
            return;
        }

        var seed = new (string Username, string Password, string Name, string Presentation, bool Admin)[]
        {
            ("anna", "sommar2025", "Anna Bergström", "Hej! Jag pluggar systemutveckling och gillar att bygga webbappar.", false),
            ("erik", "hunter2", "Erik Lund", "Kaffedrickare och kodare. Fråga mig om C#.", false),
            ("sara", "qwerty", "Sara Nyman", "Nyfiken på allt som rör säkerhet.", false),
            ("admin", "S3kr3t-Admin!2026", "Administratör", "Systemkonto. Rör inte.", true),
        };

        foreach (var user in seed)
        {
            var insert = connection.CreateCommand();
            insert.CommandText =
                "INSERT INTO Users (Username, Password, DisplayName, Presentation, IsAdmin) " +
                "VALUES ($u, $p, $n, $pres, $admin)";
            insert.Parameters.AddWithValue("$u", user.Username);
            insert.Parameters.AddWithValue("$p", user.Password);
            insert.Parameters.AddWithValue("$n", user.Name);
            insert.Parameters.AddWithValue("$pres", user.Presentation);
            insert.Parameters.AddWithValue("$admin", user.Admin ? 1 : 0);
            insert.ExecuteNonQuery();
        }
    }

    // Slår upp en användare utifrån inloggningsuppgifterna.
    public User? Login(string username, string password)
    {
        using var connection = Open();
        var command = connection.CreateCommand();
        command.CommandText =
           "SELECT Id, Username, Password, DisplayName, Presentation, IsAdmin " +
           "FROM Users WHERE Username = @username AND Password = @password";
        command.Parameters.AddWithValue("@username", username);
        command.Parameters.AddWithValue("@password", password);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return ReadUser(reader);
    }

    public User? GetById(int id)
    {
        using var connection = Open();
        var command = connection.CreateCommand();
        command.CommandText =
            "SELECT Id, Username, Password, DisplayName, Presentation, IsAdmin " +
            "FROM Users WHERE Id = $id";
        command.Parameters.AddWithValue("$id", id);

        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadUser(reader) : null;
    }

    public void UpdatePresentation(int userId, string presentation)
    {
        using var connection = Open();
        var command = connection.CreateCommand();
        command.CommandText = "UPDATE Users SET Presentation = $pres WHERE Id = $id";
        command.Parameters.AddWithValue("$pres", presentation);
        command.Parameters.AddWithValue("$id", userId);
        command.ExecuteNonQuery();
    }

    public List<User> GetAll()
    {
        using var connection = Open();
        var command = connection.CreateCommand();
        command.CommandText =
            "SELECT Id, Username, Password, DisplayName, Presentation, IsAdmin FROM Users ORDER BY DisplayName";

        var users = new List<User>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            users.Add(ReadUser(reader));
        }

        return users;
    }

    private static User ReadUser(SqliteDataReader reader) => new()
    {
        Id = reader.GetInt32(0),
        Username = reader.GetString(1),
        Password = reader.GetString(2),
        DisplayName = reader.GetString(3),
        Presentation = reader.GetString(4),
        IsAdmin = reader.GetInt32(5) == 1,
    };
}
