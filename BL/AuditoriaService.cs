using BE;
using DAO;
using System;
using System.Collections.Generic;

namespace BL
{
    public class AuditoriaService
    {
        private static readonly Lazy<AuditoriaService> _inst = new Lazy<AuditoriaService>(() => new AuditoriaService());
        public static AuditoriaService Instancia => _inst.Value;

        private readonly AuditoriaRepository _auditoriaRepo = new AuditoriaRepository();

        private AuditoriaService() { }

        public List<AuditoriaEntry> FiltrarAuditoria(
            int? id = null,
            int? usuarioId = null,
            string evento = null,
            string texto = null,
            DateTime? desdeUtc = null,
            DateTime? hastaUtcExcl = null,
            int top = 500)
        {
            return _auditoriaRepo.FiltrarAuditoria(id, usuarioId, evento, texto, desdeUtc, hastaUtcExcl, top);
        }

        public void Registrar(string evento, int? usuarioId, string detalle)
        {
            _auditoriaRepo.Registrar(evento, usuarioId, detalle);
        }
    }
}
