using System;
using System.Collections.Generic;
using System.Data.OleDb;
using System.Text;

namespace MinDayProcessNS
    {
    // Self-heals TR09 if any of the 4 synthetic MinDay marker rows have been
    // wiped -- confirmed to actually happen (2026-08-19 data reset wiped all 4,
    // required a manual SQL re-seed). Extracted out of MinDayProcess.cs (already
    // large/complex) into its own file; kept as a standalone static class rather
    // than a `partial class MinDayProcess` split, so it stays decoupled from
    // MinDayProcess's own instance state -- callers pass in the connection and
    // marker map explicitly instead of this class reaching into private fields.
    internal static class MinDayMarkerUsers
        {
        // Config key is the field's own name (e.g. "MINDAY_APPLIED_USER") -- these
        // are literally TR09 marker user IDs, so the config key and the field it
        // fills need no separate mapping name. Cached since MinDayConfig.AppSettingRaw
        // re-walks the config section on every call and these values never change
        // for the life of the process.
        private static readonly Dictionary<string, uint> _minDayUserCache = new Dictionary<string, uint>();

        internal static uint GetMinDayUser(string fieldName)
            {
            uint value;
            if (!_minDayUserCache.TryGetValue(fieldName, out value))
                {
                value = uint.Parse(SFIConfigUtils.MinDayConfig.AppSettingRaw(fieldName));
                _minDayUserCache[fieldName] = value;
                }
            return value;
            }

        // Packs an empno into TR09's key format: CHAR(10) = 4-byte little-endian
        // uint + 6 ASCII spaces. Must be bound as an ANSI (codepage 1252) string,
        // never a raw byte[] -- PervasiveOLEDB silently matches zero rows for a
        // byte[] parameter against this CHAR(10) column (see memory:
        // tr09-empno-key-lookup). Confirmed live 2026-10-22 that the same
        // string-encoded binding also works for an INSERT parameter, not just SELECT.
        internal static string PackTR09Key(uint empno)
            {
            byte[] keyBytes = new byte[10];
            BitConverter.GetBytes(empno).CopyTo(keyBytes, 0);
            for (int i = 4; i < 10; i++) keyBytes[i] = 0x20;
            return Encoding.GetEncoding(1252).GetString(keyBytes);
            }

        // Checks each (empno, display text) pair against TR09 and inserts any
        // missing rows. Reuses the caller's own open connection -- never opens a
        // second independent connection to the same Pervasive data source
        // in-process, confirmed live to crash the native provider with
        // AccessViolationException. Returns a message for each row that had to be
        // re-inserted, so the caller can log/report it however it reports status
        // (this class has no dependency on MinDayProcess's own status-event
        // mechanism).
        internal static List<string> EnsureExist(OleDbConnection connection, Dictionary<uint, string> markers)
            {
            List<string> reinsertedMessages = new List<string>();

            if (connection.State != System.Data.ConnectionState.Open)
                connection.Open();

            foreach (KeyValuePair<uint, string> marker in markers)
                {
                string packedKey = PackTR09Key(marker.Key);

                bool exists;
                using (OleDbCommand cmdCheck = connection.CreateCommand())
                    {
                    cmdCheck.CommandText = "SELECT T09username FROM TR09 WHERE T09Key_Number = 9 AND T09Key_Key = ?";
                    cmdCheck.Parameters.AddWithValue("key", packedKey);
                    exists = cmdCheck.ExecuteScalar() != null;
                    }

                if (!exists)
                    {
                    using (OleDbCommand cmdInsert = connection.CreateCommand())
                        {
                        cmdInsert.CommandText =
                            "INSERT INTO TR09 (T09Key_Number, T09Key_Key, T09pswd, T09secLevel1, T09secLevel2, " +
                            "T09rptId, T09secCrewtype, T09username, T09usertype, T09RestrictCrewmsg, T09Active, T09GroupId) " +
                            "VALUES (9, ?, '      ', 0, ' ', '    ', 'B', ?, 'O', ' ', 'N', 'DIRECTORS ')";
                        cmdInsert.Parameters.AddWithValue("key", packedKey);
                        cmdInsert.Parameters.AddWithValue("username", marker.Value);
                        int inserted = cmdInsert.ExecuteNonQuery();
                        if (inserted != 1)
                            throw new Exception("Failed to insert TR09 marker row for empno " + marker.Key + " (" + marker.Value + ")");
                        reinsertedMessages.Add(
                            "TR09 marker row for \"" + marker.Value + "\" (empno " + marker.Key + ") was missing -- re-inserted");
                        }
                    }
                }

            return reinsertedMessages;
            }
        }
    }
