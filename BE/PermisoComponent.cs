using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BE
{
    public abstract class PermisoComponent
    {
        protected PermisoComponent(int id, string nombre)
        {
            Id = id;
            Nombre = nombre ?? throw new ArgumentNullException(nameof(nombre));
        }

        /// <summary>
        /// Identificador del permiso (Permiso_Id en la base).
        /// </summary>
        public int Id { get; }

        /// <summary>
        /// Nombre legible del permiso (ej.: "Usuario.Alta").
        /// </summary>
        public string Nombre { get; }

        /// <summary>
        /// Determina si el árbol contiene el permiso indicado.
        /// </summary>
        public abstract bool TienePermiso(string nombrePermiso);

        /// <summary>
        /// Agrega un permiso hijo al componente (solo aplica a compuestos).
        /// </summary>
        /// <param name="permiso">Permiso a agregar.</param>
        public virtual void Agregar(PermisoComponent permiso)
        {
            throw new NotSupportedException("No se pueden agregar hijos a un permiso simple.");
        }

        /// <summary>
        /// Quita un permiso hijo del componente (solo aplica a compuestos).
        /// </summary>
        /// <param name="permiso">Permiso a quitar.</param>
        public virtual void Quitar(PermisoComponent permiso)
        {
            throw new NotSupportedException("No se pueden quitar hijos de un permiso simple.");
        }
    }

    /// <summary>
    /// Hoja del composite: representa un permiso atómico.
    /// </summary>
    public sealed class PermisoSimple : PermisoComponent
    {
        public PermisoSimple(int id, string nombre) : base(id, nombre)
        {
        }

        public override bool TienePermiso(string nombrePermiso)
        {
            if (string.IsNullOrWhiteSpace(nombrePermiso)) return false;
            return string.Equals(Nombre, nombrePermiso, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Nodo compuesto del árbol de permisos.
    /// </summary>
    public sealed class PermisoCompuesto : PermisoComponent
    {
        private readonly List<PermisoComponent> _hijos = new List<PermisoComponent>();

        public PermisoCompuesto(int id, string nombre) : base(id, nombre)
        {
        }

        /// <summary>
        /// Hijos directos del permiso compuesto.
        /// </summary>
        public IReadOnlyCollection<PermisoComponent> Hijos => _hijos.AsReadOnly();

        public override bool TienePermiso(string nombrePermiso)
        {
            if (string.IsNullOrWhiteSpace(nombrePermiso)) return false;

            if (string.Equals(Nombre, nombrePermiso, StringComparison.OrdinalIgnoreCase))
                return true;

            return _hijos.Any(h => h.TienePermiso(nombrePermiso));
        }

        public override void Agregar(PermisoComponent permiso)
        {
            if (permiso == null) throw new ArgumentNullException(nameof(permiso));
            if (_hijos.Any(h => h.Id == permiso.Id)) return; // evitar duplicados por Id
            _hijos.Add(permiso);
        }

        public override void Quitar(PermisoComponent permiso)
        {
            if (permiso == null) throw new ArgumentNullException(nameof(permiso));
            _hijos.RemoveAll(h => h.Id == permiso.Id);
        }
    }
}

