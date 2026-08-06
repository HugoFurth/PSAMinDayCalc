# Noon-Report / 5PM-Release Exemption — For Discussion

*Draft, 2026-07-29. This is a discussion starting point, not a finished recommendation — content below will change as we work through it together.*

## Background

There's an existing rule that skips Min Day evaluation for certain duty periods based on their report and release times. It appears to have been written for a contract rule that predates 2023. We're bringing it up now because, in reviewing the current system, we found it's still active today — and not only on old trips.

## What the rule currently does

**On a trip with only one duty period to evaluate:**
The duty is skipped (no Min Day top-up considered) if it reports after 12:00 noon local, and every version of its end time — actual, estimated, and scheduled — is before 5:00 PM local.

**On a trip with more than one duty period to evaluate:**
- The **first** duty period is skipped if it reports at or after 12:00 noon local.
- The **last** duty period is skipped if every version of its end time is at or before 5:00 PM local.
- Every duty period in between is evaluated normally — this rule never affects them.

## The issue we found

This rule is currently tied to which Min Day Credit amount applies (3:30 vs. 4:00) rather than to the trip's actual date. In practice, that means it only ever applies to Flight Attendant trips (since Pilot trips use the 4:00 amount) — but it applies to **all** Flight Attendant trips, including ones flown today, not just trips from before the 2023 change it was apparently written for.

We want your direction on whether this exemption should still apply going forward for Flight Attendant trips, or whether it should be retired now that the rule it was originally tied to no longer applies.

## A smaller inconsistency, worth noting

The exact boundary times are handled slightly differently depending on whether the trip has one duty period to evaluate or more than one:

- On a single-duty trip: the report time must be *strictly after* noon, and every end time must be *strictly before* 5:00 PM.
- On a multi-duty trip: the first duty's report time counts *at or after* noon, and the last duty's end times count *at or before* 5:00 PM.

So a duty reporting at exactly noon, or releasing at exactly 5:00 PM, may or may not trigger the exemption depending on which of these two situations it falls into. This looks like an unintentional inconsistency rather than a deliberate distinction, and is worth deciding on regardless of the outcome above.

---

**Questions for discussion:**
1. Should this exemption continue to apply to current Flight Attendant trips, or should it be retired?
2. If it continues to apply, should the exact-noon / exact-5PM boundary be handled the same way for single- and multi-duty trips?
