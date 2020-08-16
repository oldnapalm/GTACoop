using System;

namespace GTAServer.Users.Groups
{
    public class InvalidPermissionException : Exception
    {
        public InvalidPermissionException()
        {
        }

        public InvalidPermissionException(string message)
            : base(message)
        {
        }

        public InvalidPermissionException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
