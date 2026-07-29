-- Adds the fourth MinDayCalc synthetic TR09 "system user" marker (MINDAY_ERROR,
-- SFI.config key MinDayMarkerError = 99904) alongside the three seeded by
-- sql/2026-07-15-seed-mindaycalc-tr09-markers.ps1 (MinDay - Updated/No Update
-- Needed/Exception, 99901-99903). Already run once and verified live against
-- DATPSADSN on 2026-07-28 -- re-running will create a duplicate row, since this
-- statement does not check for an existing row first (unlike the original
-- PowerShell seed script). Check for an existing 'MinDay - Error' row before
-- re-running.
--
-- TR09.T09Key_Key is a CHAR(10) column that actually holds a packed 4-byte
-- little-endian unsigned int (the marker id) followed by 6 ASCII spaces --
-- confirmed empirically against the three existing marker rows and against
-- ctFILES.h's TABLEKEY struct (short number; char key[10];). There is no hex/
-- byte-literal syntax in Zen SQL (only String/Number/Date/Time/TimeStamp
-- literals are supported -- confirmed against the Zen SQL Syntax Reference),
-- and a literal string containing a raw 0x00 byte breaks the SQL parser
-- (confirmed by direct test: "Syntax Error" mid-literal). CAST(int AS BINARY(4))
-- produces big-endian bytes, the reverse of what's needed, so the int literal
-- below (1082523904 = 0x40860100) is pre-byte-swapped so its big-endian CAST
-- comes out as the correct little-endian bytes for 99904 (0x00018640).
--
-- Assigning that BINARY(4) result directly into the CHAR(10) column fails
-- ("ERROR [22018] ... Error in assignment") -- it must be re-cast to CHAR(4)
-- first. The engine then auto-pads the remaining 6 bytes of the CHAR(10)
-- column with spaces, matching every other row in the table.

INSERT INTO TR09
    (T09Key_Number, T09Key_Key, T09pswd, T09secLevel1, T09secLevel2,
     T09rptId, T09secCrewtype, T09username, T09usertype, T09RestrictCrewmsg,
     T09Active, T09GroupId)
VALUES
    (9,
     CAST(CAST(1082523904 AS BINARY(4)) AS CHAR(4)),
     '      ',
     0,
     ' ',
     '    ',
     'B',
     'MinDay - Error',
     'O',
     ' ',
     'N',
     'DIRECTORS ')
