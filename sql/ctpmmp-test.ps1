# CTPMMP.FillByPairing end-to-end test
#
# Exercises the actual compiled C# path (SFICTDataAccess.dll's CTPMMP class,
# calling PMByTimestampTableAdapter.FillByPairing, mapping via
# PMByTimestamp.FromRow) against the live database -- not just the raw SQL.
# Cross-checks the result against sql\fillbypairing-test.ps1's already-verified
# raw-SQL result for the same pairing.
#
# Must run through the 32-bit PowerShell host (matches the app's x86 build and
# the Pervasive OLE DB provider):
#   C:\Windows\SysWOW64\WindowsPowerShell\v1.0\powershell.exe -File sql\ctpmmp-test.ps1

$binDir = "D:\data\vs\CTDataAccess\bin\Debug"
Add-Type -Path (Join-Path $binDir "SFICTDateTimeUtils.dll")
Add-Type -Path (Join-Path $binDir "SFIConfigUtils.dll")
Add-Type -Path (Join-Path $binDir "SFICTDataAccess.dll")

# same test pairing sql\fillbypairing-test.ps1 already validated
$prgNo = "D7A71C"
$prgDate = "20251011"

$ctpmmp = New-Object SFICTDataAccess.CTPMMP
$count = $ctpmmp.FillByPairing($prgNo, $prgDate)

Write-Output "CTPMMP.FillByPairing($prgNo, $prgDate) returned $count row(s); List.Count = $($ctpmmp.List.Count)"

$pass = $true
if ($ctpmmp.List.Count -ne 1) {
    Write-Output "FAIL: expected 1 row, got $($ctpmmp.List.Count)"
    $pass = $false
    }
else {
    $row = $ctpmmp.List[0]
    Write-Output ("PairingID=" + $row.PairingID + " PairingDate=" + $row.PairingDate + " PilotCount=" + $row.PilotCount + " FACount=" + $row.FACount + " EmpNum=" + $row.EmpNum)

    # cross-check against the values sql\fillbypairing-test.ps1 already verified for this pairing
    if ($row.PairingID -ne $prgNo)      { Write-Output "FAIL: PairingID mismatch"; $pass = $false }
    if ($row.PairingDate -ne $prgDate)  { Write-Output "FAIL: PairingDate mismatch"; $pass = $false }
    if ($row.PilotCount -ne 1)          { Write-Output "FAIL: PilotCount expected 1, got $($row.PilotCount)"; $pass = $false }
    if ($row.FACount -ne 0)             { Write-Output "FAIL: FACount expected 0, got $($row.FACount)"; $pass = $false }
    }

Write-Output ""
if ($pass) {
    Write-Output "PASS: CTPMMP.FillByPairing (compiled C# path) matches the raw-SQL-verified result for $prgNo $prgDate."
    }
else {
    Write-Output "FAIL: see above."
    exit 1
    }
