using System;

namespace GTAServer.PluginAPI.Attributes
{
    [AttributeUsage(AttributeTargets.Method)]
    public class Command : Attribute
    {
        /// <summary>
        /// Sets name of the command
        /// </summary>
        public string Name { get; set; }

        public Command(string name)
        {
            Name = name;
        }
    }
}
