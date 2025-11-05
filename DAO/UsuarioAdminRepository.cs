using BE;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace DAO
{
    public class UsuarioAdminRepository : DAL
    {
        public List<Rol> ListarRoles()
        {
            var list = new List<Rol>();
            using (var cn = GetConnection())
            using (var cmd = new SqlCommand(
                "SELECT Rol_Id, Rol_Nombre FROM Rol ORDER BY Rol_Nombre;", cn))
            {
                cn.Open();
                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                        list.Add(new Rol { Id = rd.GetInt32(0), Nombre = rd.GetString(1) });
                }
            }
            return list;
        }
        public bool EmailExiste(string email)
        {
            using (var cn = GetConnection())
            using (var cmd = new SqlCommand("SELECT 1 FROM Usuario WHERE Usuario_Email = @em;", cn))
            {
                cmd.Parameters.AddWithValue("@em", email);
                cn.Open();
                return cmd.ExecuteScalar() != null;
            }
        }
        // DAO
        public void ActualizarUsuarioActivo(int id, bool activo)
        {
            using (var cn = GetConnection())
            using (var cmd = new SqlCommand("UPDATE Usuario SET Activo=@a WHERE Usuario_Id=@id", cn))
            {
                cmd.Parameters.AddWithValue("@a", activo);
                cmd.Parameters.AddWithValue("@id", id);
                cn.Open();
                cmd.ExecuteNonQuery();
            }
        }


        public int InsertUsuario(string email, string nombre, string hashHex, byte[] salt, bool activo, IEnumerable<int> rolesIds)
        {
            using (var cn = GetConnection())
            {
                cn.Open();
                using (var tx = cn.BeginTransaction())
                {
                    try
                    {
                        int newId;
                        using (var cmd = new SqlCommand(@"
                            INSERT INTO Usuario
                                (Usuario_Email, Usuario_Nombre, Usuario_Contraseña, Usuario_Salt, HashAlgVer, Activo, IntentosFallidos)
                            VALUES
                                (@em, @nom, @hash, @salt, 'PBKDF2-SHA256-v1', @act, 0);
                            SELECT CAST(SCOPE_IDENTITY() AS INT);", cn, tx))
                        {
                            cmd.Parameters.AddWithValue("@em", email);
                            cmd.Parameters.AddWithValue("@nom", nombre);
                            cmd.Parameters.AddWithValue("@hash", hashHex);
                            cmd.Parameters.AddWithValue("@salt", salt);
                            cmd.Parameters.AddWithValue("@act", activo);
                            newId = (int)cmd.ExecuteScalar();
                        }

                        if (rolesIds != null)
                        {
                            foreach (var rid in rolesIds)
                            {
                                using (var cmd = new SqlCommand(
                                    "INSERT INTO UsuarioRol (Usuario_Id, Rol_Id , DVH ) VALUES (@uid, @rid , @DVH);", cn, tx))
                                {
                                    var dvh = HashHelperDAL.Sha256($"{newId}|{rid}"); 

                                        cmd.Parameters.AddWithValue("@uid", newId);
                                        cmd.Parameters.AddWithValue("@DVH", dvh);
                                    cmd.Parameters.AddWithValue("@rid", rid);
                                    cmd.ExecuteNonQuery();
                                }
                            }
                        }



                        tx.Commit();
                        return newId;
                    }
                    catch
                    {
                        tx.Rollback();
                        throw;
                    }
                }
            }
        }

        public List<Usuario> ListarUsuarios()
        {
            var list = new List<Usuario>();

            using (var cn = GetConnection())
            using (var cmd = new SqlCommand(@"
        SELECT 
            u.Usuario_Id,
            u.Usuario_Email,
            u.Usuario_Nombre,
            u.Activo,
            STRING_AGG(r.Rol_Nombre, ', ') AS Roles
        FROM Usuario u
        LEFT JOIN UsuarioRol ur ON ur.Usuario_Id = u.Usuario_Id
        LEFT JOIN Rol r ON r.Rol_Id = ur.Rol_Id
        GROUP BY u.Usuario_Id, u.Usuario_Email, u.Usuario_Nombre, u.Activo
        ORDER BY u.Usuario_Email;", cn))
            {
                cn.Open();
                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                    {
                        var usuario = new Usuario
                        {
                            Id = rd.GetInt32(0),
                            Email = rd.GetString(1),
                            Nombre = rd.GetString(2),
                            Activo = rd.GetBoolean(3)
                        };

                        // cargamos el string de roles en una lista
                        string rolesStr = rd.IsDBNull(4) ? "" : rd.GetString(4);
                        usuario.Roles = rolesStr
                            .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(r => new Rol { Nombre = r.Trim() })
                            .ToList();

                        list.Add(usuario);
                    }
                }
            }

            return list;
        }
        public void UpdateUsuario(int id, string email, string nombre, bool activo)
        {
            using (var cn = GetConnection())
            using (var cmd = new SqlCommand(@"
                UPDATE Usuario
                SET Usuario_Email = @em,
                    Usuario_Nombre = @nom,
                    Activo = @act
                WHERE Usuario_Id = @id;", cn))
            {
                cmd.Parameters.AddWithValue("@id", id);
                cmd.Parameters.AddWithValue("@em", email);
                cmd.Parameters.AddWithValue("@nom", nombre);
                cmd.Parameters.AddWithValue("@act", activo);
                cn.Open();
                cmd.ExecuteNonQuery();
            }
        }

        // NUEVO: cambiar password
        public void UpdateUsuarioPassword(int id, string hashHex, byte[] salt)
        {
            using (var cn = GetConnection())
            using (var cmd = new SqlCommand(@"
                UPDATE Usuario
                SET Usuario_Contraseña = @hash,
                    Usuario_Salt       = @salt,
                    HashAlgVer         = 'PBKDF2-SHA256-v1'
                WHERE Usuario_Id = @id;", cn))
            {
                cmd.Parameters.AddWithValue("@id", id);
                cmd.Parameters.AddWithValue("@hash", hashHex);
                cmd.Parameters.AddWithValue("@salt", salt);
                cn.Open();
                cmd.ExecuteNonQuery();
            }
        }

        // NUEVO: reemplazar roles
        public void ReemplazarRolesUsuario(int usuarioId, IEnumerable<int> nuevosRoles)
        {
            using (var cn = GetConnection())
            {
                cn.Open();

                // borra roles actuales
                using (var cmdDel = new SqlCommand("DELETE FROM UsuarioRol WHERE Usuario_Id = @uid", cn))
                {
                    cmdDel.Parameters.AddWithValue("@uid", usuarioId);
                    cmdDel.ExecuteNonQuery();
                }

                // agrega nuevos roles
                foreach (var rolId in nuevosRoles)
                {
                    using (var cmdIns = new SqlCommand(
                        "INSERT INTO UsuarioRol (Usuario_Id, Rol_Id, DVH) VALUES (@uid, @rid, @dvh)", cn))
                    {
                        cmdIns.Parameters.AddWithValue("@uid", usuarioId);
                        cmdIns.Parameters.AddWithValue("@rid", rolId);
                        var dvh = HashHelperDAL.Sha256($"{usuarioId}|{rolId}");
                        cmdIns.Parameters.AddWithValue("@dvh", dvh);
                        cmdIns.ExecuteNonQuery();
                    }
                }
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
