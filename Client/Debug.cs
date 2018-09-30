using NativeUI;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Drawing;

namespace GTACoOp
{
    public class Debug
    {
        public Dictionary<string, DateTime> log;

        public Debug()
        {
            log = new Dictionary<string, DateTime>();
        }

        public void Tick()
        {
            new UIResText(string.Join("\n", log.OrderBy(x => x.Value).Select(x => x.Value.ToString("HH:mm:ss") + " " + x.Key)), new Point(10, 10), 0.5f).Draw();
        }

        public void AddToDebug(string msg)
        {
            if (log.ContainsKey(msg))
            {
                log[msg] = DateTime.Now;
                return;
            }

            log.Add(msg, DateTime.Now);

            if(log.Count > 10)
            {
                log.Remove(log.OrderBy(x => x.Value).Last().Key);
            }
        }

        public void Clear()
        {
            log.Clear();
        }
    }
}
