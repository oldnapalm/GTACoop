using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpRaven;
using SharpRaven.Data;

namespace GTACoOp
{
    class Sentry
    {
        private static DateTime _lastEvent;
        private static Type _lastException;

        public static int SentryTimeout = (int)TimeSpan.FromSeconds(10).TotalMilliseconds;

        public static RavenClient Raven { get; internal set; }

        public static void Capture(Exception ex, object extras = null)
        {
            if (_lastEvent.CompareTo(DateTime.Now) <= SentryTimeout && _lastException == ex.GetType()) return;
            var @event = new SentryEvent(ex);

            if (extras != null)
            {
                @event.Extra = extras;
            }

            Raven.Capture(@event);

            _lastEvent = DateTime.Now;
            _lastException = ex.GetType();
        }
    }
}
