using BE;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAO
{
    public class UsuarioRepository : DAL
    {
        public Usuario GetByEmail(string email)
        {
            using (var cn = GetConnection())
            using (var cmd = new SqlCommand(@"
                SELECT 
                    u.Usuario_Id,
                    u.Usuario_Email,
                    u.Usuario_Nombre,
                    u.Activo,
                    u.IntentosFallidos,
                    u.BloqueadoHasta,
                    u.UltLoginUtc,
                    u.HashAlgVer,
                    u.DatosSensiblesEnc
                FROM Usuario u
                WHERE u.Usuario_Email = @email;", cn))
            {
                cmd.Parameters.AddWithValue("@email", email);
                cn.Open();
                using (var rd = cmd.ExecuteReader())
                {
                    if (!rd.Read()) return null;

                    var usr = new Usuario
                    {
                        Id = rd.GetInt32(0),
                        Email = rd.GetString(1),
                        Nombre = rd.GetString(2),
                        Activo = rd.GetBoolean(3),
                        IntentosFallidos = rd.GetInt32(4),
                        BloqueadoHastaUtc = rd.IsDBNull(5) ? (DateTime?)null : rd.GetDateTime(5),
                        UltLoginUtc = rd.IsDBNull(6) ? (DateTime?)null : rd.GetDateTime(6),
                        HashAlgVer = rd.IsDBNull(7) ? "PBKDF2-SHA256-v1" : rd.GetString(7),
                        DatosSensiblesEnc = rd.IsDBNull(8) ? null : (byte[])rd[8],
                        Roles = new List<Rol>(),
                        Permisos = new List<Permiso>()
                    };
                    return usr;
                }
            }
        }

        public List<Rol> GetRoles(int usuarioId)
        {
            var list = new List<Rol>();
            using (var cn = GetConnection())
            using (var cmd = new SqlCommand(@"
                SELECT r.Rol_Id, r.Rol_Nombre
                FROM UsuarioRol ur
                JOIN Rol r ON r.Rol_Id = ur.Rol_Id
                WHERE ur.Usuario_Id = @uid;", cn))
            {
                cmd.Parameters.AddWithValue("@uid", usuarioId);
                cn.Open();
                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                        list.Add(new Rol { Id = rd.GetInt32(0), Nombre = rd.GetString(1) });
                }
            }
            return list;
        }

        public void RegistrarLoginOk(int usuarioId)
        {
            using (var cn = GetConnection())
            using (var cmd = new SqlCommand(@"
                UPDATE Usuario
                SET IntentosFallidos = 0,
                    BloqueadoHasta = NULL,
                    UltLoginUtc = SYSUTCDATETIME()
                WHERE Usuario_Id = @id;", cn))
            {
                cmd.Parameters.AddWithValue("@id", usuarioId);
                cn.Open();
                cmd.ExecuteNonQuery();
            }

        }

        public void RegistrarIntentoFallido(string email, int maxIntentos, int minutosBloqueo)
        {
            using (var cn = GetConnection())
            using (var cmd = new SqlCommand(@"
                UPDATE Usuario
                SET IntentosFallidos = IntentosFallidos + 1,
                    BloqueadoHasta = CASE 
                        WHEN IntentosFallidos + 1 >= @max THEN DATEADD(MINUTE, @mins, SYSUTCDATETIME())
                        ELSE BloqueadoHasta END
                WHERE Usuario_Email = @em;", cn))
            {
                cmd.Parameters.AddWithValue("@em", email);
                cmd.Parameters.AddWithValue("@max", maxIntentos);
                cmd.Parameters.AddWithValue("@mins", minutosBloqueo);
                cn.Open();
                cmd.ExecuteNonQuery();
            }
        }

        public Usuario GetById(int id)
        {
            using (var cn = GetConnection())
            using (var cmd = new SqlCommand(@"
        SELECT 
            u.Usuario_Id,
            u.Usuario_Email,
            u.Usuario_Nombre,
            u.Activo,
            u.IntentosFallidos,
            u.BloqueadoHasta,
            u.UltLoginUtc,
            u.HashAlgVer,
            u.DatosSensiblesEnc
        FROM Usuario u
        WHERE u.Usuario_Id = @id;", cn))
            {
                cmd.Parameters.AddWithValue("@id", id);
                cn.Open();
                using (var rd = cmd.ExecuteReader())
                {
                    if (!rd.Read()) return null;

                    return new Usuario
                    {
                        Id = rd.GetInt32(0),
                        Email = rd.GetString(1),
                        Nombre = rd.GetString(2),
                        Activo = rd.GetBoolean(3),
                        IntentosFallidos = rd.GetInt32(4),
                        BloqueadoHastaUtc = rd.IsDBNull(5) ? (DateTime?)null : rd.GetDateTime(5),
                        UltLoginUtc = rd.IsDBNull(6) ? (DateTime?)null : rd.GetDateTime(6),
                        HashAlgVer = rd.IsDBNull(7) ? "PBKDF2-SHA256-v1" : rd.GetString(7),
                        DatosSensiblesEnc = rd.IsDBNull(8) ? null : (byte[])rd[8]
                    };
                }
            }
        }
        public void ActualizarUsuario(int id, string nuevoNombre, bool nuevoActivo)
        {
            using (var cn = GetConnection())
            using (var cmd = new SqlCommand(@"
        UPDATE Usuario
        SET Usuario_Nombre = @nom,
            Activo = @act
        WHERE Usuario_Id = @id;", cn))
            {
                cmd.Parameters.AddWithValue("@id", id);
                cmd.Parameters.AddWithValue("@nom", nuevoNombre);
                cmd.Parameters.AddWithValue("@act", nuevoActivo);
                cn.Open();
                cmd.ExecuteNonQuery();
            }
        }

        public void EliminarUsuario(int usuarioId)
        {
            using (var cn = GetConnection())
            {
                cn.Open();
                using (var tx = cn.BeginTransaction())
                {
                    try
                    {
                        new SqlCommand("DELETE FROM UsuarioPermiso WHERE Usuario_Id = @id;", cn, tx)
                        { Parameters = { new SqlParameter("@id", usuarioId) } }
                            .ExecuteNonQuery();

                        new SqlCommand("DELETE FROM UsuarioRol WHERE Usuario_Id = @id;", cn, tx)
                        { Parameters = { new SqlParameter("@id", usuarioId) } }
                            .ExecuteNonQuery();

                        new SqlCommand("DELETE FROM Usuario WHERE Usuario_Id = @id;", cn, tx)
                        { Parameters = { new SqlParameter("@id", usuarioId) } }
                            .ExecuteNonQuery();

                        tx.Commit();
                    }
                    catch
                    {
                        tx.Rollback();
                        throw;
                    }
                }
            }
        }

    }
}
