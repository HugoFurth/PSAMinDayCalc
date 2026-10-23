# Count of distinct active pairings per bid period, for every bid period in 2026,
# using MP (crew-on-pairing) directly -- "active" meaning MP.Status = 'A'.
#
# Mastid_Bidate is stored as the 6-digit "YYYYMM" format (e.g. "202608" for Aug
# 2026) -- CONFIRMED live by running this script: a '26%' LIKE pattern matched
# nothing, '2026%' is what actually works. (The 4-digit "YYMM" TR01.T01Key_Key
# format is a different, narrower internal key -- not what's stored here.)
#
# A pairing can have multiple active crew rows in MP (one per active crew
# member); DISTINCT on the pairing key avoids counting the same pairing once
# per crew member.
#
# Not run yet -- script only, per request.
#
# Must run through the 32-bit PowerShell host (DATPSADSN is a 32-bit-only
# Pervasive ODBC driver):
#   C:\Windows\SysWOW64\WindowsPowerShell\v1.0\powershell.exe -File sql\2026-active-pairings-per-bidperiod.ps1

Add-Type -AssemblyName System.Data

$conn = New-Object System.Data.Odbc.OdbcConnection("DSN=DATPSADSN")
$conn.Open()

$cmd = $conn.CreateCommand()
$cmd.CommandText = @"
SELECT Mastid_Bidate,
       COUNT(DISTINCT Prgid_Prgno + Prgid_Prgdate) AS ActivePairingCount
FROM MP
WHERE Status = 'A' AND Mastid_Bidate LIKE '2026%'
GROUP BY Mastid_Bidate
ORDER BY Mastid_Bidate
"@

$rows = @()
$reader = $cmd.ExecuteReader()
while ($reader.Read()) {
    $rows += [PSCustomObject]@{
        BidPeriod          = $reader["Mastid_Bidate"].ToString().Trim()
        ActivePairingCount = [int]$reader["ActivePairingCount"]
    }
}
$reader.Close()
$conn.Close()

Write-Output "Active pairing count per 2026 bid period (MP, Status='A', distinct Prgid_Prgno+Prgid_Prgdate):"
$rows | Format-Table BidPeriod, ActivePairingCount -AutoSize
Write-Output ""
Write-Output ("Total across all 2026 bid periods: " + ($rows | Measure-Object -Property ActivePairingCount -Sum).Sum)
