using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BE;

namespace BL
{
    public class SessionManager
    {
        private static readonly System.Lazy<SessionManager> _inst =
            new System.Lazy<SessionManager>(() => new SessionManager());
        public static SessionManager Instancia { get { return _inst.Value; } }

        private SessionManager() { }

        public Usuario UsuarioActual { get; private set; }
        public Sesion SesionActual { get; private set; }

        public void SetCurrent(Usuario usuario, Sesion sesion)
        {
            UsuarioActual = usuario;
            SesionActual = sesion;
        }

        public void Clear()
        {
            UsuarioActual = null;
            SesionActual = null;
        }

        /// <summary>
        /// Verifica si el usuario actual cuenta con un permiso específico.
        /// </summary>
        public bool TienePermiso(string nombrePermiso)
        {
            if (UsuarioActual == null) return false;
            return UsuarioActual.TienePermiso(nombrePermiso);
        }

        public bool TieneRol(string nombreRol)
        {
            if (UsuarioActual == null || UsuarioActual.Roles == null) return false;
            foreach (var r in UsuarioActual.Roles)
                if (r.Nombre != null && r.Nombre.Equals(nombreRol, System.StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }
    }
}
