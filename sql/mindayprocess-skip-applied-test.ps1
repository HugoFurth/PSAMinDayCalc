# MinDayProcess "already applied" skip test
#
# Verifies the fix for the marker-flip bug: ProcessPairing must now bail out
# immediately (before any evaluation logic runs) when a pairing's current
# Updateid_Updempno already equals MINDAY_APPLIED_USER, rather than re-deriving
# a marker from current duty credit values and silently downgrading it back to
# MINDAY_NO_UPDATE_NEEDED_USER.
#
# Picks a real, already-applied pairing, calls the live MinDayProcess.ProcessPairing
# on it with EvalupateAndUpdate, and checks:
#   1. It returns false (Bypassed -- nothing to evaluate).
#   2. The "Min day already applied -- skipping" status message fires.
#   3. PM.Updateid_Updempno is unchanged afterward (still MINDAY_APPLIED_USER) --
#      proving no write happened, not just that the return value looked right.
#
# Must run through the 32-bit PowerShell host (DATPSADSN is a 32-bit-only
# Pervasive ODBC driver, and the app's own DLLs are x86-only):
#   C:\Windows\SysWOW64\WindowsPowerShell\v1.0\powershell.exe -File sql\mindayprocess-skip-applied-test.ps1

Add-Type -AssemblyName System.Data

function Get-ConfigValue([string]$key) {
    foreach ($configPath in @("D:\SFI\EXE\SFI.config", "Z:\SFI\EXE\SFI.config")) {
        if (Test-Path $configPath) {
            [xml]$xml = Get-Content $configPath
            $node = $xml.configuration.appSettings.add | Where-Object { $_.key -eq $key }
            if ($node) { return [uint32]$node.value.Trim() }
        }
    }
    throw "$key not found in SFI.config"
}

$markerApplied = Get-ConfigValue "MINDAY_APPLIED_USER"
Write-Output "Using MINDAY_APPLIED_USER = $markerApplied (from SFI.config)"

$conn = New-Object System.Data.Odbc.OdbcConnection("DSN=DATPSADSN")
$conn.Open()

# --- pick one real, already-applied pairing as test data ---
$pickCmd = $conn.CreateCommand()
$pickCmd.CommandText = "SELECT Prgid_Prgno_Base,Prgid_Prgno_Eqpt,Prgid_Prgno_3,Prgid_Prgno_4_6,Prgid_Prgdate FROM PM WHERE Updateid_Updempno = ?"
[void]$pickCmd.Parameters.AddWithValue("marker", [int]$markerApplied)
$pickReader = $pickCmd.ExecuteReader()
if (-not $pickReader.Read()) {
    throw "No pairings found with Updateid_Updempno = $markerApplied -- cannot pick test data."
}
$base = $pickReader["Prgid_Prgno_Base"].ToString()
$eqpt = $pickReader["Prgid_Prgno_Eqpt"].ToString()
$p3   = $pickReader["Prgid_Prgno_3"].ToString()
$p46  = $pickReader["Prgid_Prgno_4_6"].ToString()
$date = $pickReader["Prgid_Prgdate"].ToString()
$pickReader.Close()

$prgNo = "$base$eqpt$p3$p46"
Write-Output "Test pairing: $prgNo $date (currently marked $markerApplied)"

function Get-CurrentMarker {
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = "SELECT Updateid_Updempno FROM PM WHERE Prgid_Prgno_Base=? AND Prgid_Prgno_Eqpt=? AND Prgid_Prgno_3=? AND Prgid_Prgno_4_6=? AND Prgid_Prgdate=?"
    [void]$cmd.Parameters.AddWithValue("base", $base)
    [void]$cmd.Parameters.AddWithValue("eqpt", $eqpt)
    [void]$cmd.Parameters.AddWithValue("d3", $p3)
    [void]$cmd.Parameters.AddWithValue("d46", $p46)
    [void]$cmd.Parameters.AddWithValue("date", $date)
    $reader = $cmd.ExecuteReader()
    $reader.Read() | Out-Null
    $marker = [uint32]$reader["Updateid_Updempno"]
    $reader.Close()
    return $marker
}

$markerBefore = Get-CurrentMarker
Write-Output "PM.Updateid_Updempno before call: $markerBefore"

# --- exercise the actual compiled C# path ---
$binDir = "D:\Data\VS\PSAMinDayCalc\MinDayProcess\bin\Debug"
Add-Type -Path (Join-Path $binDir "SFICTDateTimeUtils.dll")
Add-Type -Path (Join-Path $binDir "SFIConfigUtils.dll")
Add-Type -Path (Join-Path $binDir "SFICTDataAccess.dll")
Add-Type -Path (Join-Path $binDir "MSEval.dll")
Add-Type -Path (Join-Path $binDir "MinDayProcess.dll")

# MinDayProcess() reads CTSecurityProfile.bin via a path relative to the
# process's current directory (not just PowerShell's $PWD) -- set both so the
# parameterless constructor finds it at D:\SFI\EXE\CTSecurityProfile.bin.
Set-Location "D:\SFI\EXE"
[Environment]::CurrentDirectory = "D:\SFI\EXE"

$mdp = New-Object MinDayProcessNS.MinDayProcess

# Register-ObjectEvent queues its action for later, asynchronous processing --
# it didn't fire before the marker/return-value checks below ran. Casting a
# scriptblock directly to the delegate type instead invokes it synchronously,
# in-line with the .NET event raise.
$script:statusMessages = New-Object System.Collections.Generic.List[string]
$handler = [MinDayProcessNS.MinDayStatusDelegate]{
    param($sender, $statusArgs)
    $script:statusMessages.Add("[" + $statusArgs.Status + "] " + $statusArgs.Message)
    }
$mdp.add_StatusUpdate($handler)

$evalupateAndUpdate = [MinDayProcessNS.PairingProcessAction]::EvalupateAndUpdate
$needsEval = $mdp.ProcessPairing($prgNo, $date, $evalupateAndUpdate)

Write-Output ""
Write-Output "ProcessPairing returned: $needsEval (expected: False)"
Write-Output "Status messages captured:"
$script:statusMessages | ForEach-Object { Write-Output "  $_" }

$markerAfter = Get-CurrentMarker
Write-Output ""
Write-Output "PM.Updateid_Updempno after call: $markerAfter"

$pass = $true
if ($needsEval -ne $false) {
    Write-Output "FAIL: expected ProcessPairing to return False (Bypassed), got $needsEval"
    $pass = $false
}
if (-not ($script:statusMessages -match "already applied")) {
    Write-Output "FAIL: expected a status message mentioning 'already applied'"
    $pass = $false
}
if ($markerAfter -ne $markerApplied) {
    Write-Output "FAIL: PM.Updateid_Updempno changed from $markerBefore to $markerAfter -- the skip did not prevent a write"
    $pass = $false
}

Write-Output ""
if ($pass) {
    Write-Output "PASS: already-applied pairing was skipped without re-deriving/overwriting its marker."
} else {
    Write-Output "FAIL: see above."
    exit 1
}

$conn.Close()
