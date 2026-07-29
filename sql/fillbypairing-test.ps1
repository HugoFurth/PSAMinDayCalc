# FillByPairing Query Test
#
# Verifies the new PMByTimestamp.FillByPairing TableAdapter query added to
# CTDataSet.xsd (D:\data\vs\CTDataAccess\CTDataSet.xsd) for the on-demand
# single-pairing min-day path (see docs\mindaycalc-single-pairing-implementation-outline.md).
#
# Since the query can't be exercised through the generated TableAdapter without
# rebuilding the CTDataAccess project in Visual Studio, this test runs the same
# SQL directly via ODBC and checks it two ways:
#   1. Cross-check: the new split-key WHERE clause (Prgid_Prgno_Base/_Eqpt/_3/_4_6)
#      must return exactly the same rows as an independently-phrased query that
#      filters on the concatenated Prgid_PrgNo instead -- two different ways of
#      saying "this one pairing" should agree row-for-row.
#   2. Sanity check: the query's computed PilotCount/FACount must match a manual
#      computation from AH.postype_0..7 and MP.position_0..7 for the same pairing.
#
# Test pairing is pulled from PM WHERE Updateid_Updempno = <the "Updated" marker>,
# i.e. one of the pairings sql\credit-pay-discrepancy-test.ps1 already validated
# has real MinDayProcess-touched data.
#
# Must run through the 32-bit PowerShell host (DATPSADSN is a 32-bit-only
# Pervasive ODBC driver):
#   C:\Windows\SysWOW64\WindowsPowerShell\v1.0\powershell.exe -File sql\fillbypairing-test.ps1

Add-Type -AssemblyName System.Data

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

# --- pick one real, already-processed pairing as test data ---
$pickCmd = $conn.CreateCommand()
$pickCmd.CommandText = "SELECT Prgid_Prgno_Base,Prgid_Prgno_Eqpt,Prgid_Prgno_3,Prgid_Prgno_4_6,Prgid_Prgdate FROM PM WHERE Updateid_Updempno = ?"
[void]$pickCmd.Parameters.AddWithValue("marker", [int]$markerUpdated)
$pickReader = $pickCmd.ExecuteReader()
if (-not $pickReader.Read()) {
    throw "No pairings found with Updateid_Updempno = $markerUpdated -- cannot pick test data."
}
$base = $pickReader["Prgid_Prgno_Base"].ToString()
$eqpt = $pickReader["Prgid_Prgno_Eqpt"].ToString()
$p3   = $pickReader["Prgid_Prgno_3"].ToString()
$p46  = $pickReader["Prgid_Prgno_4_6"].ToString()
$date = $pickReader["Prgid_Prgdate"].ToString()
$pickReader.Close()

$prgNo = "$base$eqpt$p3$p46"
Write-Output "Test pairing: $prgNo $date"

# --- query 1: new FillByPairing SQL, filtered by split key columns ---
$splitSql = @"
SELECT mp.Mastid_Empno, mp.Mastid_Bidate, mp.Assign_Code,  ab.Code AbCode, Prgid_Prgno_Base+ Prgid_Prgno_Eqpt+Prgid_Prgno_3+ Prgid_Prgno_4_6 as Prgid_PrgNo,
pm.Prgid_Prgdate, pm.Actend_Date, pm.Updateid_Updempno, pm.Updateid_Upddate,pm.Updateid_Updtime,
        if(postype_0 = 'P',pm.position_0,0)+
	if(postype_1 = 'P',pm.position_1,0)+
	if(postype_2 = 'P',pm.position_2,0)+
	if(postype_3 = 'P',pm.position_3,0)+
	if(postype_4 = 'P',pm.position_4,0)+
	if(postype_5 = 'P',pm.position_5,0)+
	if(postype_6 = 'P',pm.position_6,0)+
	if(postype_7 = 'P',pm.position_7,0) as PilotCount,
	if(postype_0 = 'C',pm.position_0,0)+
	if(postype_1 = 'C',pm.position_1,0)+
	if(postype_2 = 'C',pm.position_2,0)+
	if(postype_3 = 'C',pm.position_3,0)+
	if(postype_4 = 'C',pm.position_4,0)+
	if(postype_5 = 'C',pm.position_5,0)+
	if(postype_6 = 'C',pm.position_6,0)+
	if(postype_7 = 'C',pm.position_7,0) as FACount
FROM PM
left join ah  on 1 = 1
 left join mp on
  prgid_prgno = mp.prgid_prgno and
  pm.prgid_prgdate    = mp.prgid_prgdate and
  mp.Status = 'A'
 left join ab on
ab.Mastid_Empno = mp.Mastid_Empno and
ab.Mastid_Bidate = ab.Mastid_Bidate and
(pm.Report_Date <= ab.Period_Todate and pm.Actend_Date >= ab.Period_Fromdate)
where
  pm.Cancel<> 'C'  and
  pm.Prgid_Prgno_Base = ? and
  pm.Prgid_Prgno_Eqpt = ? and
  pm.Prgid_Prgno_3 = ? and
  pm.Prgid_Prgno_4_6 = ? and
  pm.Prgid_Prgdate = ?
"@

