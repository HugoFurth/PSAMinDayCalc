# Ad-hoc check, NOT the CO fields: across all non-cancelled 2025 PM rows, does
# the plain (non-carry-over) SkedBlock ever exceed SkedCredit, or ActBlock
# exceed ActCredit? This is the base credit-vs-block relationship, independent
# of bid-period carry-over -- a different question from
# docs/cocredit-block-exceeds-credit-analysis.md, which only looked at the
# *CO suffixed fields.
#
# Uses an explicit date-range predicate (Prgid_Prgdate BETWEEN start/end of
# 2025) rather than LIKE.
#
# Must run through the 32-bit PowerShell host (DATPSADSN is a 32-bit-only
# Pervasive ODBC driver):
#   C:\Windows\SysWOW64\WindowsPowerShell\v1.0\powershell.exe -File sql\2025-block-vs-credit-noco.ps1

Add-Type -AssemblyName System.Data

$conn = New-Object System.Data.Odbc.OdbcConnection("DSN=DATPSADSN")
$conn.Open()

$cmd = $conn.CreateCommand()
$cmd.CommandText = @"
SELECT Prgid_Prgno_Base+Prgid_Prgno_Eqpt+Prgid_Prgno_3+Prgid_Prgno_4_6 AS PrgNo,
       Prgid_Prgdate, ActualInd,
       Skdblk_Domtime, Skdblk_Inttime, Skdcdt_Domtime, Skdcdt_Inttime,
       Actblk_Domtime, Actblk_Inttime, Actcdt_Domtime, Actcdt_Inttime
FROM PM
WHERE Cancel <> 'C' AND Prgid_Prgdate >= ? AND Prgid_Prgdate <= ?
"@
[void]$cmd.Parameters.AddWithValue("start", "20250101")
[void]$cmd.Parameters.AddWithValue("end", "20251231")
$reader = $cmd.ExecuteReader()

$total = 0
$skedViolations = @()
$actViolations = @()
while ($reader.Read()) {
    $total++
    $skedBlock = [int]$reader["Skdblk_Domtime"] + [int]$reader["Skdblk_Inttime"]
    $skedCredit = [int]$reader["Skdcdt_Domtime"] + [int]$reader["Skdcdt_Inttime"]
    $actBlock = [int]$reader["Actblk_Domtime"] + [int]$reader["Actblk_Inttime"]
    $actCredit = [int]$reader["Actcdt_Domtime"] + [int]$reader["Actcdt_Inttime"]

    if ($skedBlock -gt $skedCredit) {
        $skedViolations += [PSCustomObject]@{
            PrgNo = $reader["PrgNo"].ToString().Trim()
            PrgDate = $reader["Prgid_Prgdate"].ToString().Trim()
            SkedBlock = $skedBlock
            SkedCredit = $skedCredit
        }
    }
    if ($actBlock -gt $actCredit) {
        $actViolations += [PSCustomObject]@{
            PrgNo = $reader["PrgNo"].ToString().Trim()
            PrgDate = $reader["Prgid_Prgdate"].ToString().Trim()
            ActualInd = $reader["ActualInd"].ToString().Trim()
            ActBlock = $actBlock
            ActCredit = $actCredit
        }
    }
}
$reader.Close()
$conn.Close()

Write-Output ("Total non-cancelled 2025 PM rows: " + $total)
Write-Output ("SkedBlock > SkedCredit violations: " + $skedViolations.Count)
Write-Output ("ActBlock > ActCredit violations: " + $actViolations.Count)
Write-Output ""
Write-Output "-- Sked violations (first 30) --"
$skedViolations | Select-Object -First 30 | Format-Table -AutoSize
Write-Output "-- Act violations (first 30) --"
$actViolations | Select-Object -First 30 | Format-Table -AutoSize
