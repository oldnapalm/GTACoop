using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;
using GTAServer.PluginAPI.Events;
using GTAServer.ProtocolMessages;
using GTAServer.Users.Groups;
using Microsoft.Extensions.Logging;
using GTAServer.PluginAPI;
using GTAServer.PluginAPI.Entities;
using GTAServer.Logging;

namespace GTAServer.Users
{
    /// <summary>
    /// The default permission provider
    /// </summary>
    public class UserModule : IPermissionProvider
    {
        private ILogger _logger;
        private SQLiteConnection _connection;

        private static readonly List<User> Users = new List<User>();
        private static readonly Dictionary<string, List<Permission>> Groups = new Dictionary<string, List<Permission>>();

        private GameServer _gameServer;
        private GroupsConfiguration _configuration;

        private string _filename = Path.Combine(AppContext.BaseDirectory, "Data", "Users.db");

        public UserModule(GameServer gameServer)
        {
            _gameServer = gameServer;

            _logger = Util.LoggerFactory.CreateLogger<UserModule>();
            _logger.LogInformation(LogEvent.UsersMgr, "Loading user storage");

            UpdateDatabase();
        }

        private void UpdateDatabase()
        {
            // migrate old database to new location
            var filename = Path.Combine(AppContext.BaseDirectory, "users.db");

            if (File.Exists(filename))
            {
                _logger.LogInformation("Migrating old database to new location");

                File.Move(filename, _filename);
            }

            // could be repurposed in future to also migrate database file to newer versions
        }

        public void Start()
        {
            if (!File.Exists(_filename))
            {
                SQLiteConnection.CreateFile(_filename);
            }

            var connectionString = new SQLiteConnectionStringBuilder()
            {
                DataSource = _filename,
                Version = 3
            };

            try
            {
                _connection = new SQLiteConnection(connectionString.ToString());
                _connection.Open();

                // dummy query to make sure database is valid
                new SQLiteCommand("PRAGMA database_list", _connection).ExecuteNonQuery();
            }
            catch (Exception)
            {
                _logger.LogCritical(LogEvent.UsersMgr, "Failed to open user database, make sure it's readable by the server or try deleting it.");
                _gameServer.PermissionProvider = null;

                return;
            }

            new SQLiteCommand(@"
                CREATE TABLE IF NOT EXISTS `users` (
	                `Id`	    INTEGER PRIMARY KEY AUTOINCREMENT,
	                `Username`  TEXT,
	                `Password`  TEXT,
	                `Group` 	TEXT
                );"
            , _connection).ExecuteNonQuery();

            try
            {
                LoadGroups();
            }
            catch (Exception e)
            {
                _logger.LogError(LogEvent.UsersMgr, e.Message);
                _logger.LogCritical(LogEvent.UsersMgr, "Failed to load groups configuration, default permission system and login will be disabled.");

                _gameServer.PermissionProvider = null;

                return;
            }

            ConnectionEvents.OnJoin.Add(OnJoin);
            ConnectionEvents.OnDisconnect.Add(OnLeave);

            _gameServer.RegisterCommand(new Command { 
                Name = "register",
                Description = "Register a new account with the current username",
                Usage = "Usage: register <password>" }, OnRegister);

            _gameServer.RegisterCommand(new Command { 
                Name = "login",
                Description = "Login to the account of the current username",
                Usage = "Usage: login <password>" }, OnLogin);

            _gameServer.RegisterCommand(new Command { 
                Name = "setgroup",
                Description = "Set a player in a group",
                Usage = "Usage: setgroup <player> <group>" }, OnSetGroup);
        }

        public void Stop()
        {
            // TODO save all the unsaved
            _logger.LogInformation(LogEvent.UsersMgr, "Closing database");
            _connection.Close();
        }

        private void LoadGroups()
        {
            var groups = LoadGroupsConfiguration();

            groups.Groups.ForEach(group =>
            {
                var permissions = GetPermissions(groups.Groups, group.Name);

                try
                {
                    Groups.Add(group.Name, permissions);
                }
                catch (Exception e)
                {
                    _logger.LogWarning(LogEvent.UsersMgr, $"An exception occurred while loading group '{group.Name}': {e.Message}");
                }
            });

            if (!Groups.ContainsKey(groups.Default))
            {
                throw new Exception($"Default group '{groups.Default}' does not exist");
            }

            if (!Groups.ContainsKey(groups.DefaultGuest))
            {
                throw new Exception($"Default guest group '{groups.DefaultGuest}' does not exist");
            }

            _configuration = groups;
        }

        private List<Permission> GetPermissions(List<Group> groups, string group)
        {
            var permissions = new List<Permission>();

            groups.Find(x => x.Name == group)?.Permissions.ForEach(x =>
            {
                Permission permission;
                try
                {
                    permission = Permission.Parse(x);
                }
                catch (InvalidPermissionException e)
                {
                    _logger.LogWarning(LogEvent.UsersMgr, $"Failed to parse permission {x}: {e.Message}");
                    return;
                }

                if (permission.Type == PermissionType.Group && groups.Any(y => y.Name == permission.Name))
                {
                    permissions.AddRange(GetPermissions(groups, permission.Name));
                    return;
                }

                permissions.Add(permission);
            });

            return permissions;
        }

