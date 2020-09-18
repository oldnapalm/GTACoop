using System;
using System.Linq;

namespace GTAServer.Users.Groups
{
    public class Permission
    {
        public PermissionType Type { get; }
        public string Name { get; }

        public Permission(PermissionType type, string permission)
        {
            Type = type;
            Name = permission;
        }

        /// <summary>
        /// Parses a string to permission
        /// </summary>
        /// <param name="permission"></param>
        /// <returns></returns>
        /// <exception cref="InvalidPermissionException"></exception>
        public static Permission Parse(string permission)
        {
            var split = permission.Split(".");
            if (split.Length <= 1)
                throw new InvalidPermissionException($"Permission contains no value");

            if (!Enum.TryParse(split[0], true, out PermissionType type))
            {
                throw new InvalidPermissionException($"Invalid permission type");
            }

            return new Permission(type, string.Join(".", split.Skip(1)));
        }
    }
}
