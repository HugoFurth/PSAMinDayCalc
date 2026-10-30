-- Deletes duplicate PX(ErrorNo_0=6500) rows, keeping exactly one per pairing.
-- InsertException/InsertPX (MinDayProcess.cs's CreatePX) has no delete/dedup step of
-- its own, so re-evaluating a pairing while the mixed ab/RAS/TTA-crew condition
-- stayed true inserted another identical PX(6500) row on every batch pass --
-- confirmed live on W7061/20260802 and A7441/20260901, both with duplicate rows
-- differing only by RaisedDate/RaisedTime. CreatePX was separately fixed
-- (2026-10-26, PXExists check) to stop creating new ones going forward; this is a
-- one-time cleanup of rows that already exist.
--
-- Scope: Prgid_Prgdate >= 20260701 only (July 2026 and later). Verified read-only
-- against the live DB before writing this file: 389 duplicate groups, 777 rows
-- would be deleted in this scope (3,292 groups / 8,738 rows if ever run unscoped
-- across the whole table -- NOT what this script does).
--
-- "Duplicate" = same Prgid_Prgno_Base/Eqpt/3/4_6 + Prgid_Prgdate + ErrorNo_0 = 6500,
-- identified via GROUP BY ... HAVING COUNT(*) > 1 rather than an enumerated row list.
-- Keeps the earliest-raised row per group (lowest RaisedDate, then lowest RaisedTime)
-- via a correlated EXISTS that only matches a row if an earlier-raised sibling exists
-- in the same group -- the earliest row in each group has no such sibling, so it's
-- never matched and never deleted. Both the GROUP BY/HAVING subquery and the
-- correlated EXISTS were confirmed to execute on this engine (DATPSADSN) before
-- writing this file.

DELETE FROM PX p1
WHERE p1.ErrorNo_0 = 6500
AND p1.Prgid_Prgdate >= '20260701'
AND EXISTS (
    SELECT 1 FROM PX p2
    WHERE p2.Prgid_Prgno_Base = p1.Prgid_Prgno_Base
      AND p2.Prgid_Prgno_Eqpt = p1.Prgid_Prgno_Eqpt
      AND p2.Prgid_Prgno_3 = p1.Prgid_Prgno_3
      AND p2.Prgid_Prgno_4_6 = p1.Prgid_Prgno_4_6
      AND p2.Prgid_Prgdate = p1.Prgid_Prgdate
      AND p2.ErrorNo_0 = 6500
      AND (p2.RaisedDate < p1.RaisedDate
           OR (p2.RaisedDate = p1.RaisedDate AND p2.RaisedTime < p1.RaisedTime))
);

-- Verification (run before/after to confirm the expected row count changed):
--   SELECT COUNT(*) FROM PX WHERE ErrorNo_0 = 6500 AND Prgid_Prgdate >= '20260701';
--   -- before: should show the pre-cleanup total; after: should be 777 fewer.
--
--   SELECT COUNT(*) FROM (
--       SELECT Prgid_Prgno_Base, Prgid_Prgno_Eqpt, Prgid_Prgno_3, Prgid_Prgno_4_6, Prgid_Prgdate
--       FROM PX
--       WHERE ErrorNo_0 = 6500 AND Prgid_Prgdate >= '20260701'
--       GROUP BY Prgid_Prgno_Base, Prgid_Prgno_Eqpt, Prgid_Prgno_3, Prgid_Prgno_4_6, Prgid_Prgdate
--       HAVING COUNT(*) > 1
--   ) AS dupgroups;
--   -- after: should return 0.