        private GroupsConfiguration LoadGroupsConfiguration()
        {
            var ser = new XmlSerializer(typeof(GroupsConfiguration));
            var path = System.AppContext.BaseDirectory + Path.DirectorySeparatorChar + "Configuration" +
                       Path.DirectorySeparatorChar + "groups.xml";

            GroupsConfiguration cfg = null;
            if (File.Exists(path))
            {
                using (var stream = File.OpenRead(path)) cfg = (GroupsConfiguration)ser.Deserialize(stream);
            }
            else
            {
                _logger.LogInformation(LogEvent.UsersMgr, "No groups configuration found, creating a new one");
                using (var stream = File.OpenWrite(path))
                {
                    var writer = XmlWriter.Create(stream, new XmlWriterSettings { Indent = true });

                    ser.Serialize(writer, cfg = new GroupsConfiguration(
                        // default groups
                        new Group("admin", "command.kick", "command.tp", "command.setgroup", "group.user"),
                        new Group("user", "command.login", "command.register", "command.about", "command.help",
                            "command.plugins", "command.tps", "command.vote", "command.say", "command.who"))
                    );
                }
            }

            return cfg;
        }

        /// <summary>
        /// Creates a new user in the database and stores the created user in memory
        /// </summary>
        /// <param name="username">The choosen username</param>
        /// <param name="password">The choosen password</param>
        private void CreateUser(string username, string password)
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
                Group = _configuration.Default
            });
        }

        /// <summary>
        /// Links the client to associated user
        /// </summary>
        /// <param name="client"></param>
        private void Login(Client client)
        {
            var user = Users.First(x => x.Username == client.DisplayName);
            user.Client = client;

            client.SendMessage("You have been logged in");
        }

        private void SetGroup(long id, string group)
        {
            var query = new SQLiteCommand("UPDATE `users` SET `Group` = @group WHERE `Id` = @id", _connection);
            query.Parameters.AddWithValue("@id", id);
            query.Parameters.AddWithValue("@group", group);

            query.ExecuteNonQuery();

            Users.Find(x => x.Id == id).Group = group;
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

                if (!Groups.ContainsKey(user.Group))
                {
                    SetGroup(user.Id, _configuration.Default);

                    _logger.LogWarning(LogEvent.UsersMgr, $"{user.Username} had an unknown group and has been reset to " + _configuration.Default);
                }

                client.SendMessage("Welcome back, use /login (password) to login to your account");
            }
            else
            {
                client.SendMessage("You can register an account using /register (password)");
            }
            reader.Close();

            var seconds = DateTime.Now.Ticks / TimeSpan.TicksPerSecond;
            TimeSpan GameSeconds = TimeSpan.FromSeconds((seconds * 30) % 86400);

            client.SendNativeCall(0xC8CA9670B9D83B3B, GameSeconds.Hours, GameSeconds.Minutes, GameSeconds.Seconds); // ADVANCE_CLOCK_TIME_TO
        }

        private void OnLeave(Client client)
        {
            if (Users.Any(x => x.Username == client.DisplayName))
            {
                var id = Users.RemoveAll(x => x.Username == client.DisplayName);

                _logger.LogDebug(LogEvent.UsersMgr, $"{client.DisplayName} left, removing from memory ({id})");
            }
        }

        private void OnSetGroup(CommandContext ctx, List<string> args)
        {
            if (args.Count < 2)
            {
                ctx.SendMessage("Usage: setgroup <player> <group>");
                return;
            }

            var user = Users.Find(x => x.Username == args[0]);
            if (user == null)
            {
                ctx.SendMessage("User not found.");
                return;
            }

            if (!Groups.ContainsKey(args[1]))
            {
                ctx.SendMessage("Group not found.");
                return;
            }

            SetGroup(user.Id, args[1]);
            ctx.SendMessage("Group updated.");
        }

        private void OnRegister(CommandContext ctx, List<string> args)
        {
            if(ctx.Sender is ConsoleCommandSender)
            {
                ctx.SendMessage("You cannot execute this command as console");
                return;
            }
			
            if (Users.Any(user => user.Username == ctx.Client.DisplayName))
            {
                ctx.SendMessage("Can't register an account on this username");
                return;
            }

            if (args.Count == 0)
            {
                ctx.SendMessage("Usage /register <password>");
                return;
            }

            var password = string.Join(" ", args);

            // bcrypt limits
            if (password.Length > 50)
            {
                ctx.SendMessage("Please keep your password under 50 characters");
                return;
            }

            CreateUser(ctx.Client.DisplayName, password);
            Login(ctx.Client);
        }

        private void OnLogin(CommandContext ctx, List<string> args)
        {
            if(ctx.Sender is ConsoleCommandSender)
            {
                ctx.SendMessage("You cannot execute this command as console");
                return;
            }
			
            if (Users.All(x => x.Username != ctx.Client.DisplayName))
            {
                ctx.SendMessage("You need to register first");
                return;
            }

            if (Users.Any(x => x.Client == ctx.Client))
                return;

            if (args.Count == 0)
            {
                ctx.SendMessage("Usage /login <password>");
                return;
            }

            var password = string.Join(" ", args);
            var user = Users.First(x => x.Username == ctx.Client.DisplayName);
            if (!user.PasswordVerify(password))
            {
                ctx.SendMessage("Password incorrect");
                return;
            }

            Login(ctx.Client);
        }

        public bool HasPermission(Client client, PermissionType type, string permission)
        {
            var user = Users.Find(x => x.Client == client);
            var group = (user == null) ? _configuration.DefaultGuest : user.Group;

            return Groups[group].Any(x => x.Type == type && x.Name == permission);
        }
    }
}
