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

        public CommandAttribute(string name)
        {
            Name = name;
        }
    }
}
