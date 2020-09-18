using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;

namespace GTAServer.Users.Groups
{
    public class GroupsConfiguration
    {
        public GroupsConfiguration() { }

        public GroupsConfiguration(params Group[] groups)
        {
            Groups = groups.ToList();
        }

        [XmlArray(ElementName = "Groups")]
        [XmlArrayItem(ElementName = "Group")]
        public List<Group> Groups { get; set; } = new List<Group>();
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
