# Ad-hoc check: for pairings starting 9/29 (any year) and ending in October
# (i.e. spanning a Sept->Oct bid-period boundary), does SkedBlockCO ever
# exceed SkedCreditCO? Mirrors the sched-side check in
# docs/cocredit-block-exceeds-credit-analysis.md, scoped to this specific
# start/end window per live discussion.
#
# Uses explicit date-range predicates (equality on Prgid_Prgdate, BETWEEN on
# Skdend_Date) instead of LIKE, one parameterized query per year -- LIKE
# patterns can't use the date-keyed index the way range comparisons can.
#
# Must run through the 32-bit PowerShell host (DATPSADSN is a 32-bit-only
# Pervasive ODBC driver):
#   C:\Windows\SysWOW64\WindowsPowerShell\v1.0\powershell.exe -File sql\sept29-oct-co-block-vs-credit.ps1

Add-Type -AssemblyName System.Data

$conn = New-Object System.Data.Odbc.OdbcConnection("DSN=DATPSADSN")
$conn.Open()

$rows = @()
foreach ($year in 2015..2026) {
    $prgDate = "{0}0929" -f $year
    $octStart = "{0}1001" -f $year
    $octEnd = "{0}1031" -f $year

    $cmd = $conn.CreateCommand()
    $cmd.CommandText = @"
SELECT Prgid_Prgno_Base+Prgid_Prgno_Eqpt+Prgid_Prgno_3+Prgid_Prgno_4_6 AS PrgNo,
       Prgid_Prgdate, Skdend_Date, Actend_Date, ActualInd,
       Skdblkco_Domtime, Skdblkco_Inttime, Skdcdtco_Domtime, Skdcdtco_Inttime,
       Actblkco_Domtime, Actblkco_Inttime, Actcdtco_Domtime, Actcdtco_Inttime
FROM PM
WHERE Cancel <> 'C' AND Prgid_Prgdate = ? AND Skdend_Date >= ? AND Skdend_Date <= ?
"@
    [void]$cmd.Parameters.AddWithValue("prgdate", $prgDate)
    [void]$cmd.Parameters.AddWithValue("octstart", $octStart)
    [void]$cmd.Parameters.AddWithValue("octend", $octEnd)
    $reader = $cmd.ExecuteReader()

    while ($reader.Read()) {
        $skedBlockCO = [int]$reader["Skdblkco_Domtime"] + [int]$reader["Skdblkco_Inttime"]
        $skedCreditCO = [int]$reader["Skdcdtco_Domtime"] + [int]$reader["Skdcdtco_Inttime"]
        $actBlockCO = [int]$reader["Actblkco_Domtime"] + [int]$reader["Actblkco_Inttime"]
        $actCreditCO = [int]$reader["Actcdtco_Domtime"] + [int]$reader["Actcdtco_Inttime"]

        $rows += [PSCustomObject]@{
            PrgNo = $reader["PrgNo"].ToString().Trim()
            PrgDate = $reader["Prgid_Prgdate"].ToString().Trim()
            SkdEnd = $reader["Skdend_Date"].ToString().Trim()
            ActEnd = $reader["Actend_Date"].ToString().Trim()
            ActualInd = $reader["ActualInd"].ToString().Trim()
            SkedBlockCO = $skedBlockCO
            SkedCreditCO = $skedCreditCO
            ActBlockCO = $actBlockCO
            ActCreditCO = $actCreditCO
            SkedViolation = ($skedBlockCO -gt $skedCreditCO)
            ActViolation = ($actBlockCO -gt $actCreditCO)
        }
    }
    $reader.Close()
}
$conn.Close()

Write-Output ("Total non-cancelled pairings starting 9/29 and ending in October (same year): " + $rows.Count)
Write-Output ("SkedBlockCO > SkedCreditCO violations: " + ($rows | Where-Object { $_.SkedViolation }).Count)
Write-Output ("ActBlockCO > ActCreditCO violations: " + ($rows | Where-Object { $_.ActViolation }).Count)
Write-Output ""
Write-Output "-- Sked violations --"
$rows | Where-Object { $_.SkedViolation } | Format-Table PrgNo, PrgDate, SkdEnd, SkedBlockCO, SkedCreditCO -AutoSize
Write-Output "-- Act violations --"
$rows | Where-Object { $_.ActViolation } | Format-Table PrgNo, PrgDate, ActEnd, ActualInd, ActBlockCO, ActCreditCO -AutoSize
