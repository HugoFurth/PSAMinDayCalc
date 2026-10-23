# CrewTrac Pairing Load File Format — Technical Spec

Reverse-engineered directly from the actual parsing source: `Z:\Hugo\CT Source\Load DLLs\CTW1100\CT1105.Cpp` / `CT1105.h` (module `CT1105`, "Monthly Pairing Load program", invoked via `CT1100` — "PAIRING LOAD"). Every field width, encoding rule, and validation message below is taken directly from that code, not inferred. Where something wasn't found in the code, it's marked as such rather than guessed.

Not found in this pass: the load *input* file's own naming convention (the `xxxxprgs.lis` name referenced in `CT1105.Cpp`'s header comment is the program's own error/summary *report* output, not the input file).

## 1. Physical file format

- Plain text, **one record per line**.
- Line terminator: CRLF (`\x0D\x0A`) — `readrec()` also accepts a bare LF (`\x0A`, "End-of-Line from UNIX"). A trailing PCTOOLS-style `\x1A` (Ctrl-Z) EOF marker is tolerated.
- Every line is a **fixed-column** record — each field occupies an exact character range, space- or zero-padded, no delimiters between fields.
- Files are processed one at a time (`process_1105 each file separately. The files are NOT merged.`).

## 2. Record types

Every line's **first character** is a one-digit record-type code:

| Code | Meaning | Struct |
|---|---|---|
| `1` | Pairing Master (header) | `pairing_master_crewplan` / `pairing_master_softouch` |
| `2` | Flight leg (operated or airline deadhead) | `pairing_detail` |
| `3` | Duty break (duty period) | `duty_break_1105` |
| `4` | Deadhead — other airline | `dead_head` |
| `5` | Non-flying activity | `activity` |

Any other value → `"Invalid Rec Type on Input File"` (aborts the whole run if `runtype=='R'`).

## 3. Two format dialects: CrewPlan vs. Softouch

Selected by a `runtype`/`input_system` parameter (`'C'` = CrewPlan, `'S'` = Softouch). For record types 2–5, **Softouch is a strict prefix subset** of CrewPlan — Softouch records are simply shorter (missing trailing fields), so the same struct offsets apply and the extra CrewPlan-only fields are just not present/read. **Record type 1 (Pairing Master) is the one exception** — it has two entirely separate struct layouts (see §4.1).

## 4. Sequencing rules (per the code's own historical comment, `CT1105.Cpp:13-24`)

1. **The `1` (Master) record must be first** for each pairing.
2. Records **`2`, `4`, and `5` must appear in the exact order they occur within the pairing** — `ct0070()` (the city-index builder) requires legs in true chronological order.
3. As of a later revision, this was extended: **all records for a pairing, including duty breaks (`3`), must be in order.**

## 5. Field encoding conventions (apply throughout)

- **Dates**: 8 chars, `YYYYMMDD`, no separators.
- **Times**: 4 chars, `HHMM`, digit string (not colon-separated).
- **Numeric fields**: right-justified ASCII digit strings. Parsed by `str_to_num`/`str_to_long_1105` — **leading/embedded spaces are silently converted to `'0'`**; any genuinely non-digit character triggers `"Nonnumeric Data in Numeric Field in Input Record"` and aborts that pairing's load. No sign character supported.
- **Single-character flag/indicator fields**: a blank space (`' '`) means "not set / not applicable"; any other character is used verbatim as the actual code (e.g. a deadhead-type letter).
- **`"0000"` in a no-op field** is treated as equivalent to blank (explicitly normalized before use).

## 6. Record Type 1 — Pairing Master

### 6.1 CrewPlan variant (`pairing_master_crewplan`)

