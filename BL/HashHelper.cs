using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace BL
{
    public static class HashHelper
    {
        internal static string Sha256(string s)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(s ?? ""));
                var sb = new StringBuilder(bytes.Length * 2);
                foreach (var b in bytes)
                    sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        [Obsolete("No usar para persistir DVH. Usar VerificadorIntegridadService.CalcularDVHUsuario.")]
        public static string DvhUsuario(DataRow r)
        {
            string activo = (r["Activo"] is bool b && b) ? "1" : "0";
            string bloqueado = r["BloqueadoHasta"] == DBNull.Value ? "" : ((DateTime)r["BloqueadoHasta"]).ToUniversalTime().ToString("o");
            string ultLogin = r["UltLoginUtc"] == DBNull.Value ? "" : ((DateTime)r["UltLoginUtc"]).ToUniversalTime().ToString("o");
            string hashAlg = r["HashAlgVer"]?.ToString() ?? "";

            var baseStr = string.Join("|", new[]{
        r["Usuario_Id"]?.ToString() ?? "",
        r["Usuario_Email"]?.ToString() ?? "",
        r["Usuario_Nombre"]?.ToString() ?? "",
        activo,
        r["IntentosFallidos"]?.ToString() ?? "",
        bloqueado,
        ultLogin,
        hashAlg
    });
            return Sha256(baseStr);
        }

        public static string DvhUsuarioRol(DataRow r)
        {
            string usuarioId = r["Usuario_Id"]?.ToString() ?? "";
            string rolId = r["Rol_Id"]?.ToString() ?? "";

            var baseStr = string.Join("|", new[]{
        usuarioId,
        rolId
    });

            return Sha256(baseStr);
        }


        public static string DvhUsuarioPermiso(int usuarioId, int permisoId)
            => Sha256($"{usuarioId}|{permisoId}");

        public static string CalcularDVV(DataTable dt) // concatena DVH de cada fila
        {
            var sb = new StringBuilder();
            foreach (DataRow r in dt.Rows)
                sb.Append(r["DVH"]?.ToString() ?? "");
            return Sha256(sb.ToString());
        }

    }
}
