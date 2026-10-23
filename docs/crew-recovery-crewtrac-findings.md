# Crew Recovery — CrewTrac Native Findings

Working reference for the "cancel/rebuild/reload/renumber/reassign" crew-recovery planning. Consolidates everything reverse-engineered from `Z:\Hugo\CT Source` that's directly relevant, so the recovery outline doesn't have to re-derive it. Each section links to the fuller memory/doc it came from.

## 1. Pairing Load (`CT1100`/`CT1105`)

Full field-level spec: [`pairing-load-file-format-spec.md`](pairing-load-file-format-spec.md).

Key constraints for recovery planning:
- **`pairing_number` in the Master record is the only numbering mechanism** — whatever's in that field becomes the permanent `PrgNo`. No separate renumber step exists in the load path.
- **Duplicate check is pure key-existence** (`prgno`+`prgdate`), ignoring cancel status. A load file cannot reuse a just-canceled pairing's number.
- Technically accepts a 6-char suffixed number (e.g. `A7123C`) via raw `strncpy` — but this does **not** create genuine parent/child linkage (see §3).
- **No pick/drop field exists in the load record** (`CT1105.h:112`, `pairing_detail` struct) — pick/drop codes (§4) can only be set afterward via CTWPM Modify, never via the load file.
- Never writes `FH` — a loaded leg has no `Status` (`A`/`I`) until something else populates it.

## 2. Replacement Pairing workflow (`CT1510`/`CT1520`/`CT1530`)

A genuine, purpose-built, pre-existing 3-program batch workflow — near-exact match for the recovery plan's cancel→renumber→reassign shape.