| Field | Width | Notes |
|---|---|---|
| `rec_type` | 1 | `'1'` |
| `pairing_number` | 6 | **Becomes the pairing's `PrgNo` directly** — no separate numbering step exists anywhere else in the load process. 2nd character drives equipment-type lookup (Table 04) — see §8. |
| `pairing_date` | 8 | Together with `pairing_number`, this is the full key checked for duplicates (§8). |
| `from_date` | 8 | |
| `to_date` | 8 | |
| `frequency` | 7 | Day-of-week operate pattern (1=Mon...7=Sun, per the DDF's own convention referenced elsewhere in CT). |
| `no_op_1`…`no_op_8` | 4 each | Up to 8 non-operating date overrides; `"0000"` = not set. |
| `report_date` / `report_time` | 8 / 4 | First duty's report. GMT variance is computed live via `ct0992` against the base city's daylight/standard table — can fail (`"Daylight/Standard Table Entry Not Found"`). |
| `skdend_date` / `skdend_time` | 8 / 4 | Also seeds `estend`/`actend` identically at load time (all three start equal to the scheduled value). |
| `first_flight` | 5 | |
| `first_deptime` | 4 | |
| `number_duty_periods` | 3 | → `pm.numdp` |
| `FAR_domintl` | 1 | |
| `time_away_from_base` | 5 | TAFB, minutes — → `pm.skdtafb`/`esttafb`/`acttafb` (all seeded equal) |
| `standup_overngt` | 1 | `'Y'`/other |
| `position` | 32 | **Repeating groups of 4 bytes**: 2-char position code (matched against the airline's `ah.posmask` table, e.g. `CA`, `FO`) + 2-char zero-padded count. Terminated by a run of 4 spaces. Unmatched code → `"Can Not Find Position in Airline Header Record"`; count over the equipment's max → `"Position over max for eqpt"`. |
| `rest_required` | 4 | |
| `FAR_part_ind` | 1 | |
| `block_day` | `PRGMAXDAYS` × 4 | Per-calendar-day block-time buckets |
| `type_guar` | 1 | |
| `duty_guar` | 4 | |
| `duty_guar_carryover` | 4 | |
| `trip_guar` | 4 | |
| `block_time` | 4 | |
| `deadhead_credit` | 4 | |
| `deadhead_pay` | 4 | |
| `total_credit` | 4 | |
| `total_pay` | 4 | |
| `mend_carryo_block` | 4 | "month-end carryover" block |
| `mend_carryo_deadh_credit` | 4 | |
| `mend_carryo_deadh_pay` | 4 | |
| `mend_carryo_total_credit` | 4 | |
| `mend_carryo_total_pay` | 4 | |
| `language` | 4 × `LANGUAGE_MASK` | Up to 4 language-requirement entries; each validated against the airline's language table (invalid code → non-fatal warning, that language entry is simply dropped, load continues) |
| `duty` | `PRGMAXDAYS` × 4 | |
| `pay` | `PRGMAXDAYS` × 4 | |
| `contract_domintl` | 1 | |
| `limit_hours` | 4 | |
| `mend_carryo_limit_hours` | 4 | |
| `fatigue_units` | 4 | |
| `last` | 1 | Sentinel/terminator byte |

### 6.2 Softouch variant (`pairing_master_softouch`)

Same field layout through `duty[PRGMAXDAYS][4]`, then **stops** — no `pay[]`, no `contract_domintl`, no `limit_hours`, no `mend_carryo_limit_hours`. Goes straight to `fatigue_units` then `last`. (Comment explicitly warns: *"When you make changes in [the CrewPlan loader] you MIGHT have to duplicate them in `load_pm_softouch_1105()`"* — the two loaders are hand-maintained in parallel, not derived from one shared struct.)

## 7. Record Type 2 — Flight Leg Detail (`pairing_detail`)

| Field | Width | Notes |
|---|---|---|
| `rectype` | 1 | `'2'` |
| `prgno` / `prgdate` | 6 / 8 | Must match the pairing's Master record |
| `flight` | 5 | |
| `flt_date` | 8 | |
| `dpt_city` / `arr_city` | 3 / 3 | |
| `dpt_city_date` / `dpt_city_time` | 8 / 4 | |
| `arr_city_date` / `arr_city_time` | 8 / 4 | |
| `leg_num` | 3 | |
| `deadhead_ind` | 1 | Blank = operated leg. Non-blank = deadhead code letter (positions are **not** counted toward crew composition when this is set — `check_positions_1105` is only called `if (deadhead_ind == ' ')`). |
| `leg_break_ind` | 1 | Blank = n/a; else becomes `fl.layovind` directly (native layover-indicator convention: `D`=end duty day, `P`=end pairing, `S`=prior short layover). |
| `FAR_domintl` | 1 | |
| `block_time` / `block_cross_over` | 4 / 4 | |
| `credit` | 4 | |
| `deadhead_pay` | 4 | |
| *— CrewPlan-only fields below; absent in Softouch —* | | |
| `fartype` | 1 | AOC-added; values `'1' '5' 'S' 'F'(ferry) 'I' 'M' 'N'`(F/A no-rules) |
| `total_crew_complement` | 2 | Pilots: total pilot count. Cabin: delta from FAR minimum. |
| `dept_variance` / `arrv_variance` | 5 / 5 | Format `'-0360'`/`' 0360'` = minutes off, sign-prefixed (this field alone *is* signed, unlike the general numeric convention in §5) |
| `leg_eqpt_type` | 3 | |
| `contract_domintl` | 1 | **Softouch's last real field** — everything after this in the CrewPlan struct is unused filler for Softouch. |
| `filler` | 503 | Reserved |

## 8. Record Type 3 — Duty Break (`duty_break_1105`)

| Field | Width | Notes |
|---|---|---|
| `rectype` | 1 | `'3'` |
| `pairing_number` / `pairing_date` | 6 / 8 | |
| `duty_seq_num` | 3 | |
| `duty_num` | 4 | |
| `start_date` / `start_time` | 8 / 4 | |
| `end_date` / `end_time` | 8 / 4 | |
| `FAR_domintl` | 1 | Per AOC, set from the leg's own `FAR_domintl` for consistency with CrewTrac's own convention |
| `sch_duty_time` | 4 | |
| `layover_time` | 5 | |
| `layover_city` | 3 | |
| `hotel_name` / `hotel_phone` | 26 / 14 | |
| `rest_type` | 2 | |
| `rest_number` | 1 | |
| `rest_begin` | 4 | |
| `rest_required` | 4 | |
| `duty_guarantee` | 4 | |
| `total_block` | 4 | |
| `deadhead_credit` / `deadhead_pay` | 4 / 4 | |
| `duty_credit` / `duty_pay` | 4 / 4 | |
| `day_night` | 1 | `'D'` or `'N'` — **Softouch's last real field.** |
| *— CrewPlan-only —* | | |
| `fatigue_units` | 4 | Air BC-specific |
| `contract_domintl` | 1 | |
| `layover_block_to_block` | 5 | Added 1996-12-04 |
| `limit_hours` | 4 | |
| `filler` | 451 | Reserved |

## 9. Record Type 4 — Other-Airline Deadhead (`dead_head`)

| Field | Width | Notes |
|---|---|---|
| `rec_type` | 1 | `'4'` |
| `pairing_number` / `pairing_date` | 6 / 8 | |
| `pairing_leg_num` | 3 | |
| `airline` | 3 | The other carrier's code |
| `flight` / `flight_date` | 5 / 8 | |
| `dpt_city`/`dpt_city_date`/`dpt_city_time` | 3/8/4 | |
| `arr_city`/`arr_city_date`/`arr_city_time` | 3/8/4 | |
| `credit` / `pay` | 4 / 4 | |
| `leg_break_ind` | 1 | Same convention as Record 2 — **Softouch's last real field.** |
| *— CrewPlan-only —* | | |
| `dept_variance` / `arrv_variance` | 5 / 5 | Marked "Not used" in the struct comment |
| `other_airline` | 3 | Marked "Not used" |
| `deadhead_ind` | 1 | Added 1998-11-24; only honored `if (input_system==CREWPLAN)` — falls back to `DEFAULT_DEADHEAD_CODE` otherwise |
| `filler` | 513 | Reserved |

## 10. Record Type 5 — Non-Flying Activity (`activity`)

| Field | Width | Notes |
|---|---|---|
| `rec_type` | 1 | `'5'` |
| `pairing_number` / `pairing_date` | 6 / 8 | |
| `pairing_leg_num` | 3 | |
| `transaction_code` | 3 | The activity/transaction code (e.g. ground-time codes) |
| `transaction_date` | 8 | |
| `dpt_city`/`dpt_city_date`/`dpt_city_time` | 3/8/4 | |
| `arr_city`/`arr_city_date`/`arr_city_time` | 3/8/4 | |
| `credit` / `pay` | 4 / 4 | |
| `leg_break_ind` | 1 | **Softouch's last real field.** |
| *— CrewPlan-only —* | | |
| `duty_ind` | 1 | Marked "Not used" |
| `filler` | 531 | Reserved |

## 11. Validation / error conditions confirmed in code

| Message | Trigger | Fatal to whole run? |
|---|---|---|
| `Invalid Rec Type on Input File` | First-byte record code not `1`-`5` | Only if `runtype=='R'` |
| `Duplicate Pairing Master` | A `PM` record already exists at this exact `prgno`+`prgdate` — **regardless of that record's cancel status** | No — just that pairing is skipped (`prg_not_loaded++`) |
| `Pairing Is Too Many Days` | Computed span (`skdend_date` − `pairing_date` + 1) exceeds `PRGMAXDAYS` | No — that pairing skipped |
| `Daylight/Standard Table Entry Not Found` | GMT-variance lookup (`ct0992`) failed for the base city/date/time | No — that pairing skipped |
| `Invalid Language on Pairing. Language Not Loaded` | Language code not in the airline's table | **Non-fatal** — that one language entry is dropped, load continues |
| `Can Not Find Position in Airline Header Record` | Position code in `position[]` doesn't match `ah.posmask` | No — that pairing skipped |
| `Position over max for eqpt` | Position count exceeds the equipment's configured max | No — that pairing skipped |
| `Nonnumeric Data in Numeric Field in Input Record` | A numeric field contains a non-digit, non-space character | No — that pairing skipped |
| `Contract record error - Cabin/Pilot/Both` | `ct2102()` (contract record read) fails on the very first Master record | **Yes — aborts the entire file** |

## 12. Interaction with recovery/renumber planning (see prior discussion)

Two facts from this format directly constrain the cancel→rebuild→load→renumber plan:

- **§6.1 confirms `pairing_number` is the *only* place the new `PrgNo` is set** — there is no later renumbering step anywhere in `CT1105`/`CT1100`. Whatever value sits in this field of the load file becomes the pairing's permanent number.
- **§11's "Duplicate Pairing Master" check is a pure key-existence test** — it does not look at `pm.cancel`. A canceled pairing's `PM` record still occupies its key, so a load file cannot reuse a just-canceled pairing's number; it must use a genuinely different one.
