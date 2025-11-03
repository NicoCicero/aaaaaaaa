using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BE;

namespace DAO
{
    public class PermisoDAO : DAL
    {
        /// <summary>
        /// Recupera todos los registros de la tabla Permiso.
        /// </summary>
        public List<PermisoComposite> GetAllPermisos()
        {
            var permisos = new List<PermisoComposite>();
            using (var cn = GetConnection())
            using (var cmd = new SqlCommand(
                "SELECT Permiso_Id, Permiso_Nombre, EsCompuesto FROM Permiso ORDER BY Permiso_Nombre;",
                cn))
            {
                cn.Open();
                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                    {
                        permisos.Add(new PermisoComposite
                        {
                            Id = rd.GetInt32(0),
                            Nombre = rd.GetString(1),
                            EsCompuesto = rd.GetBoolean(2)
                        });
                    }
                }
            }

            return permisos;
        }

        /// <summary>
        /// Recupera las relaciones padre-hijo del árbol de permisos.
        /// </summary>
        public List<(int PadreId, int HijoId)> GetRelaciones()
        {
            var relaciones = new List<(int PadreId, int HijoId)>();
            using (var cn = GetConnection())
            using (var cmd = new SqlCommand(
                "SELECT PermisoPadre_Id, PermisoHijo_Id FROM PermisoHijo;",
                cn))
            {
                cn.Open();
                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                    {
                        relaciones.Add((rd.GetInt32(0), rd.GetInt32(1)));
                    }
                }
            }

            return relaciones;
        }

        /// <summary>
        /// Permisos raíz asociados a un rol específico.
        /// </summary>
        public List<int> GetPermisosDeRol(int rolId)
        {
            var permisos = new List<int>();
            using (var cn = GetConnection())
            using (var cmd = new SqlCommand(
                "SELECT Permiso_Id FROM RolPermiso WHERE Rol_Id = @rolId;",
                cn))
            {
                cmd.Parameters.AddWithValue("@rolId", rolId);
                cn.Open();
                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                    {
                        permisos.Add(rd.GetInt32(0));
                    }
                }
            }

            return permisos;
        }

        /// <summary>
        /// Inserta un registro en RolPermiso.
        /// </summary>
        public void AsignarPermisoARol(int rolId, int permisoId)
        {
            using (var cn = GetConnection())
            using (var cmd = new SqlCommand(
                "IF NOT EXISTS (SELECT 1 FROM RolPermiso WHERE Rol_Id = @rol AND Permiso_Id = @permiso) " +
                "INSERT INTO RolPermiso (Rol_Id, Permiso_Id) VALUES (@rol, @permiso);",
                cn))
            {
                cmd.Parameters.AddWithValue("@rol", rolId);
                cmd.Parameters.AddWithValue("@permiso", permisoId);
                cn.Open();
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Elimina un registro de RolPermiso.
        /// </summary>
        public void QuitarPermisoARol(int rolId, int permisoId)
        {
            using (var cn = GetConnection())
            using (var cmd = new SqlCommand(
                "DELETE FROM RolPermiso WHERE Rol_Id = @rol AND Permiso_Id = @permiso;",
                cn))
            {
                cmd.Parameters.AddWithValue("@rol", rolId);
                cmd.Parameters.AddWithValue("@permiso", permisoId);
                cn.Open();
                cmd.ExecuteNonQuery();
            }
        }
    }
}