| Step | Program | Deployed as | Does |
|---|---|---|---|
| 1 | `CT1510` | `CTW1510.exe` | **Cancel original pairings** — reads a flat file of `prgid`s, cancels each via `ct2410` (same engine CTWPM's interactive Cancel uses), creates an `RP` record |
| 2 | `CT1520` | `CTW1520.exe` | **Enter replacement pairings** — updates `RP` with the actual replacement/new pairing number. This is the real "renumber" step for this scenario |
| 3 | `CT1530` | `CTW1530.dll` (via `ct_report`, §5) | **Assign replacement pairings** — reads `RP`, automatically reassigns every crew member who was on the canceled original onto its replacement; produces `xxxxasgn.lis`; safely re-runnable |

**Not yet confirmed**: `RP` record layout, `CT1510`'s input file format, whether `CT1520` requires the replacement pairing to already exist in `PM` (likely yes — `CT1530` needs somewhere real to reassign crew to). None of `CT1510`/`CT1520` take command-line args — both need their input file picked via their own UI, same as `CTWLoad.exe`.

## 3. Suffix versioning (`last_suffix`)

- `PMPrg.cpp::genNextPrgVersion()` always blanks position 5 of the pairing number before reading — it always reads the **parent** (blank-suffix, root) `PM` record's `last_suffix`, never an intermediate suffixed version.
- `CT2290.cpp` (real commit path) sets `pm.last_suffix = hdr->prgno[5]` **on the parent**, gated by `if (ohdr->prgno[5] == ' ')`.
- Suffix letters are always auto-assigned sequentially (next after current `last_suffix`, or `'A'` if none) — never user-targetable to a specific letter.
- **Implication for recovery**: if a load file directly injects a suffixed number (e.g. loading `A7123C` when `A7123`/`A7123A`/`A7123B` already exist), nothing updates the parent's `last_suffix` — a manual write to the parent `PM` record is required afterward to avoid a future collision with CT's own auto-assignment.

## 4. Pick/Drop (`FL.CodeType` / `FL.PickDrop`)

A **leg-level** "this leg doesn't count" mechanism — a third one alongside `FH.Status='I'` (§6) and `PM.cancel`.

- `FL.PickDrop[3]` — a TR02 transaction code. `FL.CodeType` (1 char) — `A`=assign, `D`=drop, `B`=absence (only these 3 of TR02's 10 `t02action` values are valid here; see below).
- Set only via CTWPM's Modify UI (grid inline editor `PMStringGrid.cpp` or the newer `TLegEditDialog`/`Leg` model) — **never via Pairing Load**.
- Two different propagation behaviors exist in the codebase (worth resolving before relying on either): the grid editor (`propagateDropPU()`) only propagates the code forward through the rest of the pairing when `codetype=='B'`; the newer `Leg::setDropPickup` propagates unconditionally regardless of codetype.
- **Live downstream effects of `D`/`B`** (confirmed across `CT2131.cpp`, `CT2150.C`, `CT2153.C`, `CT2610.C`, `ct2144.c`): block time zeroed, excluded from duty/pairing credit accumulation, excluded from Post processing — **the same bucket as a canceled leg (`status=='I'`)**. `CT2220.C` (Modify's diff engine) flags the leg `inventory='D'`, which opens/frees a position slot — the real driver behind open-time/open-position counts.
- The one thing it does **not** do: auto-create a crew `AB` (Absence) record — that logic exists in `CT2131.cpp` (lines ~996-1169) but is dead, commented-out code.
- `TR02.t02action` values: `A`=Assign, `B`=Absence, `D`=Drop, `F`=Day off, `O`=Other time, `N`=Non-M/S, `R`=Rday, `X`=Release from duty, `C`=Check-In, `Y`=Notify (only A/B/D valid for `FL.CodeType`). `TR02.t02activity_type` (independent, mutually-exclusive classification): `E`=Extension, `R`=Reroute/Resked, `S`=Bypass Min Day, `O`=Other, `C`=Cancellation — consulted by the pay engine (`CT2394.C`, `CT8521/8531.C`) for `codetype=='A'` legs to decide extension/reroute/MMG handling.

## 5. `ct_report` DLL dispatch (callable native logic)

Full trace: memory `ct-report-dll-dispatch-mechanism`.

- ~200 Report/Load modules (`CTW1100.DLL` among them) export a single, plain `extern "C"` entry point: `ct_report(TdmAlias*, char* report_id, char* error_message, TCTStatusProc*, CTINT param_count, char* param_array[])`. Confirmed via `dumpbin /exports` on the real deployed `CTW1100.DLL` (ordinal 1).
- Dispatched via `ReportDllManager` (`LoadLibrary` + `GetProcAddress(handle, "ct_report")`) and `CallReportDLL` (`Lib\Reports\ReportParamsUtils.cpp:575`).
- **Real caller**: `CTWLoad.exe` (`CTWLoad\LoadParamsForm.cpp` → `TfrmLoadParams::doLoad()` → `CallReportDLL`). **Live-tested 2026-09-23**: `CTWLOAD.EXE CT1100` launches and opens directly to the Pairing Load params window (window title "Pairing Load") — confirms the load name → DLL name resolution (`CT1100` → `CTW1100.DLL`) works end-to-end from the command line.
- Evaluate (`ct2710`) and MinDay credit (`ct2392`) remain a genuine dead end for direct calling — they only exist statically linked into `Pairing.lib`/`Pay.lib`, no DLL at all.
- Contrast: `CT1510`/`CT1520` are standalone EXEs with no `ct_report` export and no command-line args — window automation (already proven for `ctwpm.exe`'s Evaluate button) would be the only way to drive them programmatically, not a command-line call.

## 6. Canceled-leg detection (`FH.Status`)

- CTWPM displays a leg in red (canceled) based on `FH.Status == 'I'` (`PMGridColors.h` / `PMGridItem.cpp:971`), not the `TR02`/`t02activity_type=='C'` field that was the first (wrong) guess.
- The `FL`/`FH` join (`CTDataSet.Designer.cs`) is a `LEFT OUTER JOIN` requiring `fh.status IN ('A','I')` **and** a taxi/air-return time match — meaning `Status` can come back `NULL` (a third, unresolved state) even when a real `FH` row exists, if the join's time-match condition fails.
- `CTPairing.cs`'s `FillInMissingFH` fallback only covers the taxi/air-return case (`Citypair_Deptcity == Citypair_Arrvcity`) — normal legs with a failed join are left permanently unresolved.
- Production data: 63,660 `'A'` vs 309 `'I'` (since Sept 2025).

## 7. Open items / not yet built

- The actual "remove canceled legs, rebuild, ensure city continuity" tool — diagnosed in full (§6) but not built.
- `RP` table layout and `CT1510` input file format (§2).
- The `PM.last_suffix` fixup script for the suffix-injection load plan (§3).
- Resolution of the two-different-propagation-behavior discrepancy in pick/drop code entry (§4) before it's relied on for recovery planning.
