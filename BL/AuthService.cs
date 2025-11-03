using BE;
using DAO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BL
{
    public class AuthService
    {
        private static readonly Lazy<AuthService> _inst = new Lazy<AuthService>(() => new AuthService());
        public static AuthService Instancia { get { return _inst.Value; } }

        private readonly UserLoginDAL _loginDal = new UserLoginDAL();
        private readonly UsuarioRepository _usuarios = new UsuarioRepository();
        private readonly SesionRepository _sesiones = new SesionRepository();
        private readonly AuditoriaRepository _aud = new AuditoriaRepository();
        private readonly CryptoManager _crypto = CryptoManager.Instancia;
        private readonly PermisoService _permisos = PermisoService.Instancia;

        private const int MAX_INTENTOS = 5;
        private const int BLOQUEO_MIN = 15;

        private AuthService() { }

        public bool Login(string email, string password, string ip = null, string userAgent = null)
        {
            // Traigo BE Usuario y Hash+Salt
            var u = _usuarios.GetByEmail(email);
            var hs = _loginDal.ObtenerHashYSalt(email);

            if (u == null || hs.HashHex == null)
            {
                _aud.Registrar("LOGIN_FAIL", null, $"Email={email} no existe");
                return false;
            }

            if (!u.Activo)
            {
                _aud.Registrar("LOGIN_BLOQUEADO", u.Id, "Usuario inactivo");
                return false;
            }

            if (u.BloqueadoHastaUtc.HasValue && u.BloqueadoHastaUtc.Value > DateTime.UtcNow)
            {
                _aud.Registrar("LOGIN_BLOQUEADO", u.Id, $"Bloqueado hasta {u.BloqueadoHastaUtc}");
                return false;
            }

            var hashIngresado = _crypto.HashPasswordPBKDF2(password, hs.Salt);
            if (!_crypto.ConstantTimeEquals(hs.HashHex, hashIngresado))
            {
                _usuarios.RegistrarIntentoFallido(email, MAX_INTENTOS, BLOQUEO_MIN);
                _aud.Registrar("LOGIN_FAIL", u.Id, "Hash no coincide");
                return false;
            }

            // OK → reseteo intentos y abro sesión
            _usuarios.RegistrarLoginOk(u.Id);

            // roles del usuario (para UI)
            u.Roles = _usuarios.GetRoles(u.Id);
            u.PermisosCompuestos = _permisos.ObtenerPermisosDeUsuario(u.Id);

            var s = new Sesion
            {
                Id = Guid.NewGuid(),
                UsuarioId = u.Id,
                InicioUtc = DateTime.UtcNow,
                Ip = ip,
                UserAgent = userAgent
            };
            _sesiones.Abrir(s);
            SessionManager.Instancia.SetCurrent(u, s);

            _aud.Registrar("LOGIN_OK", u.Id, $"Sesion={s.Id}");
            return true;
        }

        public void Logout()
        {
            var s = SessionManager.Instancia.SesionActual;
            var u = SessionManager.Instancia.UsuarioActual;

            if (s != null)
            {
                _sesiones.Cerrar(s.Id);
                _aud.Registrar("LOGOUT", u != null ? (int?)u.Id : null, $"Sesion={s.Id}");
            }
            SessionManager.Instancia.Clear();
        }
    }
}
