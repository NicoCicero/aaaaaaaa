using DAO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BE;

namespace BL
{
    public class IdiomaService
    {
        private readonly IdiomaRepository _idiomaRepo = new IdiomaRepository();
        private readonly TraduccionRepository _tradRepo = new TraduccionRepository();

        public void SeleccionarIdioma(string codigo)
        {
            var traducciones = _tradRepo.ObtenerTraduccionesPorCodigo(codigo);
            var todasLasClaves = _tradRepo.ListarClaves();

            // fallback
            foreach (var clave in todasLasClaves)
                if (!traducciones.ContainsKey(clave))
                    traducciones[clave] = $"[{clave}]";

            // ✅ guardar el código actual antes o después de Notificar (da igual)
            IdiomaManager.Instancia.SetCodigoActual(codigo);

            // notificar a todos los forms suscriptos
            IdiomaManager.Instancia.Notificar(traducciones);
        }

        public void SeleccionarIdiomaPorDefecto()
        {
            var def = _idiomaRepo.ObtenerIdiomaPorDefecto();
            if (def.Codigo != null)
                SeleccionarIdioma(def.Codigo);
        }

        //  ESTA es la que usa el ComboBox
        public List<Idioma> ListarIdiomas()
        {
            var raws = _idiomaRepo.ListarIdiomasRaw();
            return raws.Select(r => new Idioma
            {
                Id = r.Id,
                Codigo = r.Codigo,
                Nombre = r.Nombre,
                EsPorDefecto = r.EsPorDefecto
            }).ToList();
        }
    }
}
