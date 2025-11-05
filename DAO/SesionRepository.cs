using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BE;

namespace DAO
{
    public class SesionRepository : DAL
    {
        public void Abrir(Sesion s)
        {
            using (var cn = GetConnection())
            using (var cmd = new SqlCommand(@"
                INSERT INTO Sesion (Id, Usuario_Id, InicioUtc, Ip, UserAgent)
                VALUES (@id, @uid, @ini, @ip, @ua);", cn))
            {
                cmd.Parameters.AddWithValue("@id", s.Id);
                cmd.Parameters.AddWithValue("@uid", s.UsuarioId);
                cmd.Parameters.AddWithValue("@ini", s.InicioUtc);
                cmd.Parameters.AddWithValue("@ip", (object)s.Ip ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ua", (object)s.UserAgent ?? DBNull.Value);
                cn.Open();
                cmd.ExecuteNonQuery();
            }
        }

        public void Cerrar(Guid sesionId)
        {
            using (var cn = GetConnection())
            using (var cmd = new SqlCommand(@"
                UPDATE Sesion SET FinUtc = SYSUTCDATETIME() WHERE Id = @id;", cn))
            {
                cmd.Parameters.AddWithValue("@id", sesionId);
                cn.Open();
                cmd.ExecuteNonQuery();
            }
        }
        public void CerrarSesionesDeUsuario(int usuarioId)
        {
            using (var cn = GetConnection())
            using (var cmd = new SqlCommand(@"
        UPDATE Sesion
        SET FinUtc = SYSUTCDATETIME()
        WHERE Usuario_Id = @uid AND FinUtc IS NULL;", cn))
            {
                cmd.Parameters.AddWithValue("@uid", usuarioId);
                cn.Open();
                cmd.ExecuteNonQuery();
            }
        }

    }
}
