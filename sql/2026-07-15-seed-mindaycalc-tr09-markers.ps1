# One-time seed of the three MinDayCalc synthetic TR09 "system user"
# identities used as PM/MS examination markers. Run once against production
# before the MarkPMExamined/MarkMSExamined code ships. Safe to re-run -- it
# checks for existing rows first and skips them.
#
# Must run through the 32-bit PowerShell host (DATPSADSN is a 32-bit-only
# Pervasive ODBC driver):
#   C:\Windows\SysWOW64\WindowsPowerShell\v1.0\powershell.exe -File sql\2026-07-15-seed-mindaycalc-tr09-markers.ps1

Add-Type -AssemblyName System.Data

$markers = @(
    @{ EmpNo = 99901; Username = "MinDay - Updated" }
    @{ EmpNo = 99902; Username = "MinDay - No Update Needed" }
    @{ EmpNo = 99903; Username = "MinDay - Exception" }
)

$conn = New-Object System.Data.Odbc.OdbcConnection("DSN=DATPSADSN")
$conn.Open()

foreach ($m in $markers) {
    $checkCmd = $conn.CreateCommand()
    $checkCmd.CommandText = "SELECT COUNT(*) FROM TR09 WHERE T09Key_Number = 9 AND T09username = ?"
    [void]$checkCmd.Parameters.AddWithValue("username", $m.Username)
    $existing = $checkCmd.ExecuteScalar()
    if ($existing -gt 0) {
        Write-Output "SKIP: $($m.Username) already exists ($existing row(s))"
        continue
    }

    # Pack the employee number as a 10-byte key: 4-byte little-endian
    # unsigned int + 6 bytes of blank padding, matching the existing
    # encoding for every other TR09 row (confirmed empirically against
    # live data: e.g. empno 23560 -> bytes 08 5C 00 00 20 20 20 20 20 20).
    $empnoBytes = [BitConverter]::GetBytes([uint32]$m.EmpNo)
    [byte[]]$keyBytes = $empnoBytes + [byte[]](0x20,0x20,0x20,0x20,0x20,0x20)

    $insertCmd = $conn.CreateCommand()
    $insertCmd.CommandText = @'
INSERT INTO TR09
    (T09Key_Number, T09Key_Key, T09pswd, T09secLevel1, T09secLevel2,
     T09rptId, T09secCrewtype, T09username, T09usertype, T09RestrictCrewmsg,
     T09Active, T09GroupId)
VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
'@
    [void]$insertCmd.Parameters.AddWithValue("num", [int16]9)
    [void]$insertCmd.Parameters.AddWithValue("key", $keyBytes)
    [void]$insertCmd.Parameters.AddWithValue("pswd", "")
    [void]$insertCmd.Parameters.AddWithValue("sl1", [int16]0)
    [void]$insertCmd.Parameters.AddWithValue("sl2", "")
    [void]$insertCmd.Parameters.AddWithValue("rptid", "")
    [void]$insertCmd.Parameters.AddWithValue("crewtype", "B")
    [void]$insertCmd.Parameters.AddWithValue("username", $m.Username)
    [void]$insertCmd.Parameters.AddWithValue("usertype", "O")
    [void]$insertCmd.Parameters.AddWithValue("restrict", "")
    [void]$insertCmd.Parameters.AddWithValue("active", "N")
    [void]$insertCmd.Parameters.AddWithValue("groupid", "DIRECTORS")

    try {
        $rows = $insertCmd.ExecuteNonQuery()
        Write-Output "INSERTED: $($m.Username) (empno $($m.EmpNo)) - $rows row(s)"
    } catch {
        Write-Output "FAILED: $($m.Username) (empno $($m.EmpNo)) - $($_.Exception.Message)"
    }
}

$conn.Close()
