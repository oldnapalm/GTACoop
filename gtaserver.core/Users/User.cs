using System;
using System.Collections.Generic;
using System.Text;
using GTAServer.ProtocolMessages;

namespace GTAServer.Users
{
    public class User
    {
        public long Id { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string Group { get; set; }
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
