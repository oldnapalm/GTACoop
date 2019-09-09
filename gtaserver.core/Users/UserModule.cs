using System;
using System.Collections.Generic;
using System.Text;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using GTAServer.Console;
using GTAServer.Console.Modules;
using GTAServer.PluginAPI.Events;
using GTAServer.ProtocolMessages;
using Microsoft.Extensions.Logging;

namespace GTAServer.Users
{
    class UserModule : IModule
    {
        private ILogger _logger;
        private SQLiteConnection _connection;

        public static readonly List<User> Users = new List<User>();

        public void OnEnable(ConsoleInstance instance)
        {
            _logger = Util.LoggerFactory.CreateLogger<UserModule>();
            _logger.LogInformation("Loading user storage");

            var filename = System.AppContext.BaseDirectory + Path.DirectorySeparatorChar + "users.db";
            var create = false;

            if (!File.Exists(filename))
            {
                SQLiteConnection.CreateFile(filename);
                create = true;
            }

            var connectionString = new SQLiteConnectionStringBuilder()
            {
                DataSource = filename,
                Version = 3
            };

            _connection = new SQLiteConnection(connectionString.ToString());
            _connection.Open();

            if (create)
            {
                new SQLiteCommand(@"
                    CREATE TABLE `users` (
	                    `Id`	    INTEGER PRIMARY KEY AUTOINCREMENT,
	                    `Username`  TEXT,
	                    `Password`  TEXT,
	                    `Group` 	TEXT
                    );"
                , _connection).ExecuteNonQuery();
            }

            ConnectionEvents.OnJoin.Add(OnJoin);
            ConnectionEvents.OnDisconnect.Add(OnLeave);

            ServerManager.GameServer.RegisterCommand("register", OnRegister);
            ServerManager.GameServer.RegisterCommand("login", OnLogin);
        }

        /// <summary>
        /// Creates a new user in the database and stores the created user in memory
        /// </summary>
        /// <param name="username">The choosen username</param>
        /// <param name="password">The choosen password</param>
        public void CreateUser(string username, string password)
        {
            var hash = BCrypt.Net.BCrypt.HashPassword(password);
            password = null;

            var query = new SQLiteCommand(
                "INSERT INTO `users` (`Username`, `Password`, `Group`) VALUES (@username, @password, 'user'); SELECT last_insert_rowid();", _connection);
            query.Parameters.AddWithValue("@username", username);
            query.Parameters.AddWithValue("@password", hash);

            var id = (long) query.ExecuteScalar();
            Users.Add(new User()
            {
                Id = id,
                Username = username,
                Password = hash,
                Group = "user"
            });
        }

        /// <summary>
        /// Links the client to associated user
        /// </summary>
        /// <param name="client"></param>
        public void Login(Client client)
        {
            var user = Users.Single(x => x.Username == client.DisplayName);
            user.Client = client;

            client.SendMessage("You have been logged in");
        }

        private void OnJoin(Client client)
        {
            var query = new SQLiteCommand("SELECT * FROM `users` WHERE `username` = @username;", _connection);
            query.Parameters.AddWithValue("@username", client.DisplayName);

            var reader = query.ExecuteReader();
            if (reader.Read())
            {
                var user = new User()
                {
                    Id = (long)reader["Id"],
                    Username = (string)reader["Username"],
                    Password = (string)reader["Password"],
                    Group = (string)reader["Group"]
                };
                Users.Add(user);

                client.SendMessage("Use /login (password) to login to your account");
            }
            else
            {
                client.SendMessage("You can register an account using /register (password)");
            }

            reader.Close();
        }

        public void OnLeave(Client client)
        {
            if (Users.Any(x => x.Username == client.DisplayName))
            {
                var id = Users.RemoveAll(x => x.Username == client.DisplayName);

                _logger.LogDebug($"{client.DisplayName} left, removing from memory ({id})");
            }
        }

        private void OnRegister(Client client, ChatData chatData)
        {
            if (Users.Any(user => user.Username == client.DisplayName))
            {
                client.SendMessage("Can't register an account on this username");
                return;
            }

            var args = chatData.Message.Split().ToList();
            args.RemoveAt(0);

            if (args.Count == 0)
            {
                client.SendMessage("Usage /register (password)");
                return;
            }

            var password = string.Join(" ", args);

            // bcrypt limits
            if (password.Length > 50)
            {
                client.SendMessage("Please keep your password under 50 characters");
                return;
            }

            CreateUser(client.DisplayName, password);
            Login(client);
        }

        private void OnLogin(Client client, ChatData chatData)
        {
            if (Users.All(x => x.Username != client.DisplayName))
            {
                client.SendMessage("You need to register first");
                return;
            }

            var args = chatData.Message.Split().ToList();
            args.RemoveAt(0);

            if (args.Count == 0)
            {
                client.SendMessage("Usage /login (password)");
                return;
            }

            var password = string.Join(" ", args);
            var user = Users.Single(x => x.Username == client.DisplayName);
            if (!user.PasswordVerify(password))
            {
                client.SendMessage("Password incorrect");
                return;
            }

            Login(client);
        }

        public string Name => "User module";

        public string Description => "Manages the users group, permissions and authentication";
    }
}
