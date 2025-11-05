using BE;
using DAO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BL
{
    public class UserAdminService
    {
        private static readonly Lazy<UserAdminService> _inst =
           new Lazy<UserAdminService>(() => new UserAdminService());
        public static UserAdminService Instancia => _inst.Value;

        private readonly UsuarioAdminRepository _repo = new UsuarioAdminRepository();
        private readonly UsuarioRepository _usrRepo = new UsuarioRepository(); // para GetById + GetRoles
        private readonly CryptoManager _crypto = CryptoManager.Instancia;
        private readonly ControlCambiosRepository _ccRepo = new ControlCambiosRepository();

        private UserAdminService() { }

        // =============== LISTADOS ===============

        public List<Usuario> ListarUsuarios()
        {
            if (!SessionManager.Instancia.TienePermiso("Usuario.Modificar"))
                throw new UnauthorizedAccessException("No contás con permiso para listar usuarios.");
            return _repo.ListarUsuarios();
        }

        public List<Rol> ListarRoles()
        {
            if (!SessionManager.Instancia.TienePermiso("Usuario.Modificar"))
                throw new UnauthorizedAccessException("No contás con permiso para consultar roles.");
            return _repo.ListarRoles();
        }

        public Usuario ObtenerUsuarioCompleto(int id)
        {
            if (!SessionManager.Instancia.TienePermiso("Usuario.Modificar"))
                throw new UnauthorizedAccessException();

            var u = _usrRepo.GetById(id);
            if (u == null) return null;
            u.Roles = _usrRepo.GetRoles(id);
            return u;
        }

        // =============== ALTA ===============

        public int CrearUsuario(string email, string nombre, string passwordPlano, bool activo, IEnumerable<int> rolesIds)
        {
            if (!SessionManager.Instancia.TienePermiso("Usuario.Alta"))
                throw new UnauthorizedAccessException("No contás con permiso para crear usuarios.");

            if (string.IsNullOrWhiteSpace(email)) throw new ArgumentException("Email requerido");
            if (string.IsNullOrWhiteSpace(nombre)) throw new ArgumentException("Nombre requerido");
            if (string.IsNullOrEmpty(passwordPlano) || passwordPlano.Length < 8)
                throw new ArgumentException("La contraseña debe tener al menos 8 caracteres");
            if (rolesIds == null || !rolesIds.Any())
                throw new ArgumentException("Seleccioná al menos un rol");

            if (_repo.EmailExiste(email))
                throw new InvalidOperationException("Ya existe un usuario con ese email.");

            var salt = _crypto.GenerateSalt(16);
            var hash = _crypto.HashPasswordPBKDF2(passwordPlano, salt);

            int nuevoId = _repo.InsertUsuario(email, nombre, hash, salt, activo, rolesIds);

            // auditoría de control de cambios (ALTA)
            int? usuarioActualId = SessionManager.Instancia.UsuarioActual?.Id;
            string resumenNuevo = $"Email={email}; Nombre={nombre}; Activo={activo}; Roles={string.Join(",", rolesIds)}";

            _ccRepo.RegistrarCambio(
                entidad: "Usuario",
                entidadId: nuevoId,
                accion: "Alta",
                campo: null,
                valorAnterior: null,
                valorNuevo: resumenNuevo,
                usuarioId: usuarioActualId
            );

            // DVH fila creada
            var raw = new DAO.DvRawRepository();
            var row = raw.SelectUsuarioByIdRaw(nuevoId);
            var dvh = VerificadorIntegridadService.Instancia.CalcularDVHUsuario(row);
            raw.UpdateUsuarioDVH(nuevoId, dvh);
            // DVV de la tabla
            VerificadorIntegridadService.Instancia.RecalcularDVV_Usuario();
            VerificadorIntegridadService.Instancia.RecalcularDVV_UsuarioRol();


            return nuevoId;

        }

        // =============== MODIFICACIÓN ===============

        public void ActualizarUsuario(
            int id,
            string email,
            string nombre,
            bool activo,
            IEnumerable<int> rolesIds,
            string passwordPlano // puede venir null o ""
        )
        {
            if (!SessionManager.Instancia.TienePermiso("Usuario.Modificar"))
                throw new UnauthorizedAccessException("No contás con permiso para modificar usuarios.");

            // 1. traigo el usuario original para comparar
            var original = _usrRepo.GetById(id);
            if (original == null)
                throw new Exception("Usuario no encontrado.");

            // 1.b traigo los roles originales
            var rolesOriginales = _usrRepo.GetRoles(id).Select(r => r.Id).ToList();
            var rolesNuevos = rolesIds?.ToList() ?? new List<int>();

            // 2. actualizo datos básicos en BD
            _repo.UpdateUsuario(id, email, nombre, activo);

            // 3. actualizo roles en BD
            _repo.ReemplazarRolesUsuario(id, rolesNuevos);

            VerificadorIntegridadService.Instancia.RecalcularDVV_UsuarioRol();


            // 4. contraseña opcional
            bool cambioContraseña = false;
            if (!string.IsNullOrEmpty(passwordPlano))
            {
                if (passwordPlano.Length < 8)
                    throw new ArgumentException("La contraseña debe tener al menos 8 caracteres");

                var salt = _crypto.GenerateSalt(16);
                var hash = _crypto.HashPasswordPBKDF2(passwordPlano, salt);
                _repo.UpdateUsuarioPassword(id, hash, salt);
                cambioContraseña = true;
            }

            // 5. registrar control de cambios CAMPO POR CAMPO
            int? usuarioActualId = SessionManager.Instancia.UsuarioActual?.Id;

            // Email
            RegistrarCambioSiDiferente(
                entidad: "Usuario",
                entidadId: id,
                campo: "Email",
                valorAnterior: original.Email,
                valorNuevo: email,
                usuarioId: usuarioActualId
            );

            // Nombre
            RegistrarCambioSiDiferente(
                entidad: "Usuario",
                entidadId: id,
                campo: "Nombre",
                valorAnterior: original.Nombre,
                valorNuevo: nombre,
                usuarioId: usuarioActualId
            );

            // Activo
            RegistrarCambioSiDiferente(
                entidad: "Usuario",
                entidadId: id,
                campo: "Activo",
                valorAnterior: original.Activo.ToString(),
                valorNuevo: activo.ToString(),
                usuarioId: usuarioActualId
            );

            // Roles (solo si realmente cambió la lista)
            bool rolesIguales =
                rolesOriginales.Count == rolesNuevos.Count &&
                !rolesOriginales.Except(rolesNuevos).Any();

            if (!rolesIguales)
            {
                string rolesViejosTxt = string.Join(",", rolesOriginales);
                string rolesNuevosTxt = string.Join(",", rolesNuevos);

                _ccRepo.RegistrarCambio(
                    entidad: "Usuario",
                    entidadId: id,
                    accion: "Modificacion",
                    campo: "Roles",
                    valorAnterior: rolesViejosTxt,
                    valorNuevo: rolesNuevosTxt,
                    usuarioId: usuarioActualId
                );
            }

            // Contraseña (si la cambiaron)
            if (cambioContraseña)
            {
                _ccRepo.RegistrarCambio(
                    entidad: "Usuario",
                    entidadId: id,
                    accion: "Modificacion",
                    campo: "Contraseña",
                    valorAnterior: null,            // por seguridad no guardamos hash viejo
                    valorNuevo: "*** actualizada ***",
                    usuarioId: usuarioActualId
                );
            }

            var raw = new DAO.DvRawRepository();
            var row = raw.SelectUsuarioByIdRaw(id);
            var dvh = VerificadorIntegridadService.Instancia.CalcularDVHUsuario(row);
            raw.UpdateUsuarioDVH(id, dvh);
            
            VerificadorIntegridadService.Instancia.RecalcularDVV_Usuario();

        }

        // =============== HELPERS ===============

        /// <summary>
        /// Registra un cambio en ControlCambios solo si valorAnterior != valorNuevo
        /// </summary>
        private void RegistrarCambioSiDiferente(
            string entidad,
            int entidadId,
            string campo,
            string valorAnterior,
            string valorNuevo,
            int? usuarioId)
        {
            // si ambos son null o iguales, no registramos nada
            if (string.Equals(valorAnterior, valorNuevo, StringComparison.Ordinal))
                return;

            _ccRepo.RegistrarCambio(
                entidad: entidad,
                entidadId: entidadId,
                accion: "Modificacion",
                campo: campo,
                valorAnterior: valorAnterior,
                valorNuevo: valorNuevo,
                usuarioId: usuarioId
            );
        }
        public void ActualizarRolesUsuario(int usuarioId, List<int> nuevosRoles)
        {
            if (!SessionManager.Instancia.TienePermiso("Usuario.Modificar"))
                throw new UnauthorizedAccessException("No contás con permiso para modificar roles de usuario.");

            

            // Registramos el cambio
            int? usuarioActualId = SessionManager.Instancia.UsuarioActual?.Id;
            _ccRepo.RegistrarCambio(
                entidad: "Usuario",
                entidadId: usuarioId,
                accion: "Modificacion",
                campo: "Roles",
                valorAnterior: null,
                valorNuevo: string.Join(",", nuevosRoles),
                usuarioId: usuarioActualId
            );

            _repo.ReemplazarRolesUsuario(usuarioId, nuevosRoles);

            // Setear DVH por cada fila actual de UsuarioRol del usuario
            var raw = new DAO.DvRawRepository();
            foreach (var rolId in nuevosRoles)
            {
                var dvh = BL.HashHelper.Sha256($"{usuarioId}|{rolId}");
                raw.UpdateUsuarioRolDVH(usuarioId, rolId, dvh);
            }

            // Luego DVV de la tabla
            BL.VerificadorIntegridadService.Instancia.RecalcularDVV_UsuarioRol();
        }

        public void EliminarUsuario(int usuarioId)
        {
            if (!SessionManager.Instancia.TienePermiso("Usuario.Baja"))
                throw new UnauthorizedAccessException("No contás con permiso para eliminar usuarios.");

            var actual = SessionManager.Instancia.UsuarioActual;
            if (actual != null && actual.Id == usuarioId)
                throw new InvalidOperationException("No podés eliminar tu propio usuario.");

            _repo.EliminarUsuario(usuarioId);

            _ccRepo.RegistrarCambio(
                entidad: "Usuario",
                entidadId: usuarioId,
                accion: "Baja",
                campo: null,
                valorAnterior: null,
                valorNuevo: null,
                usuarioId: actual?.Id
            );

            // Recalcular DVV de tablas afectadas
            VerificadorIntegridadService.Instancia.RecalcularDVV_Usuario();
            VerificadorIntegridadService.Instancia.RecalcularDVV_UsuarioRol();
            VerificadorIntegridadService.Instancia.RecalcularDVV_UsuarioPermiso();

        }
        // BL
        public void BajaLogicaUsuario(int usuarioId)
        {
            if (!SessionManager.Instancia.TienePermiso("Usuario.Baja"))
                throw new UnauthorizedAccessException();
            _repo.ActualizarUsuarioActivo(usuarioId, false);
            new SesionRepository().CerrarSesionesDeUsuario(usuarioId);
            // opcional: _repo.QuitarRolesPermisos(usuarioId);
            // auditoría de cambio
            _ccRepo.RegistrarCambio("Usuario", usuarioId, "Modificacion", "Activo", "True", "False", SessionManager.Instancia.UsuarioActual?.Id);

            // Recalcular DVH/DVV del usuario afectado
            var raw = new DAO.DvRawRepository();
            var row = raw.SelectUsuarioByIdRaw(usuarioId);
            if (row != null)
            {
                var dvh = VerificadorIntegridadService.Instancia.CalcularDVHUsuario(row);
                raw.UpdateUsuarioDVH(usuarioId, dvh);
            }
            VerificadorIntegridadService.Instancia.RecalcularDVV_Usuario();

        }


    }
}
