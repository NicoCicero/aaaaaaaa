using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAO
{
    public class DvvRepository : DAL
    {
        public void Upsert(string tabla, string valor)
        {
            using (var cn = GetConnection())
            using (var cmd = new SqlCommand(@"
MERGE DVV AS T
USING (SELECT @t AS Tabla, @v AS Valor) AS S
ON T.Tabla = S.Tabla
WHEN MATCHED THEN UPDATE SET Valor = S.Valor
WHEN NOT MATCHED THEN INSERT (Tabla, Valor) VALUES (S.Tabla, S.Valor);", cn))
            {
                cmd.Parameters.AddWithValue("@t", tabla);
                cmd.Parameters.AddWithValue("@v", valor ?? "");
                cn.Open();
                cmd.ExecuteNonQuery();
            }
        }

        public string Get(string tabla)
        {
            using (var cn = GetConnection())
            using (var cmd = new SqlCommand("SELECT Valor FROM DVV WHERE Tabla=@t;", cn))
            {
                cmd.Parameters.AddWithValue("@t", tabla);
                cn.Open();
                var v = cmd.ExecuteScalar();
                return v == null || v is DBNull ? null : v.ToString();
            }
        }
    }
}