$splitCmd = $conn.CreateCommand()
$splitCmd.CommandText = $splitSql
[void]$splitCmd.Parameters.AddWithValue("base", $base)
[void]$splitCmd.Parameters.AddWithValue("eqpt", $eqpt)
[void]$splitCmd.Parameters.AddWithValue("p3", $p3)
[void]$splitCmd.Parameters.AddWithValue("p46", $p46)
[void]$splitCmd.Parameters.AddWithValue("date", $date)
$splitReader = $splitCmd.ExecuteReader()
$splitRows = @()
while ($splitReader.Read()) {
    $row = [ordered]@{}
    for ($i = 0; $i -lt $splitReader.FieldCount; $i++) {
        $row[$splitReader.GetName($i)] = $splitReader[$i]
    }
    $splitRows += [PSCustomObject]$row
}
$splitReader.Close()

# --- query 2: independent cross-check, filtered by concatenated PrgNo instead ---
$concatSql = $splitSql -replace "(?s)where\s*\r?\n\s*pm\.Cancel<> 'C'.*", "where`n  pm.Cancel<> 'C'  and`n  Prgid_Prgno_Base+ Prgid_Prgno_Eqpt+Prgid_Prgno_3+ Prgid_Prgno_4_6 = ? and`n  pm.Prgid_Prgdate = ?"

$concatCmd = $conn.CreateCommand()
$concatCmd.CommandText = $concatSql
[void]$concatCmd.Parameters.AddWithValue("prgno", $prgNo)
[void]$concatCmd.Parameters.AddWithValue("date", $date)
$concatReader = $concatCmd.ExecuteReader()
$concatRows = @()
while ($concatReader.Read()) {
    $row = [ordered]@{}
    for ($i = 0; $i -lt $concatReader.FieldCount; $i++) {
        $row[$concatReader.GetName($i)] = $concatReader[$i]
    }
    $concatRows += [PSCustomObject]$row
}
$concatReader.Close()

# --- query 3: manual sanity check of PilotCount/FACount from AH + MP directly ---
$ahCmd = $conn.CreateCommand()
$ahCmd.CommandText = "SELECT Postype_0,Postype_1,Postype_2,Postype_3,Postype_4,Postype_5,Postype_6,Postype_7 FROM AH"
$ahReader = $ahCmd.ExecuteReader()
$postype = @()
if ($ahReader.Read()) {
    for ($i = 0; $i -le 7; $i++) { $postype += $ahReader[$i].ToString().Trim() }
}
$ahReader.Close()

# Position_0..7 (crew-count-by-slot) live on PM, the pairing header -- not MP,
# which is the per-crew-member assignment join table.
$pmCmd = $conn.CreateCommand()
$pmCmd.CommandText = "SELECT Position_0,Position_1,Position_2,Position_3,Position_4,Position_5,Position_6,Position_7 FROM PM WHERE Prgid_Prgno_Base=? AND Prgid_Prgno_Eqpt=? AND Prgid_Prgno_3=? AND Prgid_Prgno_4_6=? AND Prgid_Prgdate=?"
[void]$pmCmd.Parameters.AddWithValue("base", $base)
[void]$pmCmd.Parameters.AddWithValue("eqpt", $eqpt)
[void]$pmCmd.Parameters.AddWithValue("p3", $p3)
[void]$pmCmd.Parameters.AddWithValue("p46", $p46)
[void]$pmCmd.Parameters.AddWithValue("date", $date)
$pmReader = $pmCmd.ExecuteReader()
$expectedPilot = 0
$expectedFA = 0
if ($pmReader.Read()) {
    for ($i = 0; $i -le 7; $i++) {
        $pos = [int]$pmReader[$i]
        if ($postype[$i] -eq 'P') { $expectedPilot += $pos }
        if ($postype[$i] -eq 'C') { $expectedFA += $pos }
    }
}
$pmReader.Close()
$conn.Close()

# --- compare and report ---
$pass = $true

Write-Output "`nSplit-key query returned $($splitRows.Count) row(s); concat-key query returned $($concatRows.Count) row(s)."
if ($splitRows.Count -eq 0) {
    Write-Output "FAIL: split-key query returned no rows for a pairing known to exist in PM."
    $pass = $false
}
if ($splitRows.Count -ne $concatRows.Count) {
    Write-Output "FAIL: row count mismatch between split-key and concat-key queries."
    $pass = $false
}

for ($i = 0; $i -lt [Math]::Min($splitRows.Count, $concatRows.Count); $i++) {
    $s = $splitRows[$i]
    $c = $concatRows[$i]
    foreach ($col in $s.PSObject.Properties.Name) {
        if ("$($s.$col)" -ne "$($c.$col)") {
            Write-Output "FAIL: row $i column '$col' differs -- split='$($s.$col)' concat='$($c.$col)'"
            $pass = $false
        }
    }
}

if ($splitRows.Count -gt 0) {
    $actualPilot = $splitRows[0].PilotCount
    $actualFA = $splitRows[0].FACount
    Write-Output "PilotCount: query=$actualPilot expected(manual)=$expectedPilot"
    Write-Output "FACount:    query=$actualFA expected(manual)=$expectedFA"
    if ("$actualPilot" -ne "$expectedPilot" -or "$actualFA" -ne "$expectedFA") {
        Write-Output "FAIL: PilotCount/FACount from FillByPairing query does not match manual AH/MP computation."
        $pass = $false
    }
}

Write-Output ""
if ($pass) {
    Write-Output "PASS: FillByPairing query is consistent with the concat-key cross-check and manual PilotCount/FACount computation for $prgNo $date."
} else {
    Write-Output "FAIL: see above."
    exit 1
}
