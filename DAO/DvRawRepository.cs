using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAO
{
    public class DvRawRepository : DAL
    {
        public DataTable SelectTabla(string nombreTabla)
        {
            var dt = new DataTable();
            using (var cn = GetConnection())
            using (var da = new SqlDataAdapter($"SELECT * FROM {nombreTabla};", cn))
            {
                da.Fill(dt);
            }
            return dt;
        }

        public DataRow SelectUsuarioByIdRaw(int id)
        {
            var dt = new DataTable();
            using (var cn = GetConnection())
            using (var da = new SqlDataAdapter("SELECT * FROM Usuario WHERE Usuario_Id=@id;", cn))
            {
                da.SelectCommand.Parameters.AddWithValue("@id", id);
                da.Fill(dt);
            }
            return dt.Rows.Count > 0 ? dt.Rows[0] : null;
        }

        public void UpdateUsuarioDVH(int id, string dvh)
        {
            using (var cn = GetConnection())
            using (var cmd = new SqlCommand("UPDATE Usuario SET DVH=@dvh WHERE Usuario_Id=@id;", cn))
            {
                cmd.Parameters.AddWithValue("@dvh", dvh);
                cmd.Parameters.AddWithValue("@id", id);
                cn.Open();
                cmd.ExecuteNonQuery();
            }
        }

        public void UpdateUsuarioRolDVH(int usuarioId, int rolId, string dvh)
        {
            using (var cn = GetConnection())
            using (var cmd = new SqlCommand("UPDATE UsuarioRol SET DVH=@dvh WHERE Usuario_Id=@u AND Rol_Id=@r;", cn))
            {
                cmd.Parameters.AddWithValue("@dvh", (object)dvh ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@u", usuarioId);
                cmd.Parameters.AddWithValue("@r", rolId);
                cn.Open();
                cmd.ExecuteNonQuery();
            }
        }

        public void UpdateUsuarioPermisoDVH(int usuarioId, int permisoId, string dvh)
        {
            using (var cn = GetConnection())
            using (var cmd = new SqlCommand("UPDATE UsuarioPermiso SET DVH=@dvh WHERE Usuario_Id=@u AND Permiso_Id=@p;", cn))
            {
                cmd.Parameters.AddWithValue("@dvh", (object)dvh ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@u", usuarioId);
                cmd.Parameters.AddWithValue("@p", permisoId);
                cn.Open();
                cmd.ExecuteNonQuery();
            }
        }
    }
}
