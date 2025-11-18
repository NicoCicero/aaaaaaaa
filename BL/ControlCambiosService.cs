using BE;
using DAO;
using System;
using System.Collections.Generic;

namespace BL
{
    public class ControlCambiosService
    {
        private static readonly Lazy<ControlCambiosService> _inst = new Lazy<ControlCambiosService>(() => new ControlCambiosService());
        public static ControlCambiosService Instancia => _inst.Value;

        private readonly ControlCambiosRepository _repo = new ControlCambiosRepository();

        private ControlCambiosService() { }

        public List<ControlCambioEntry> FiltrarCambios(
            int? id = null,
            int? usuarioId = null,
            string entidad = null,
            int? entidadId = null,
            string campo = null,
            DateTime? desdeUtc = null,
            DateTime? hastaUtcExcl = null,
            int top = 500)
        {
            return _repo.FiltrarCambios(id, usuarioId, entidad, entidadId, campo, desdeUtc, hastaUtcExcl, top);
        }

        public void RegistrarCambio(
            string entidad,
            int entidadId,
            string accion,
            string campo,
            string valorAnterior,
            string valorNuevo,
            int? usuarioId)
        {
            _repo.RegistrarCambio(entidad, entidadId, accion, campo, valorAnterior, valorNuevo, usuarioId);
        }
    }
}
