using System;
using System.Collections.Generic;
using System.Linq;

namespace MESInsight.Core
{
    public static class DataLoaderDiagnostics
    {
        private const int MaxEntries = 200;

        private static readonly object Lock = new object();
        private static readonly List<string> Entries = new List<string>();

        public static void Record(string context, Exception exception)
        {
            string entry =
                "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] " + context + Environment.NewLine +
                exception.GetType().Name + ": " + exception.Message + Environment.NewLine +
                exception.StackTrace;

            lock (Lock)
            {
                Entries.Add(entry);
                if (Entries.Count > MaxEntries)
                    Entries.RemoveAt(0);
            }
        }

        public static List<string> GetRecentEntries()
        {
            lock (Lock)
                return new List<string>(Entries);
        }

        public static string GetFormattedLog()
        {
            lock (Lock)
                return string.Join(Environment.NewLine + Environment.NewLine, Entries);
        }

        public static bool HasEntries
        {
            get
            {
                lock (Lock) return Entries.Count > 0;
            }
        }

        public static void Clear()
        {
            lock (Lock)
                Entries.Clear();
        }
    }
}