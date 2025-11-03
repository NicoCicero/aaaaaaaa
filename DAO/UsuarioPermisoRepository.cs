using BE;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAO
{
    public class UsuarioPermisoRepository : DAL
    {
        public void AsignarPermisoAUsuario(int usuarioId, int permisoId)
        {
            using (var cn = GetConnection())
            using (var cmd = new SqlCommand(@"
                IF NOT EXISTS (SELECT 1 FROM UsuarioPermiso WHERE Usuario_Id = @uid AND Permiso_Id = @pid)
                    INSERT INTO UsuarioPermiso (Usuario_Id, Permiso_Id)
                    VALUES (@uid, @pid);", cn))
            {
                cmd.Parameters.AddWithValue("@uid", usuarioId);
                cmd.Parameters.AddWithValue("@pid", permisoId);
                cn.Open();
                cmd.ExecuteNonQuery();
            }
        }

        public void QuitarPermisoAUsuario(int usuarioId, int permisoId)
        {
            using (var cn = GetConnection())
            using (var cmd = new SqlCommand(
                "DELETE FROM UsuarioPermiso WHERE Usuario_Id = @uid AND Permiso_Id = @pid;", cn))
            {
                cmd.Parameters.AddWithValue("@uid", usuarioId);
                cmd.Parameters.AddWithValue("@pid", permisoId);
                cn.Open();
                cmd.ExecuteNonQuery();
            }
        }

        public List<int> GetPermisosDeUsuario(int usuarioId)
        {
            var list = new List<int>();
            using (var cn = GetConnection())
            using (var cmd = new SqlCommand(
                "SELECT Permiso_Id FROM UsuarioPermiso WHERE Usuario_Id = @uid;", cn))
            {
                cmd.Parameters.AddWithValue("@uid", usuarioId);
                cn.Open();
                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                        list.Add(rd.GetInt32(0));
                }
            }
            return list;
        }

        public List<PermisoComponent> GetPermisosDirectos(int usuarioId)
        {
            var lista = new List<PermisoComponent>();
            using (var cn = GetConnection())
            using (var cmd = new SqlCommand(@"
        SELECT p.Permiso_Id, p.Permiso_Nombre, p.EsCompuesto
        FROM UsuarioPermiso up
        JOIN Permiso p ON p.Permiso_Id = up.Permiso_Id
        WHERE up.Usuario_Id = @uid;", cn))
            {
                cmd.Parameters.AddWithValue("@uid", usuarioId);
                cn.Open();
                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                    {
                        if (rd.GetBoolean(2))
                            lista.Add(new BE.PermisoCompuesto(rd.GetInt32(0), rd.GetString(1)));
                        else
                            lista.Add(new BE.PermisoSimple(rd.GetInt32(0), rd.GetString(1)));
                    }
                }
            }
            return lista;
        }

        public void QuitarPermisoAUsuario(int usuarioId)
        {
            using (var cn = GetConnection())
            using (var cmd = new SqlCommand(
                "DELETE FROM UsuarioPermiso WHERE Usuario_Id = @uid;", cn))
            {
                cmd.Parameters.AddWithValue("@uid", usuarioId);
                cn.Open();
                cmd.ExecuteNonQuery();
            }
        }

    }
}
