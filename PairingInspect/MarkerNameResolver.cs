using System;
using System.Data.OleDb;
using SFICTDataAccess;
using SFIConfigUtils;

namespace PairingInspect
    {
    public static class MarkerNameResolver
        {
        public static string Resolve(CTDataAccesBase dataAccess, uint empno)
            {
            uint markerUpdated = uint.Parse(SFIConfig.AppSettingRaw("MinDayMarkerUpdated"));
            uint markerNoUpdateNeeded = uint.Parse(SFIConfig.AppSettingRaw("MinDayMarkerNoUpdateNeeded"));
            uint markerException = uint.Parse(SFIConfig.AppSettingRaw("MinDayMarkerException"));

            if (empno == markerUpdated) return "MinDay - Updated";
            if (empno == markerNoUpdateNeeded) return "MinDay - No Update Needed";
            if (empno == markerException) return "MinDay - Exception";

            using (OleDbCommand cmd = dataAccess.Connection.CreateCommand())
                {
                cmd.CommandText = "SELECT T09username FROM TR09 WHERE T09Key_Number = 9 AND T09Key_Key = ?";
                byte[] keyBytes = new byte[10];
                BitConverter.GetBytes(empno).CopyTo(keyBytes, 0);
                for (int i = 4; i < 10; i++) keyBytes[i] = 0x20;
                cmd.Parameters.AddWithValue("key", keyBytes);
                object result = cmd.ExecuteScalar();
                return result == null ? ("Employee " + empno) : result.ToString().Trim();
                }
            }
        }
    }
