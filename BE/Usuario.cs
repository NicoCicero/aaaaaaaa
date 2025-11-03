using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace BE
{
    public class Usuario
    {
        /// <summary>
        /// Entidad de dominio Usuario (sin lógica de DB/UI).
        /// </summary>
        /// public List<Rol> Roles { get; set; } = new List<Rol>();
        public List<Permiso> Permisos { get; set; } = new List<Permiso>(); // si usás permisos finos
        public List<Rol> Roles { get; set; } = new List<Rol>();

        /// <summary>
        /// Árbol de permisos asociado al usuario logueado.
        /// </summary>
        public PermisoComponent PermisosCompuestos { get; set; }


        public int Id { get; set; }

        /// <summary>Email único de login.</summary>
        public string Email { get; set; } = string.Empty;

        /// <summary>Nombre a mostrar.</summary>
        public string Nombre { get; set; } = string.Empty;

        /// <summary>Habilitado para iniciar sesión.</summary>
        public bool Activo { get; set; } = true;

        /// <summary>Intentos fallidos acumulados (para lockout).</summary>
        public int IntentosFallidos { get; set; } = 0;

        /// <summary>Si está bloqueado, hasta cuándo (UTC).</summary>
        public DateTime? BloqueadoHastaUtc { get; set; }

        /// <summary>Último login exitoso (UTC).</summary>
        public DateTime? UltLoginUtc { get; set; }

        /// <summary>Versión del algoritmo de hash usado en DB (para migraciones).</summary>
        public string HashAlgVer { get; set; } = "PBKDF2-SHA256-v1";

        /// <summary>
        /// Datos sensibles cifrados (ej.: DNI). Puede ser null en C# 7.3 aunque no usemos '?'.
        /// </summary>
        public byte[] DatosSensiblesEnc { get; set; }  // null permitido en C# 7.3

        /// <summary>
        /// Verifica si el usuario cuenta con un permiso determinado consultando el Composite cargado.
        /// </summary>
        public bool TienePermiso(string nombrePermiso)
        {
            if (PermisosCompuestos == null) return false;
            return PermisosCompuestos.TienePermiso(nombrePermiso);
        }

        public override string ToString() => $"{Nombre} <{Email}>";
    }
}

