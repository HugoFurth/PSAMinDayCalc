using System;
using System.Collections.Generic;
using System.Data.OleDb;
using System.Text;
using SFIConfigUtils;

namespace PairingInspect2
    {
    public enum MinDayMarkerStatus
        {
        Applied,
        NoUpdateNeeded,
        Exception,
        Error,
        Unknown
        }

    public static class MinDayNameResolver
        {
        // Config key is the field's own name (e.g. "MINDAY_APPLIED_USER") -- these
        // are literally TR09 marker user IDs, so the config key and the value it
        // fills need no separate mapping name. Cached since MinDayConfig.AppSettingRaw
        // re-walks the config section on every call and these values never change
        // for the life of the process; without caching, a single pairing lookup in
        // the UI (IsKnownMinDayMarker + Resolve back-to-back) re-parses all four
        // twice.
        private static readonly Dictionary<string, uint> _minDayUserCache = new Dictionary<string, uint>();

        private static uint GetMinDayUser(string fieldName)
            {
            uint value;
            if (!_minDayUserCache.TryGetValue(fieldName, out value))
                {
                value = uint.Parse(MinDayConfig.AppSettingRaw(fieldName));
                _minDayUserCache[fieldName] = value;
                }
            return value;
            }

        public static bool IsKnownMinDayMarker(uint empno)
            {
            uint markerUpdated = GetMinDayUser("MINDAY_APPLIED_USER");
            uint markerNoUpdateNeeded = GetMinDayUser("MINDAY_NO_UPDATE_NEEDED_USER");
            uint markerException = GetMinDayUser("MINDAY_EXCEPTION_USER");
            uint markerError = GetMinDayUser("MINDAY_ERROR_USER");
            return empno == markerUpdated || empno == markerNoUpdateNeeded || empno == markerException || empno == markerError;
            }

        public static MinDayMarkerStatus Resolve(uint empno)
            {
            uint MINDAY_APPLIED_USER = GetMinDayUser("MINDAY_APPLIED_USER");
            uint MINDAY_NO_UPDATE_NEEDED_USER = GetMinDayUser("MINDAY_NO_UPDATE_NEEDED_USER");
            uint MINDAY_EXCEPTION_USER = GetMinDayUser("MINDAY_EXCEPTION_USER");
            uint MINDAY_ERROR_USER = GetMinDayUser("MINDAY_ERROR_USER");

            if (empno == MINDAY_APPLIED_USER) return MinDayMarkerStatus.Applied;
            if (empno == MINDAY_NO_UPDATE_NEEDED_USER) return MinDayMarkerStatus.NoUpdateNeeded;
            if (empno == MINDAY_EXCEPTION_USER) return MinDayMarkerStatus.Exception;
            if (empno == MINDAY_ERROR_USER) return MinDayMarkerStatus.Error;

            return MinDayMarkerStatus.Unknown;
            }

        // TR09.T09username is the actual source of truth for these markers' display
        // names (they're seeded there as synthetic "system user" rows -- see
        // sql/2026-07-15-seed-mindaycalc-tr09-markers.ps1 and
        // sql/2026-07-28-add-mindaycalc-tr09-error-marker.sql), so read them from
        // TR09 once at startup rather than duplicating the text as C# literals that
        // could drift out of sync with the actual rows. There is no TR09 row for
        // MinDayMarkerStatus.Unknown -- it isn't a marker, just "not yet examined".
        private static readonly Dictionary<MinDayMarkerStatus, string> _displayNames = new Dictionary<MinDayMarkerStatus, string>();

        public static void LoadDisplayNames(OleDbConnection connection)
            {
            _displayNames[MinDayMarkerStatus.Applied] = ReadTR09Username(connection, GetMinDayUser("MINDAY_APPLIED_USER"));
            _displayNames[MinDayMarkerStatus.NoUpdateNeeded] = ReadTR09Username(connection, GetMinDayUser("MINDAY_NO_UPDATE_NEEDED_USER"));
            _displayNames[MinDayMarkerStatus.Exception] = ReadTR09Username(connection, GetMinDayUser("MINDAY_EXCEPTION_USER"));
            _displayNames[MinDayMarkerStatus.Error] = ReadTR09Username(connection, GetMinDayUser("MINDAY_ERROR_USER"));
            }

        // TR09.T09Key_Key is a CHAR(10) holding a packed 4-byte little-endian uint
        // (the empno) followed by 6 ASCII spaces, not a plain string -- see
        // sql/2026-07-28-add-mindaycalc-tr09-error-marker.sql for how that packing
        // was derived.
        private static string ReadTR09Username(OleDbConnection connection, uint empno)
            {
            using (OleDbCommand cmd = connection.CreateCommand())
                {
                cmd.CommandText = "SELECT T09username FROM TR09 WHERE T09Key_Number = 9 AND T09Key_Key = ?";
                byte[] keyBytes = new byte[10];
                BitConverter.GetBytes(empno).CopyTo(keyBytes, 0);
                for (int i = 4; i < 10; i++) keyBytes[i] = 0x20;
                // PervasiveOLEDB silently matches zero rows when this parameter is bound as a
                // raw byte[] (infers OleDbType.Binary/VarBinary) -- it needs to see a CHAR-typed
                // value to compare correctly against T09Key_Key. Encoding the same bytes as an
                // ANSI (codepage 1252) string round-trips them byte-for-byte and the driver
                // then matches correctly. Confirmed live: byte[] returns null for every known
                // marker row; this string form finds all four.
                string key = Encoding.GetEncoding(1252).GetString(keyBytes);
                cmd.Parameters.AddWithValue("key", key);
                object result = cmd.ExecuteScalar();
                return result == null ? ("Employee " + empno) : result.ToString().Trim();
                }
            }

        public static string DisplayName(MinDayMarkerStatus status)
            {
            string name;
            return _displayNames.TryGetValue(status, out name) ? name : "Pending Min Day processing";
            }
        }
    }
