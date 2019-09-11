using System;

namespace GTAServer.PluginAPI.Attributes
{
    [AttributeUsage(AttributeTargets.Method)]
    class Command : Attribute
    {
        public string Name { get; set; }

        public Command(string name)
        {
            Name = name;
        }
    }
}
