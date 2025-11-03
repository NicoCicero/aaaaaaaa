using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BE
{
    public class PermisoComposite
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public bool EsCompuesto { get; set; }
    }
}
