<#
Finds one real example pairing per MinDay condition, for showing to the client.
Run ON THE CLIENT'S MACHINE, from a deployed PairingInspect2/PSAMinDay site
(reads their own SFI.config + MinDay.config -- no hardcoded DSN or marker empnos).

Must run under 32-bit PowerShell -- PervasiveOLEDB is 32-bit only:

    C:\Windows\SysWOW64\WindowsPowerShell\v1.0\powershell.exe -ExecutionPolicy Bypass -File .\2026-10-30-find-mindaycalc-demo-examples.ps1

Optional params:
  -ExeDir   folder containing SFI.config / MinDay.config (default d:\sfi\exe)
  -YearMonth  YYYYMM to search (default 202610)
#>
param(
    [string]$ExeDir = "d:\sfi\exe",
    [string]$YearMonth = "202610"
)

Add-Type -AssemblyName System.Data

function Read-AppSetting($configPath, $key) {
    [xml]$xml = Get-Content $configPath
    $node = $xml.configuration.appSettings.add | Where-Object { $_.key.Trim() -eq $key }
    if (-not $node) { throw "Key '$key' not found in $configPath" }
    return $node.value.Trim()
}

$sfiConfigPath = Join-Path $ExeDir "SFI.config"
$minDayConfigPath = Join-Path $ExeDir "MinDay.config"
if (-not (Test-Path $sfiConfigPath)) { throw "Not found: $sfiConfigPath" }
if (-not (Test-Path $minDayConfigPath)) { throw "Not found: $minDayConfigPath" }

[xml]$sfiXml = Get-Content $sfiConfigPath
$connStr = $sfiXml.configuration.connectionStrings.add |
    Where-Object { $_.name -eq "SFICTDataAccess" } |
    Select-Object -ExpandProperty connectionString
$connStr = ($connStr -replace "\s+", " ").Trim()

$markerApplied   = Read-AppSetting $minDayConfigPath "MINDAY_APPLIED_USER"
$markerNoUpdate  = Read-AppSetting $minDayConfigPath "MINDAY_NO_UPDATE_NEEDED_USER"
$markerException = Read-AppSetting $minDayConfigPath "MINDAY_EXCEPTION_USER"
$markerError     = Read-AppSetting $minDayConfigPath "MINDAY_ERROR_USER"

$markerNames = @{
    $markerApplied   = "Applied"
    $markerNoUpdate  = "No Update Needed"
    $markerException = "Exception"
    $markerError     = "Error"
}

Write-Output "Connection: $connStr"
Write-Output "Markers: Applied=$markerApplied  NoUpdate=$markerNoUpdate  Exception=$markerException  Error=$markerError"
Write-Output ""

$conn = New-Object System.Data.OleDb.OleDbConnection($connStr)
$conn.Open()

$fromDate = "${YearMonth}01"
# crude next-month bound good enough for a YYYYMM search window
$y = [int]$YearMonth.Substring(0,4); $m = [int]$YearMonth.Substring(4,2)
if ($m -eq 12) { $toDate = "{0}0101" -f ($y+1) } else { $toDate = "{0}{1:D2}01" -f $y, ($m+1) }

function Run-Example($label, $sql, $params) {
    Write-Output "=== $label ==="
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = $sql
    foreach ($p in $params) { $cmd.Parameters.AddWithValue($p.Name, $p.Value) | Out-Null }
    $reader = $cmd.ExecuteReader()
    if ($reader.Read()) {
        $prgNo = "$($reader['PrgNo'])".Trim()
        $prgDate = "$($reader['Prgid_Prgdate'])".Trim()
        $marker = "$($reader['Updateid_Updempno'])".Trim()
        $markerLabel = $markerNames[$marker]
        if (-not $markerLabel) { $markerLabel = "(unknown: $marker)" }
        Write-Output "  $prgNo / $prgDate  --  marker: $markerLabel"
    } else {
        Write-Output "  No example found in $YearMonth"
    }
    $reader.Close()
    Write-Output ""
}

