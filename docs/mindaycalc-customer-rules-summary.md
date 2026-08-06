# PSA Min Day Process Rules

*Reflects rules for trips commencing on or after 9/1/2022. Last updated 7/30/2026.*

<p style="font-size: 1.15em;">"Min Day" is a minimum daily credit guarantee: if a crew member's actual duty period has less credit than the applicable Min Day Credit amount, the system raises that duty up to that minimum. This document describes the rules the system currently uses to determine under what conditions to apply that credit. Further, as Min Day Credit cannot properly be applied to some trips, a pairing exception is created in that case for manual review (i.e., a multi-crew trip needs to be split into single-crew trips).</p>

# Mixed Pilot/FA

Trips with a mixed Pilot/Flight Attendant crew are excluded entirely from Min Day processing. These trips are flagged with a pairing exception. To process them for Min Day credit, they need to be split into pure pilot or pure FA versions.

# Pilots

**Min Day Credit: 4:00**

Each duty period's actual credit is compared against 4:00. If it falls short, that duty is topped up to 4:00 — subject to the limits described below.

As part of Min Day, if a Pilot has a layover of more than one full calendar day between duty periods within the same trip, Min Day adds credit for each full extra day of layover, at the 4:00 rate.

## Limits on Min Day Credit

- **RAS.** If a trip is assigned to one or more Pilots on RAS, along with one or more Pilots who are not on RAS, an exception is created and the trip is left for manual review. If every assigned Pilot on the trip is on RAS, no exception is created — the trip is simply marked as not needing Min Day.
- **Single Duty Trips.** Trips with 1 duty period do not receive Min Day Credit.
- **Exclusion Codes.** If a duty period includes a non-flying activity coded with one of a specific set of exclusion codes, that whole duty period is left out of Min Day evaluation. The codes currently configured are `GRS` and `LFT`. The list is under customer control.

## Trip Rig

After Min Day has been applied, 1-for-4 trip rig is calculated and applied if the pairing is below the guarantee. The 1-for-4 is part of the Min Day process. If rig settings are changed within CrewTrac, those changes are not applied for Min Day trips.

# Flight Attendants

**Min Day Credit: 3:30**

Each duty period's actual credit is compared against 3:30. If it falls short, that duty is topped up to 3:30 — subject to the limits described below.

As part of Min Day, if a Flight Attendant has a layover of more than one full calendar day between duty periods within the same trip, Min Day adds credit for each full extra day of layover, at the 3:30 rate.

## Limits on Min Day Credit

- **RAS/TTA/Dropped for Absence.** If the trip is assigned to RAS or TTA or has been dropped for an absence, along with one or more Flight Attendants who do not meet that same criteria, an exception is created and the trip is left for manual review. If every assigned Flight Attendant on the trip is RAS or TTA or has dropped the trip for an absence, no exception is created — the trip is simply marked as not needing Min Day.
- **Single Duty Trips.** Trips with 1 duty period do not receive Min Day Credit.
- **Exclusion Codes.** If a duty period includes a non-flying activity coded with one of a specific set of exclusion codes, that whole duty period is left out of Min Day evaluation. The codes currently configured are `GRS` and `LFT`. The list is under customer control.
- **Fake Deadheads.** Duty periods made up entirely of "fake deadhead" leg(s) do not qualify for Min Day and are skipped. A leg is considered "fake" when it's coded with deadhead code "K".
- **Noon-Report/5PM-Release Exemption.** A duty period is excluded from Min Day evaluation if it reports after 12:00 noon local and releases at or before 5:00 PM local. On a trip with only one duty period to evaluate, the report must be strictly after noon and every version of the end time (actual, estimated, and scheduled) must be strictly before 5:00 PM. On a trip with more than one duty period to evaluate, only the first duty (report at or after noon) and the last duty (every end time at or before 5:00 PM) can be exempted this way — every duty in between is always evaluated.

© 2014-2026 SkyFlexx International and Zephyr Data Systems. All rights reserved worldwide.
