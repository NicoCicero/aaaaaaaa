using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BL
{
    public class IdiomaManager
    {
        private static readonly Lazy<IdiomaManager> _inst =
            new Lazy<IdiomaManager>(() => new IdiomaManager());
        public static IdiomaManager Instancia => _inst.Value;

        private readonly List<IIdiomaObserver> _observers = new List<IIdiomaObserver>();

        private Dictionary<string, string> _ultimaTraduccion;

        private IdiomaManager() { }

        public void Suscribir(IIdiomaObserver obs)
        {
            if (!_observers.Contains(obs))
                _observers.Add(obs);

            // si ya había una traducción cargada, se la mando al nuevo
            if (_ultimaTraduccion != null)
                obs.ActualizarIdioma(_ultimaTraduccion);
        }

        public void Desuscribir(IIdiomaObserver obs)
        {
            _observers.Remove(obs);
        }

        public void Notificar(Dictionary<string, string> textos)
        {
            // guardo la última
            _ultimaTraduccion = textos;

            foreach (var o in _observers.ToList())
                o.ActualizarIdioma(textos);
        }

        public Dictionary<string, string> ObtenerUltimaTraduccion()
        {
            return _ultimaTraduccion;
        }

        private string _codigoActual;
        public string CodigoActual => _codigoActual;

        // Lo usa IdiomaService para setear el idioma elegido
        internal void SetCodigoActual(string codigo)
        {
            _codigoActual = codigo;
        }
    }
}
