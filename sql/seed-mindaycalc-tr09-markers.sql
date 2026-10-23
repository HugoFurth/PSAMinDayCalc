-- Seeds the 4 MinDayCalc synthetic TR09 "system user" marker rows
-- (MINDAY_APPLIED_USER/99901, MINDAY_NO_UPDATE_NEEDED_USER/99902,
-- MINDAY_EXCEPTION_USER/99903, MINDAY_ERROR_USER/99904) into a cttable.dat.
--
-- Reusable version of 2026-08-19-reseed-all-mindaycalc-tr09-markers.sql --
-- same 4 literal INSERTs, kept here under a non-dated name since this gets
-- re-run against fresh/new cttable.dat copies, not just the one-time
-- production reseed that file documents.
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
--   99904 -> 1082523904   ("MinDay - Error")
--
-- Does NOT check for existing rows first -- confirm the rows are actually
-- missing before running, or you'll create duplicates.

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
