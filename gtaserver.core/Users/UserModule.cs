using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using GTAServer.PluginAPI.Events;
using GTAServer.ProtocolMessages;
using GTAServer.Users.Groups;
using Microsoft.Extensions.Logging;
using GTAServer.PluginAPI;
using GTAServer.PluginAPI.Entities;

namespace GTAServer.Users
{
    public class UserModule : IPermissionProvider
    {
        private ILogger _logger;
        private SQLiteConnection _connection;

        private static readonly List<User> Users = new List<User>();
        private static readonly Dictionary<string, List<Permission>> Groups = new Dictionary<string, List<Permission>>();

        private GameServer _gameServer;

        public UserModule(GameServer gameServer)
        {
            _gameServer = gameServer;

            _logger = Util.LoggerFactory.CreateLogger<UserModule>();
            _logger.LogInformation("Loading user storage");
        }

        public void Start()
        {
            var filename = Path.Combine(System.AppContext.BaseDirectory, "users.db");
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

            LoadGroups();

            ConnectionEvents.OnJoin.Add(OnJoin);
            ConnectionEvents.OnDisconnect.Add(OnLeave);

            _gameServer.RegisterCommand("register", OnRegister);
            _gameServer.RegisterCommand("login", OnLogin);

            _gameServer.RegisterCommand("setgroup", OnSetGroup);
        }

        public void Stop()
        {
            // TODO save all the unsaved
            _logger.LogInformation("Closing database");
            _connection.Close();
        }

        public void LoadGroups()
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
                    _logger.LogWarning($"An exception occurred while loading group '{group.Name}': {e.Message}");
                }
            });
        }

        public List<Permission> GetPermissions(List<Group> groups, string group)
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
                    _logger.LogWarning($"Failed to parse permission {x}: {e.Message}");
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

        public GroupsConfiguration LoadGroupsConfiguration()
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
                _logger.LogInformation("No groups configuration found, creating a new one");
                using (var stream = File.OpenWrite(path))
                {
                    ser.Serialize(stream, cfg = new GroupsConfiguration(
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

        public void SetGroup(long id, string group)
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
                    SetGroup(user.Id, "user");

                    _logger.LogWarning($"{user.Username} had an unknown group and has been reset to user");
                }

                client.SendMessage("Welcome back, use /login (password) to login to your account");
            }
            else
            {
                client.SendMessage("You can register an account using /register (password)");
            }

            reader.Close();
        }

        private void OnLeave(Client client)
        {
            if (Users.Any(x => x.Username == client.DisplayName))
            {
                var id = Users.RemoveAll(x => x.Username == client.DisplayName);

                _logger.LogDebug($"{client.DisplayName} left, removing from memory ({id})");
            }
        }

        private void OnSetGroup(CommandContext ctx, List<string> args)
        {
            if (args.Count < 2) return;

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
                ctx.SendMessage("Usage /register (password)");
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
                ctx.SendMessage("Usage /login (password)");
                return;
            }

            var password = string.Join(" ", args);
            var user = Users.Single(x => x.Username == ctx.Client.DisplayName);
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
            var group = (user == null) ? "user" : user.Group;

            return Groups[group].Any(x => x.Type == type && x.Name == permission);
        }

        public string Name => "User module";

        public string Description => "Manages the users group, permissions and authentication";
    }
}
