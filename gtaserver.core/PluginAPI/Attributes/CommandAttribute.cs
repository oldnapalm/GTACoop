using System;

namespace GTAServer.PluginAPI.Attributes
{
    [AttributeUsage(AttributeTargets.Method)]
    public class CommandAttribute : Attribute
    {
        /// <summary>
        /// Sets name of the command
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Sets the description displayed about the command
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Sets the usage message of the command
        /// </summary>
        public string Usage { get; set; }

        public CommandAttribute(string name)
        {
            Name = name;
        }
    }
}
