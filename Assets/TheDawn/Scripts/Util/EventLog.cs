using System.Collections.Generic;

namespace TheDawn
{
    public enum LogKind { Info, Good, Warning, Danger }

    public struct LogEntry
    {
        public string Text;
        public LogKind Kind;
        public string Stamp;
    }

    public static class EventLog
    {
        public static readonly List<LogEntry> Entries = new List<LogEntry>();
        const int Max = 200;

        public static void Add(string text, LogKind kind = LogKind.Info)
        {
            string stamp = GameClock.Instance != null ? GameClock.Instance.ShortStamp : "";
            Entries.Add(new LogEntry { Text = text, Kind = kind, Stamp = stamp });
            if (Entries.Count > Max) Entries.RemoveAt(0);
            UnityEngine.Debug.Log("[The Dawn] " + stamp + " " + text);
        }

        public static void Clear() { Entries.Clear(); }
    }
}
