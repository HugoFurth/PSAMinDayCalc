-- Re-seeds all 4 MinDayCalc synthetic TR09 "system user" marker rows
-- (MINDAY_APPLIED_USER/99901, MINDAY_NO_UPDATE_NEEDED_USER/99902,
-- MINDAY_EXCEPTION_USER/99903, MINDAY_ERROR_USER/99904).
--
-- Written after a data reset wiped all 4 rows from DATPSADSN (confirmed
-- 2026-08-19 via the doc-validated read technique in
-- docs/superpowers/plans/2026-07-15-mindaycalc-pm-ms-examination-markers.md,
-- Step 4: query T09username LIKE 'MinDay%' and decode T09Key_Key with
-- reader.GetBytes(), never by converting the CHAR(10) column to a .NET
-- string first -- the packed binary key does not round-trip through string
-- conversion reliably). Re-run this whole file any time the data is
-- refreshed and PairingInspect starts showing "Employee 999xx" instead of
-- proper marker names.
--
-- Does NOT check for existing rows first -- confirm the rows are actually
-- missing (see verification query at the bottom) before running, or you'll
-- create duplicates.
--
-- TR09.T09Key_Key is a CHAR(10) column holding a packed 4-byte little-endian
-- unsigned int (the marker id) followed by 6 ASCII spaces -- confirmed
-- empirically against every other TR09 row and against ctFILES.h's
-- TABLEKEY struct (short number; char key[10];). There is no hex/byte-literal
-- syntax in Zen SQL (only String/Number/Date/Time/TimeStamp literals), and a
-- literal string containing a raw 0x00 byte breaks the SQL parser. CAST(int
-- AS BINARY(4)) produces big-endian bytes, the reverse of what's needed, so
-- each int literal below is pre-byte-swapped so its big-endian CAST comes out
-- as the correct little-endian bytes for that marker id. Assigning the
-- BINARY(4) result directly into the CHAR(10) column fails ("ERROR [22018]
-- ... Error in assignment") -- it must be re-cast to CHAR(4) first; the
-- engine then auto-pads the remaining 6 bytes of the CHAR(10) column with
-- spaces, matching every other row in the table.
--
-- Marker id -> byte-swapped int literal (all four share the same high bytes
-- 0x86 01 00 since 99901-99904 are consecutive and fall in the same 256-value
-- range -- only the low byte changes):
--   99901 -> 1032192256   ("MinDay - Updated")
--   99902 -> 1048969472   ("MinDay - No Update Needed")
--   99903 -> 1065785088   ("MinDay - Exception")
--   99904 -> 1082523904   ("MinDay - Error" -- originally seeded/verified live
--                          2026-07-28 by 2026-07-28-add-mindaycalc-tr09-error-marker.sql)

INSERT INTO TR09
    (T09Key_Number, T09Key_Key, T09pswd, T09secLevel1, T09secLevel2,
     T09rptId, T09secCrewtype, T09username, T09usertype, T09RestrictCrewmsg,
     T09Active, T09GroupId)
VALUES
    (9, CAST(CAST(1032192256 AS BINARY(4)) AS CHAR(4)), '      ', 0, ' ', '    ', 'B',
     'MinDay - Updated', 'O', ' ', 'N', 'DIRECTORS ');

INSERT INTO TR09
    (T09Key_Number, T09Key_Key, T09pswd, T09secLevel1, T09secLevel2,
     T09rptId, T09secCrewtype, T09username, T09usertype, T09RestrictCrewmsg,
     T09Active, T09GroupId)
VALUES
    (9, CAST(CAST(1048969472 AS BINARY(4)) AS CHAR(4)), '      ', 0, ' ', '    ', 'B',
     'MinDay - No Update Needed', 'O', ' ', 'N', 'DIRECTORS ');

INSERT INTO TR09
    (T09Key_Number, T09Key_Key, T09pswd, T09secLevel1, T09secLevel2,
     T09rptId, T09secCrewtype, T09username, T09usertype, T09RestrictCrewmsg,
     T09Active, T09GroupId)
VALUES
    (9, CAST(CAST(1065785088 AS BINARY(4)) AS CHAR(4)), '      ', 0, ' ', '    ', 'B',
     'MinDay - Exception', 'O', ' ', 'N', 'DIRECTORS ');

INSERT INTO TR09
    (T09Key_Number, T09Key_Key, T09pswd, T09secLevel1, T09secLevel2,
     T09rptId, T09secCrewtype, T09username, T09usertype, T09RestrictCrewmsg,
     T09Active, T09GroupId)
VALUES
    (9, CAST(CAST(1082523904 AS BINARY(4)) AS CHAR(4)), '      ', 0, ' ', '    ', 'B',
     'MinDay - Error', 'O', ' ', 'N', 'DIRECTORS ');

-- Verification (run through the 32-bit ODBC host, DATPSADSN is 32-bit-only):
--   Add-Type -AssemblyName System.Data
--   $conn = New-Object System.Data.Odbc.OdbcConnection("DSN=DATPSADSN")
--   $conn.Open()
--   $cmd = $conn.CreateCommand()
--   $cmd.CommandText = "SELECT T09Key_Key, T09username FROM TR09 WHERE T09Key_Number = 9 AND T09username LIKE 'MinDay%'"
--   $reader = $cmd.ExecuteReader()
--   while ($reader.Read()) {
--       $bytes = New-Object byte[] 4
--       [void]$reader.GetBytes(0, 0, $bytes, 0, 4)
--       $empno = [BitConverter]::ToUInt32($bytes, 0)
--       Write-Output ("empno={0}  username=[{1}]" -f $empno, $reader.GetValue(1).ToString().Trim())
--   }
--   $reader.Close()
--   $conn.Close()
-- Expected: four rows, empno=99901/99902/99903/99904 with the usernames above.
