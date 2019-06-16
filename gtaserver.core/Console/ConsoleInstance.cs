using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using GTAServer.Console.Modules;

namespace GTAServer.Console
{
    internal class ConsoleInstance
    {
        private readonly Dictionary<string, Action<List<string>>> _consoleCommands = new Dictionary<string, Action<List<string>>>();

        public readonly ILogger Logger;
        public delegate void TextEnteredHandler(ConsoleInstance sender, string text);

        /// <summary>
        /// Gets called whenever something is entered into the server console
        /// </summary>
        public event TextEnteredHandler TextEntered; 

        public ConsoleInstance(ILogger logger)
        {
            Logger = logger;

            TextEntered += TextEnteredEvent;
        }

        /// <summary>
        /// Internal function to handle text entered in console and reroute to commands
        /// </summary>
        private void TextEnteredEvent(ConsoleInstance sender, string text)
        {
            var splitted = text.Split();

            // check if command exists
            if (_consoleCommands.ContainsKey(splitted[0]))
            {
                var args = splitted.ToList();
                args.RemoveAt(0);

                _consoleCommands.Single(x => x.Key == splitted[0]).Value(args);
            }
            else
            {
                Logger.LogInformation("Command not found");
            }
        }

        public void Start()
        {
            var thread = new Thread(() =>
            {
                while (true)
                {
                    var input = System.Console.ReadLine();
                    if (string.IsNullOrWhiteSpace(input)) continue;

                    TextEntered?.Invoke(this, input);
                }
            }) {Name = "Console Instance"};

            thread.Start();
        }

        /// <summary>
        /// Adds an console command to be executed
        /// </summary>
        /// <param name="commandName">The name of the command to add</param>
        /// <param name="callback">The callback which gets called whenever the command is executed</param>
        public void AddCommand(string commandName, Action<List<string>> callback)
        {
            if (_consoleCommands.ContainsKey(commandName))
                throw new Exception("A command with this name already exists");

            _consoleCommands.Add(commandName, callback);
        }

        /// <summary>
        /// Adds an module to the class which then gets called with the current class object
        /// </summary>
        /// <param name="module">The module to add</param>
        public void AddModule(IModule module)
        {
            module.OnEnable(this);
        }

        /// <summary>
        /// Writes an new line to console using internal logger
        /// </summary>
        /// <param name="text">The text to write to logger</param>
        public void WriteLn(string text)
        {
            Logger.LogInformation(text);
        }
    }
}
