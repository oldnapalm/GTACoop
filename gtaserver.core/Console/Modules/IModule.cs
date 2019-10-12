using System;
using System.Collections.Generic;
using System.Text;
using GTAServer.Console;

namespace GTAServer.Console.Modules
{
    internal interface IModule
    {
        void OnEnable(ConsoleInstance instance);

        string Name { get; }
        string Description { get; }
    }
}
