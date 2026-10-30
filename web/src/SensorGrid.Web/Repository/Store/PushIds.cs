// What this file is for: makes Firebase-style "push IDs": 20 characters that sort by creation time and never repeat.
// How it fits in: Repository folder. New alerts, reports, users and data loads get their ID on the server, so one multi-path update can write related records together.
// IDs are case-sensitive: never lowercase a URL that contains one.
namespace SensorGrid.Repository;

/// <summary>Creates new, time-ordered record IDs.</summary>
public static class PushIds
{
    // the 64 characters used, in sorting order (the same set Firebase uses)
    private const string Chars = "-0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ_abcdefghijklmnopqrstuvwxyz";
    private static readonly object Gate = new();           // only one ID is made at a time
    private static readonly Random Rng = new();
    private static long _lastTime;                         // the millisecond of the previous ID
    private static readonly int[] LastRandom = new int[12]; // the random part of the previous ID

    /// <summary>The highest character a push ID can contain. A time prefix followed by twelve of these is the last ID of that millisecond.</summary>
    public const char LastChar = 'z';

    /// <summary>
    /// The first 8 characters of every push ID made at this time. Because push IDs start with their time, a period of push-ID keys
    /// (for example the gateway's history rows) can be read as a key range.
    /// </summary>
    public static string TimePrefix(DateTime utc)
    {
        var t = new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToUnixTimeMilliseconds();
        var stamp = new char[8];
        for (var i = 7; i >= 0; i--)
        {
            stamp[i] = Chars[(int)(t % 64)];
            t /= 64;
        }
        return new string(stamp);
    }

    /// <summary>A new ID stamped with the current time.</summary>
    public static string New() => At(DateTime.UtcNow);

    /// <summary>A new ID stamped with a chosen time, so records can sort in the order they happened.</summary>
    public static string At(DateTime utc)
    {
        var now = new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToUnixTimeMilliseconds();
        lock (Gate)
        {
            var duplicate = now == _lastTime; // two IDs in the same millisecond?
            _lastTime = now;

            // the first 8 characters are the time, written in base 64, so IDs sort by time
            var stamp = TimePrefix(utc).ToCharArray();

            if (!duplicate)
            {
                // a new millisecond: pick 12 fresh random characters
                for (var i = 0; i < 12; i++) LastRandom[i] = Rng.Next(64);
            }
            else
            {
                // Same millisecond twice: add one to the random part so the ID is still new and still sorts after the last one.
                var i = 11;
                while (i >= 0 && LastRandom[i] == 63)
                {
                    LastRandom[i] = 0;
                    i--;
                }
                if (i >= 0) LastRandom[i]++;
            }

            // 8 time characters followed by 12 random ones
            var id = new char[20];
            Array.Copy(stamp, id, 8);
            for (var i = 0; i < 12; i++) id[8 + i] = Chars[LastRandom[i]];
            return new string(id);
        }
    }
}