#nullable disable   // turning off null warnings so the code stays simple
using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;

namespace MinorProject2_GUI
{
    public class DataAccess
    {
        // connection string pointing to the local SQL Server
        private readonly string connectionString = "Server=localhost;Database=PremierLeagueDesktop;Trusted_Connection=True;TrustServerCertificate=True;";

        // these grab players / managers AND their club name, so the tables show a name instead of just a number
        private readonly string playerSelect = @"SELECT p.PlayerID, p.FirstName, p.LastName, p.Position, p.ClubID, c.ClubName
                                                 FROM Player p
                                                 LEFT JOIN Club c ON p.ClubID = c.ClubID";

        private readonly string managerSelect = @"SELECT m.ManagerID, m.FirstName, m.LastName, m.TacticalStyle, m.ClubID, c.ClubName
                                                  FROM Manager m
                                                  LEFT JOIN Club c ON m.ClubID = c.ClubID";

        // little helper so we don't repeat the same connection code over and over
        private DataTable RunSelect(string query, string keyword = null)
        {
            DataTable dt = new DataTable();
            using (SqlConnection conn = new SqlConnection(connectionString))
            using (SqlCommand cmd = new SqlCommand(query, conn))
            {
                if (keyword != null)
                {
                    cmd.Parameters.AddWithValue("@kw", "%" + keyword + "%");
                }
                conn.Open();
                using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                {
                    adapter.Fill(dt);
                }
            }
            return dt;
        }

        // 1. READ: get everything from any table
        public DataTable GetAllRecords(string tableName)
        {
            return RunSelect($"SELECT * FROM [{tableName}]");
        }

        // READ: all players (with club name)
        public DataTable GetPlayers()
        {
            return RunSelect(playerSelect + " ORDER BY p.PlayerID");
        }

        // READ: all managers (with club name)
        public DataTable GetManagers()
        {
            return RunSelect(managerSelect + " ORDER BY m.ManagerID");
        }

        // 2. SEARCH: looks in first name, last name, AND position
        public DataTable SearchPlayers(string keyword)
        {
            string query = playerSelect + @" WHERE p.FirstName LIKE @kw
                                                OR p.LastName LIKE @kw
                                                OR p.Position LIKE @kw
                                             ORDER BY p.PlayerID";
            return RunSelect(query, keyword);
        }

        // 3. CREATE: add a new record
        public void InsertRecord(string tableName, Dictionary<string, object> columnValues)
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                List<string> cols = new List<string>();
                List<string> paramNames = new List<string>();

                using (SqlCommand cmd = new SqlCommand())
                {
                    cmd.Connection = conn;
                    foreach (var pair in columnValues)
                    {
                        cols.Add($"[{pair.Key}]");
                        string pName = "@" + pair.Key;
                        paramNames.Add(pName);
                        cmd.Parameters.AddWithValue(pName, pair.Value ?? DBNull.Value);
                    }

                    cmd.CommandText = $"INSERT INTO [{tableName}] ({string.Join(", ", cols)}) VALUES ({string.Join(", ", paramNames)})";
                    conn.Open();
                    cmd.ExecuteNonQuery();
                }
            }
        }

        // 4. UPDATE (one column): change a single column on a record
        public void UpdateRecord(string tableName, string idColumn, int idValue, string columnToUpdate, object newValue)
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                string query = $"UPDATE [{tableName}] SET [{columnToUpdate}] = @newVal WHERE [{idColumn}] = @id";
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@newVal", newValue ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@id", idValue);
                    conn.Open();
                    cmd.ExecuteNonQuery();
                }
            }
        }

        // UPDATE (a bunch of columns): change lots of columns on a record in one go
        public void UpdateRecord(string tableName, string idColumn, int idValue, Dictionary<string, object> columnValues)
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                using (SqlCommand cmd = new SqlCommand())
                {
                    cmd.Connection = conn;

                    List<string> sets = new List<string>();
                    int i = 0;
                    foreach (var pair in columnValues)
                    {
                        string pName = "@p" + i;
                        sets.Add($"[{pair.Key}] = {pName}");
                        cmd.Parameters.AddWithValue(pName, pair.Value ?? DBNull.Value);
                        i++;
                    }
                    cmd.Parameters.AddWithValue("@id", idValue);

                    cmd.CommandText = $"UPDATE [{tableName}] SET {string.Join(", ", sets)} WHERE [{idColumn}] = @id";
                    conn.Open();
                    cmd.ExecuteNonQuery();
                }
            }
        }

        // 5. DELETE: remove a record by its id
        public void DeleteRecord(string tableName, string idColumn, int idValue)
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                string query = $"DELETE FROM [{tableName}] WHERE [{idColumn}] = @id";
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@id", idValue);
                    conn.Open();
                    cmd.ExecuteNonQuery();
                }
            }
        }
    }
}