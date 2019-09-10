using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;

namespace GTAServer.Users.Groups
{
    public class Groups
    {
        [XmlElement(ElementName = "Groups")]
        public List<Group> GroupsList { get; set; } = new List<Group>()
        {
            new Group("admin", "command.kick", "command.tp", "group.user"),
            new Group("user", "command.login", "command.register", "command.about", "command.help", "command.plugins", "command.tps")
        };
    }

    public class Group
    {
        public Group() { }

        public Group(string name, params string[] permissions)
        {
            Name = name;
            Permissions = permissions.ToList();
        }

        public string Name { get; set; }

        [XmlArray(ElementName = "Permissions")]
        [XmlArrayItem(ElementName = "Permission")]
        public List<string> Permissions { get; set; }
    }
}
