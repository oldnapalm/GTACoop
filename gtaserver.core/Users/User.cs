using GTAServer.ProtocolMessages;

namespace GTAServer.Users
{
    /// <summary>
    /// Represents a user in the default permission provider
    /// </summary>
    public class User
    {
        /// <summary>
        /// Gets id of the user
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// Gets or sets the username of the user
        /// </summary>
        public string Username { get; set; }

        /// <summary>
        /// Gets or sets the password hash of the user
        /// </summary>
        public string Password { get; set; }

        /// <summary>
        /// Gets or sets the group of the user
        /// </summary>
        public string Group { get; set; }

        /// <summary>
        /// Gets or sets the associated <see cref="GTAServer.ProtocolMessages.Client"/> for this user
        /// </summary>
        public Client Client { get; set; }

        /// <summary>
        /// Checks if the password equals the stored hash
        /// </summary>
        /// <param name="password">Password to check</param>
        /// <returns></returns>
        public bool PasswordVerify(string password)
        {
            return BCrypt.Net.BCrypt.Verify(password, Password);
        }
    }
}
