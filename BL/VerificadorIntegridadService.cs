using DAO;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BL
{
    public class VerificadorIntegridadService
    {
        private static readonly Lazy<VerificadorIntegridadService> _inst =
            new Lazy<VerificadorIntegridadService>(() => new VerificadorIntegridadService());
        public static VerificadorIntegridadService Instancia => _inst.Value;

        private readonly DvRawRepository _raw = new DvRawRepository();
        private readonly DvvRepository _dvvRepo = new DvvRepository();

        private VerificadorIntegridadService() { }


        public void RecalcularTodo()
        {
            var raw = new DAO.DvRawRepository();

            // Usuario
            var tU = raw.SelectTabla("Usuario");
            foreach (DataRow r in tU.Rows)
            {
                var dvh = CalcularDVHUsuario(r);
                raw.UpdateUsuarioDVH((int)r["Usuario_Id"], dvh);
            }
            RecalcularDVV_Usuario();

            // UsuarioRol
            var tUR = raw.SelectTabla("UsuarioRol");
            foreach (DataRow r in tUR.Rows)
            {
                var dvh = CalcularDVHUsuarioRol(r);
                raw.UpdateUsuarioRolDVH((int)r["Usuario_Id"], (int)r["Rol_Id"], dvh);
            }
            RecalcularDVV_UsuarioRol();

            // UsuarioPermiso
            var tUP = raw.SelectTabla("UsuarioPermiso");
            foreach (DataRow r in tUP.Rows)
            {
                var dvh = CalcularDVHUsuarioPermiso(r);
                raw.UpdateUsuarioPermisoDVH((int)r["Usuario_Id"], (int)r["Permiso_Id"], dvh);
            }
            RecalcularDVV_UsuarioPermiso();


        }



        // ==== DVH ====
        public string CalcularDVHUsuario(DataRow r)
        {
            var s = string.Join("|", new[]
            {
                r.Field<int>("Usuario_Id").ToString(),
                r["Usuario_Email"] as string ?? "",
                r["Usuario_Nombre"] as string ?? "",
                ((bool)r["Activo"]) ? "1" : "0",
                r["IntentosFallidos"].ToString(),
                r["BloqueadoHasta"] is DBNull ? "" : ((DateTime)r["BloqueadoHasta"]).ToUniversalTime().ToString("o"),
                r["UltLoginUtc"]   is DBNull ? "" : ((DateTime)r["UltLoginUtc"]).ToUniversalTime().ToString("o"),
                r["HashAlgVer"] as string ?? ""
            });
            return HashHelper.Sha256(s);
        }

        public string CalcularDVHUsuarioRol(DataRow r)
        {
            return HashHelper.Sha256($"{r.Field<int>("Usuario_Id")}|{r.Field<int>("Rol_Id")}");
        }

        public static string CalcularDVHUsuarioRol(int usuarioId , int rolId)
        {
            return HashHelper.Sha256($"{usuarioId}|{rolId}");
        }

        public string CalcularDVHUsuarioPermiso(DataRow r)
        {
            return HashHelper.Sha256($"{r.Field<int>("Usuario_Id")}|{r.Field<int>("Permiso_Id")}");
        }

        // ==== DVV por tabla ====
        public void RecalcularDVV_Usuario()
        {
            var t = _raw.SelectTabla("Usuario");
            string sum = "Usuario";
            foreach (DataRow r in t.Rows) sum += "|" + (r["DVH"] as string ?? "");
            _dvvRepo.Upsert("Usuario", HashHelper.Sha256(sum));
        }

        public void RecalcularDVV_UsuarioRol()
        {
            var t = _raw.SelectTabla("UsuarioRol");
            string sum = "UsuarioRol";
            foreach (DataRow r in t.Rows) sum += "|" + (r["DVH"] as string ?? "");
            _dvvRepo.Upsert("UsuarioRol", HashHelper.Sha256(sum));
        }

        public void RecalcularDVV_UsuarioPermiso()
        {
            var t = _raw.SelectTabla("UsuarioPermiso");
            string sum = "UsuarioPermiso";
            foreach (DataRow r in t.Rows) sum += "|" + (r["DVH"] as string ?? "");
            _dvvRepo.Upsert("UsuarioPermiso", HashHelper.Sha256(sum));
        }

        // ==== Validación global para el login/start ====
        public (bool ok, string detalle) ValidarTodo()
        {
            var problemas = new List<string>();

            // Usuario
            var tU = _raw.SelectTabla("Usuario");
            foreach (DataRow r in tU.Rows)
            {
                var calc = CalcularDVHUsuario(r);
                var db = r["DVH"] as string ?? "";
                if (!string.Equals(calc, db, StringComparison.OrdinalIgnoreCase))
                    problemas.Add($"Usuario_Id={r.Field<int>("Usuario_Id")} DVH inválido");
            }
            var sumU = "Usuario";
            foreach (DataRow r in tU.Rows)
            {
                sumU += "|" + (r["DVH"] as string ?? "");
            }

            string dvv = _dvvRepo.Get("Usuario");
            sumU = HashHelper.Sha256(sumU);
            if (sumU.CompareTo(dvv) != 0)
            {
                problemas.Add("DVV(Usuario) inválido");
            }

            // UsuarioRol
            var tUR = _raw.SelectTabla("UsuarioRol");

            foreach (DataRow r in tUR.Rows)
            {
                var calc = CalcularDVHUsuarioRol(r);
                var db = r["DVH"] as string ?? "";
                if (!string.Equals(calc, db, StringComparison.OrdinalIgnoreCase))
                    problemas.Add($"UsuarioRol (U={r.Field<int>("Usuario_Id")},R={r.Field<int>("Rol_Id")}) DVH inválido");
            }
            var sumUR = "UsuarioRol";
            foreach (DataRow r in tUR.Rows)
            {
                sumUR += "|" + (r["DVH"] as string ?? "");
            }

            sumUR = HashHelper.Sha256(sumUR);
            dvv = _dvvRepo.Get("UsuarioRol");

            if (sumUR.CompareTo(dvv) != 0)
                problemas.Add("DVV(UsuarioRol) inválido");

            // UsuarioPermiso
            var tUP = _raw.SelectTabla("UsuarioPermiso");
            foreach (DataRow r in tUP.Rows)
            {
                var calc = CalcularDVHUsuarioPermiso(r);
                var db = r["DVH"] as string ?? "";
                if (!string.Equals(calc, db, StringComparison.OrdinalIgnoreCase))
                    problemas.Add($"UsuarioPermiso (U={r.Field<int>("Usuario_Id")},P={r.Field<int>("Permiso_Id")}) DVH inválido");
            }
            var sumUP = "UsuarioPermiso";
            foreach (DataRow r in tUP.Rows)
            {
                sumUP += "|" + (r["DVH"] as string ?? "");
            }
            sumUP = HashHelper.Sha256(sumUP);
            dvv = _dvvRepo.Get("UsuarioPermiso");

            if (sumUP.CompareTo(dvv) != 0)
                problemas.Add("DVV(UsuarioPermiso) inválido");

            return (problemas.Count == 0, string.Join(Environment.NewLine, problemas));
        }
    }
}
