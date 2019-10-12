using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GTAServer.Users.Groups
{
    public class Permission
    {
        public Permission(PermissionType type, string permission)
        {
            Type = type;
            Name = permission;
        }

        public static Permission Parse(string permission)
        {
            var split = permission.Split(".");
            if(split.Length <= 1)
                throw new Exception($"Invalid permission '{permission}'");

            PermissionType type;
            if (!Enum.TryParse(split[0], true, out type))
            {
                throw new Exception($"Invalid permission '{permission}', type '{split[0]}' is not valid");
            }

            return new Permission(type, string.Join(".", split.Skip(1)));
        }

        public PermissionType Type { get; set; }
        public string Name { get; set; }
    }
}