# 1. Single-duty pairing -- Min Day not applicable at all
Run-Example "1. Single-duty pairing (Min Day doesn't apply)" @"
SELECT Prgid_Prgno_Base+Prgid_Prgno_Eqpt+Prgid_Prgno_3+Prgid_Prgno_4_6 AS PrgNo, Prgid_Prgdate, Updateid_Updempno
FROM PM
WHERE Cancel <> 'C' AND Numdp = 1 AND Prgid_Prgdate >= ? AND Prgid_Prgdate < ?
"@ @(
    [PSCustomObject]@{Name="from"; Value=$fromDate}
    [PSCustomObject]@{Name="to"; Value=$toDate}
)

# 2. Mixed pilot/FA crew -- always a PX exception since 9/1/2022
Run-Example "2. Mixed pilot/FA crew (Type='B')" @"
SELECT Prgid_Prgno_Base+Prgid_Prgno_Eqpt+Prgid_Prgno_3+Prgid_Prgno_4_6 AS PrgNo, Prgid_Prgdate, Updateid_Updempno
FROM PM
WHERE Cancel <> 'C' AND Type = 'B' AND Prgid_Prgdate >= ? AND Prgid_Prgdate < ?
"@ @(
    [PSCustomObject]@{Name="from"; Value=$fromDate}
    [PSCustomObject]@{Name="to"; Value=$toDate}
)

# 3. All crew excused (RAS/TTA) -- correctly bypassed, no top-up.
#    NOTE: avoid correlated NOT EXISTS/EXISTS subqueries here -- confirmed live
#    that the native PervasiveOLEDB provider segfaults (AccessViolationException)
#    on that shape. GROUP BY/HAVING aggregates are the safe equivalent, same
#    pattern already proven working elsewhere this session.
Run-Example "3. All crew RAS/TTA (correct bypass, marker=No Update Needed)" @"
SELECT pm.Prgid_Prgno_Base+pm.Prgid_Prgno_Eqpt+pm.Prgid_Prgno_3+pm.Prgid_Prgno_4_6 AS PrgNo, pm.Prgid_Prgdate, pm.Updateid_Updempno
FROM PM pm
INNER JOIN mp ON mp.Prgid_Prgno = pm.Prgid_Prgno_Base+pm.Prgid_Prgno_Eqpt+pm.Prgid_Prgno_3+pm.Prgid_Prgno_4_6
             AND mp.Prgid_Prgdate = pm.Prgid_Prgdate
WHERE pm.Cancel <> 'C' AND pm.Updateid_Updempno = ? AND mp.status = 'A' AND pm.Prgid_Prgdate >= ? AND pm.Prgid_Prgdate < ?
GROUP BY pm.Prgid_Prgno_Base, pm.Prgid_Prgno_Eqpt, pm.Prgid_Prgno_3, pm.Prgid_Prgno_4_6, pm.Prgid_Prgdate, pm.Updateid_Updempno
HAVING SUM(CASE WHEN mp.Assign_Code IN ('RAS','TTA') THEN 1 ELSE 0 END) > 0
   AND SUM(CASE WHEN mp.Assign_Code IS NULL OR mp.Assign_Code NOT IN ('RAS','TTA') THEN 1 ELSE 0 END) = 0
"@ @(
    [PSCustomObject]@{Name="marker"; Value=$markerNoUpdate}
    [PSCustomObject]@{Name="from"; Value=$fromDate}
    [PSCustomObject]@{Name="to"; Value=$toDate}
)

# 4. Mixed RAS/TTA + non-RAS crew -- PX exception
Run-Example "4. Mixed RAS/TTA + regular crew (marker=Exception)" @"
SELECT pm.Prgid_Prgno_Base+pm.Prgid_Prgno_Eqpt+pm.Prgid_Prgno_3+pm.Prgid_Prgno_4_6 AS PrgNo, pm.Prgid_Prgdate, pm.Updateid_Updempno
FROM PM pm
INNER JOIN mp ON mp.Prgid_Prgno = pm.Prgid_Prgno_Base+pm.Prgid_Prgno_Eqpt+pm.Prgid_Prgno_3+pm.Prgid_Prgno_4_6
             AND mp.Prgid_Prgdate = pm.Prgid_Prgdate
