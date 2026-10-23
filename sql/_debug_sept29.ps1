Add-Type -AssemblyName System.Data
$conn = New-Object System.Data.Odbc.OdbcConnection("DSN=DATPSADSN")
$conn.Open()

$cmd = $conn.CreateCommand()
$cmd.CommandText = "SELECT COUNT(*) FROM PM WHERE Prgid_Prgdate LIKE '____0929'"
Write-Output ("Pairings starting 9/29 (any year, any end): " + $cmd.ExecuteScalar())

$cmd2 = $conn.CreateCommand()
$cmd2.CommandText = "SELECT Prgid_Prgno_Base+Prgid_Prgno_Eqpt+Prgid_Prgno_3+Prgid_Prgno_4_6 AS PrgNo, Prgid_Prgdate, Skdend_Date, Cancel FROM PM WHERE Prgid_Prgdate LIKE '____0929'"
$r = $cmd2.ExecuteReader()
$n = 0
while ($r.Read() -and $n -lt 20) {
    Write-Output ($r["PrgNo"].ToString().Trim() + " | " + $r["Prgid_Prgdate"] + " -> " + $r["Skdend_Date"] + " | Cancel=" + $r["Cancel"])
    $n++
}
$r.Close()
$conn.Close()
