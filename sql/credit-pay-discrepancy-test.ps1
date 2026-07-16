# Credit/Pay Discrepancy Test
#
# MinDayProcess.AddDutyPeriodToListIfNeeded flags a duty's Credit and Pay for a
# min-day top-up independently (a duty can need one without the other). This
# test verifies that flagging stays symmetric in practice: for every duty in
# every pairing MinDayProcess has actually updated (PM.Updateid_Updempno ==
# the "Updated" marker), (Credit < threshold) must never disagree with
# (Pay < threshold). A mismatch would mean one field was left under the
# min-day floor while the other was raised -- a bug in AddDutyPeriodToListIfNeeded
# or UpdateDutyCreditsAndPay.
#
# Re-run this any time MinDayProcess's credit/pay logic changes.
#
# Must run through the 32-bit PowerShell host (DATPSADSN is a 32-bit-only
# Pervasive ODBC driver):
#   C:\Windows\SysWOW64\WindowsPowerShell\v1.0\powershell.exe -File sql\credit-pay-discrepancy-test.ps1

Add-Type -AssemblyName System.Data

# Read the "Updated" marker empno from the shared config -- the same value
# MinDayProcess itself uses -- rather than hardcoding it.
function Get-MinDayMarkerUpdated {
    foreach ($configPath in @("D:\SFI\EXE\SFI.config", "Z:\SFI\EXE\SFI.config")) {
        if (Test-Path $configPath) {
            [xml]$xml = Get-Content $configPath
            $node = $xml.configuration.appSettings.add | Where-Object { $_.key -eq "MinDayMarkerUpdated" }
            if ($node) { return [uint32]$node.value.Trim() }
        }
    }
    throw "MinDayMarkerUpdated not found in SFI.config"
}

$markerUpdated = Get-MinDayMarkerUpdated
Write-Output "Using MinDayMarkerUpdated = $markerUpdated (from SFI.config)"

$conn = New-Object System.Data.Odbc.OdbcConnection("DSN=DATPSADSN")
$conn.Open()

# AH.postype_0..7 classifies each of the 8 position slots as Pilot ('P') or
# Cabin/FA ('C') -- same config MinDayProcess uses to pick MINDAYCREDIT4HR
# vs MINDAYCREDIT35HR.
$ahCmd = $conn.CreateCommand()
$ahCmd.CommandText = "SELECT Postype_0,Postype_1,Postype_2,Postype_3,Postype_4,Postype_5,Postype_6,Postype_7 FROM AH"
$ahReader = $ahCmd.ExecuteReader()
$postype = @()
if ($ahReader.Read()) {
    for ($i = 0; $i -le 7; $i++) { $postype += $ahReader[$i].ToString().Trim() }
}
$ahReader.Close()

$cmd = $conn.CreateCommand()
$cmd.CommandText = "SELECT Prgid_Prgno_Base+Prgid_Prgno_Eqpt+Prgid_Prgno_3+Prgid_Prgno_4_6 AS PrgNo, Prgid_Prgdate, " +
    "Position_0,Position_1,Position_2,Position_3,Position_4,Position_5,Position_6,Position_7 FROM PM WHERE Updateid_Updempno = ?"
[void]$cmd.Parameters.AddWithValue("marker", [int]$markerUpdated)
$reader = $cmd.ExecuteReader()
$pairings = @()
while ($reader.Read()) {
    $pos = @()
    for ($i = 0; $i -le 7; $i++) { $pos += [int]$reader[2 + $i] }
    $pairings += [PSCustomObject]@{ PrgNo = $reader["PrgNo"].ToString().Trim(); PrgDate = $reader["Prgid_Prgdate"].ToString(); Pos = $pos }
}
$reader.Close()

Write-Output ("Total marked pairings: " + $pairings.Count)

$discrepancyCount = 0
foreach ($p in $pairings) {
    $pilotCount = 0
    $faCount = 0
    for ($i = 0; $i -le 7; $i++) {
        if ($postype[$i] -eq 'P') { $pilotCount += $p.Pos[$i] }
        if ($postype[$i] -eq 'C') { $faCount += $p.Pos[$i] }
    }
    # Mirrors MinDayProcess.ProcessPairing's MINDAYCREDIT selection.
    $threshold = 210
    if ($p.PrgDate -ge "20220901" -and $pilotCount -gt 0 -and $faCount -eq 0) { $threshold = 240 }

    $dbCmd = $conn.CreateCommand()
    $dbCmd.CommandText = "SELECT Dpno, Actcdt_Domtime+Actcdt_Inttime AS Credit, Actpay_Domtime+Actpay_Inttime AS Pay FROM DB WHERE Prgid_Prgno=? AND Prgid_Prgdate=?"
    [void]$dbCmd.Parameters.AddWithValue("prgno", $p.PrgNo)
    [void]$dbCmd.Parameters.AddWithValue("date", $p.PrgDate)
    $dbReader = $dbCmd.ExecuteReader()
    while ($dbReader.Read()) {
        $credit = [int]$dbReader["Credit"]
        $pay = [int]$dbReader["Pay"]
        $creditBelow = $credit -lt $threshold
        $payBelow = $pay -lt $threshold
        if ($creditBelow -ne $payBelow) {
            $discrepancyCount++
            Write-Output ("DISCREPANCY: {0} {1} Dpno={2} Credit={3} Pay={4} Threshold={5} (PilotCount={6} FACount={7})" -f `
                    $p.PrgNo, $p.PrgDate, $dbReader["Dpno"], $credit, $pay, $threshold, $pilotCount, $faCount)
        }
    }
    $dbReader.Close()
}

Write-Output ("`nTotal duties with credit/pay discrepancy vs threshold: " + $discrepancyCount)
$conn.Close()
