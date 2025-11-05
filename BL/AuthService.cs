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

        private const int MAX_INTENTOS = 5;   // igual que antes
        private const int BLOQUEO_MIN = 15;   // igual que antes

        private AuthService() { }

        /// <summary>
        /// Login tradicional con PBKDF2 + lockout por intentos. 
        /// (Viejo + NUEVO: recalculo de DVH/DVV tras cambios volátiles en Usuario).
        /// </summary>
        public bool Login(string email, string password, string ip = null, string userAgent = null)
        {
            // Traigo BE Usuario y Hash+Salt (igual que antes)
            var u = _usuarios.GetByEmail(email);
            var hs = _loginDal.ObtenerHashYSalt(email);  // HashHex + Salt

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
                // === LÓGICA EXISTENTE ===
                _usuarios.RegistrarIntentoFallido(email, MAX_INTENTOS, BLOQUEO_MIN);
                _aud.Registrar("LOGIN_FAIL", u.Id, "Hash no coincide");

                // === NUEVO === (sin romper capas):
                // IntentosFallidos (y quizá BloqueadoHasta) cambiaron -> DVH de la fila y luego DVV de Usuario
                RecalcularDVHUsuarioFila(u.Id);
                VerificadorIntegridadService.Instancia.RecalcularDVV_Usuario();

                return false;
            }

            // === OK === → reseteo intentos, seteo UltLogin, etc. (tu repo ya lo hacía)
            _usuarios.RegistrarLoginOk(u.Id);  // mantiene tu flujo original :contentReference[oaicite:1]{index=1}

            // === NUEVO === DVH/DVV tras cambios de UltLogin/Intentos/Bloqueo
            RecalcularDVHUsuarioFila(u.Id);
            VerificadorIntegridadService.Instancia.RecalcularDVV_Usuario();

            // Cargar roles y permisos compuestos para la UI (igual que antes)
            u.Roles = _usuarios.GetRoles(u.Id);
            u.PermisosCompuestos = _permisos.ObtenerPermisosDeUsuario(u.Id);

            // Abrir sesión (igual que antes)
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

        /// <summary>
        /// Cierra la sesión actual (sin cambios, solo orden de limpieza/auditoría).
        /// </summary>
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

        // ============ Helpers internos (NUEVO) ============

        /// <summary>
        /// Recalcula el DVH de la fila Usuario indicada usando la misma fórmula que valida tu servicio
        /// y lo persiste a través de la DAO raw. No rompe 4 capas (BL orquesta, DAO solo persiste).
        /// </summary>
        private void RecalcularDVHUsuarioFila(int usuarioId)
        {
            var dvRepo = new DvRawRepository();
            var row = dvRepo.SelectUsuarioByIdRaw(usuarioId); // lee columnas crudas de Usuario
            var dvh = VerificadorIntegridadService.Instancia.CalcularDVHUsuario(row);
            dvRepo.UpdateUsuarioDVH(usuarioId, dvh);
        }
    }
}