WHERE pm.Cancel <> 'C' AND pm.Updateid_Updempno = ? AND mp.status = 'A' AND pm.Prgid_Prgdate >= ? AND pm.Prgid_Prgdate < ?
GROUP BY pm.Prgid_Prgno_Base, pm.Prgid_Prgno_Eqpt, pm.Prgid_Prgno_3, pm.Prgid_Prgno_4_6, pm.Prgid_Prgdate, pm.Updateid_Updempno
HAVING SUM(CASE WHEN mp.Assign_Code IN ('RAS','TTA') THEN 1 ELSE 0 END) > 0
   AND SUM(CASE WHEN mp.Assign_Code IS NULL OR mp.Assign_Code NOT IN ('RAS','TTA') THEN 1 ELSE 0 END) > 0
"@ @(
    [PSCustomObject]@{Name="marker"; Value=$markerException}
    [PSCustomObject]@{Name="from"; Value=$fromDate}
    [PSCustomObject]@{Name="to"; Value=$toDate}
)

# 5. Mixed RAS/TTA crew + multi-day trip -- the Oct 27 bypass-bug scenario.
#    No marker filter here on purpose: whichever marker this comes back with
#    IS the finding -- Applied means the bug is still live at this site,
#    Exception means the fix is working.
Run-Example "5. Mixed RAS/TTA crew + multi-day trip (bypass-bug scenario -- check marker!)" @"
SELECT pm.Prgid_Prgno_Base+pm.Prgid_Prgno_Eqpt+pm.Prgid_Prgno_3+pm.Prgid_Prgno_4_6 AS PrgNo, pm.Prgid_Prgdate, pm.Updateid_Updempno
FROM PM pm
INNER JOIN mp ON mp.Prgid_Prgno = pm.Prgid_Prgno_Base+pm.Prgid_Prgno_Eqpt+pm.Prgid_Prgno_3+pm.Prgid_Prgno_4_6
             AND mp.Prgid_Prgdate = pm.Prgid_Prgdate
WHERE pm.Cancel <> 'C' AND pm.Numdp > 1 AND mp.status = 'A' AND pm.Prgid_Prgdate >= ? AND pm.Prgid_Prgdate < ?
GROUP BY pm.Prgid_Prgno_Base, pm.Prgid_Prgno_Eqpt, pm.Prgid_Prgno_3, pm.Prgid_Prgno_4_6, pm.Prgid_Prgdate, pm.Updateid_Updempno
HAVING SUM(CASE WHEN mp.Assign_Code IN ('RAS','TTA') THEN 1 ELSE 0 END) > 0
   AND SUM(CASE WHEN mp.Assign_Code IS NULL OR mp.Assign_Code NOT IN ('RAS','TTA') THEN 1 ELSE 0 END) > 0
"@ @(
    [PSCustomObject]@{Name="from"; Value=$fromDate}
    [PSCustomObject]@{Name="to"; Value=$toDate}
)

# 6. Plain floor top-up, no RAS involved at all
Run-Example "6. Plain floor top-up (marker=Applied, no RAS/TTA crew)" @"
SELECT pm.Prgid_Prgno_Base+pm.Prgid_Prgno_Eqpt+pm.Prgid_Prgno_3+pm.Prgid_Prgno_4_6 AS PrgNo, pm.Prgid_Prgdate, pm.Updateid_Updempno
FROM PM pm
LEFT JOIN mp ON mp.Prgid_Prgno = pm.Prgid_Prgno_Base+pm.Prgid_Prgno_Eqpt+pm.Prgid_Prgno_3+pm.Prgid_Prgno_4_6
            AND mp.Prgid_Prgdate = pm.Prgid_Prgdate AND mp.status = 'A' AND mp.Assign_Code IN ('RAS','TTA')
WHERE pm.Cancel <> 'C' AND pm.Updateid_Updempno = ? AND pm.Prgid_Prgdate >= ? AND pm.Prgid_Prgdate < ?
GROUP BY pm.Prgid_Prgno_Base, pm.Prgid_Prgno_Eqpt, pm.Prgid_Prgno_3, pm.Prgid_Prgno_4_6, pm.Prgid_Prgdate, pm.Updateid_Updempno
HAVING COUNT(mp.Mastid_Empno) = 0
"@ @(
    [PSCustomObject]@{Name="marker"; Value=$markerApplied}
    [PSCustomObject]@{Name="from"; Value=$fromDate}
    [PSCustomObject]@{Name="to"; Value=$toDate}
)

$conn.Close()
