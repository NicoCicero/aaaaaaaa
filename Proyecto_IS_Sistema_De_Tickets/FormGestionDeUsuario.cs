using BE;
using BL;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Proyecto_IS_Sistema_De_Tickets
{
    public partial class FormGestionDeUsuario : Form, IIdiomaObserver
    {
        private readonly IdiomaService _idiomaSrv = new IdiomaService();

        public FormGestionDeUsuario(ModoFormulario modo, int? usuarioId = null)
        {
            InitializeComponent();

            _modo = modo;
            _usuarioId = usuarioId;

            IdiomaManager.Instancia.Suscribir(this);
            tvPermisos.AfterCheck += TvPermisos_AfterCheck;
            tvPermisos.BeforeCheck += TvPermisos_BeforeCheck;
        }
        public enum ModoFormulario
        {
            Alta,
            Edicion
        }
        private readonly ModoFormulario _modo;
        private readonly int? _usuarioId;
        private bool _suspendCheckEvents;

        public void ActualizarIdioma(Dictionary<string, string> t)
        {
            // títulos según modo
            if ((string)lblGestionUsuario.Tag == "NEW")
                lblGestionUsuario.Text = t["TITLE_NUEVO_REGISTRO"];
            else if ((string)lblGestionUsuario.Tag == "EDIT")
                lblGestionUsuario.Text = t["TITLE_ACTUALIZAR_USUARIO"];
            else
                lblGestionUsuario.Text = t["TITLE_GESTION_USUARIO"];

            // labels
            lblEmail.Text = t["LBL_EMAIL"];
            lblNombre.Text = t["LBL_NOMBRE"];
            lblContraseña.Text = t["LBL_CONTRASENA"];
            lblConfirmarContraseña.Text = t["LBL_CONFIRMAR_CONTRASENA"];

            // checks
            chkActivo.Text = t["CHK_USUARIO_ACTIVO"];
            chkMostrarContraseña.Text = t["CHK_MOSTRAR_CONTRASENA"];

            // botones
            btnGuardar.Text = t["BTN_GUARDAR"];
            btnCancelar.Text = t["BTN_CANCELAR"];
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            IdiomaManager.Instancia.Desuscribir(this);
            base.OnFormClosed(e);
        }

        private void btnGuardar_Click(object sender, EventArgs e)
        {
            bool puedeCrear = SessionManager.Instancia.TienePermiso("Usuario.Alta");
            bool puedeEditar = SessionManager.Instancia.TienePermiso("Usuario.Modificar");

            string email = txtEmail.Text.Trim();
            string nombre = txtNombre.Text.Trim();
            string pass1 = txtContraseña.Text;
            string pass2 = txtConfirmarContraseña.Text;
            bool activo = chkActivo.Checked;

            // 1) saco lo tildado del tree
            var rolesIds = new List<int>();
            var permisosIds = new List<int>();

            foreach (TreeNode raiz in tvPermisos.Nodes)
            {
                foreach (TreeNode nodo in raiz.Nodes)
                {
                    LeerNodoTildado(nodo, rolesIds, permisosIds);
                }
            }

            // debe haber un único rol seleccionado
            if (rolesIds.Count != 1)
            {
                MessageBox.Show("Seleccioná exactamente un rol.");
                return;
            }

            // ⚠️ 2) VALIDACIÓN DE REDUNDANCIA
            // si tildó permisos sueltos y además tildó roles, tengo que chequear
            if (permisosIds.Count > 0 && rolesIds.Count > 0)
            {
                // junto todos los permisos que traen esos roles
                var permisosQueTraenLosRoles = new HashSet<int>();

                foreach (var rolId in rolesIds)
                {
                    // esto en tu proyecto devuelve List<PermisoComponent>
                    var permisosDelRol = PermisoService.Instancia.ObtenerPermisosDeRol(rolId);
                    if (permisosDelRol == null) continue;

                    foreach (var comp in permisosDelRol)
                        AplanarPermisoComponent(comp, permisosQueTraenLosRoles);
                }

                // ahora sí: veo si algún permiso suelto está adentro de los permisos de los roles
                foreach (var permSuelto in permisosIds)
                {
                    if (permisosQueTraenLosRoles.Contains(permSuelto))
                    {
                        MessageBox.Show("Estás asignando un permiso suelto que ya viene dentro de uno de los roles seleccionados. Quitá el permiso suelto.");
                        return;
                    }
                }
            }

            try
            {
                if (_modo == ModoFormulario.Alta)
                {
                    if (!puedeCrear)
                    {
                        MessageBox.Show("No contás con permiso para dar de alta usuarios.");
                        return;
                    }
                    // en alta la pass es obligatoria
                    if (string.IsNullOrEmpty(pass1) || pass1.Length < 8)
                    {
                        MessageBox.Show("La contraseña debe tener al menos 8 caracteres.");
                        return;
                    }
                    if (pass1 != pass2)
                    {
                        MessageBox.Show("Las contraseñas no coinciden.");
                        return;
                    }

                    int nuevoId = UserAdminService.Instancia.CrearUsuario(email, nombre, pass1, activo, rolesIds);

                    // permisos directos del usuario
                    PermisoService.Instancia.ReemplazarPermisosDirectosUsuario(nuevoId, permisosIds);

                    MessageBox.Show("Usuario creado. Id=" + nuevoId);
                    this.Close();
                }
                else
                {
                    if (!puedeEditar)
                    {
                        MessageBox.Show("No contás con permiso para modificar usuarios.");
                        return;
                    }

                    // EDICIÓN
                    string passOpcional = null;
                    if (!string.IsNullOrEmpty(pass1) || !string.IsNullOrEmpty(pass2))
                    {
                        if (pass1.Length < 8)
                        {
                            MessageBox.Show("La contraseña debe tener al menos 8 caracteres.");
                            return;
                        }
                        if (pass1 != pass2)
                        {
                            MessageBox.Show("Las contraseñas no coinciden.");
                            return;
                        }
                        passOpcional = pass1;
                    }

                    UserAdminService.Instancia.ActualizarUsuario(
                        id: _usuarioId.Value,
                        email: email,
                        nombre: nombre,
                        activo: activo,
                        rolesIds: rolesIds,
                        passwordPlano: passOpcional
                    );

                    // actualizo los permisos directos del usuario
                    PermisoService.Instancia.ReemplazarPermisosDirectosUsuario(_usuarioId.Value, permisosIds);

                    MessageBox.Show("Usuario actualizado.");
                    this.Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message);
            }
        }

        // mete en el set este permiso y todos sus hijos
        private void AplanarPermisoComponent(PermisoComponent comp, HashSet<int> set)
        {
            if (comp == null) return;

            if (!set.Contains(comp.Id))
                set.Add(comp.Id);

            if (comp is PermisoCompuesto pc && pc.Hijos != null)
            {
                foreach (var h in pc.Hijos)
                    AplanarPermisoComponent(h, set);
            }
        }


        private void JuntarPermisosRecursivo(BE.PermisoComponent perm, HashSet<int> destino)
        {
            destino.Add(perm.Id);
            if (perm is BE.PermisoCompuesto comp)
            {
                foreach (var h in comp.Hijos)
                    JuntarPermisosRecursivo(h, destino);
            }
        }


        private void LeerNodoTildado(TreeNode nodo, List<int> rolesIds, List<int> permisosIds)
        {
            if (nodo.Checked && nodo.Tag is string tag)
            {
                if (tag.StartsWith("ROL_"))
                {
                    int id = int.Parse(tag.Substring(4));
                    if (!rolesIds.Contains(id))
                        rolesIds.Add(id);
                }
                else if (tag.StartsWith("PERM_"))
                {
                    int id = int.Parse(tag.Substring(5));
                    if (!permisosIds.Contains(id))
                        permisosIds.Add(id);
                }
            }

            // recursivo por si es un permiso compuesto
            foreach (TreeNode h in nodo.Nodes)
                LeerNodoTildado(h, rolesIds, permisosIds);
        }


        private void FormGestionDeUsuario_Load(object sender, EventArgs e)
        {
            if (SessionManager.Instancia.UsuarioActual == null)
            {
                MessageBox.Show("La sesión no está activa. Volviendo al login.");
                Application.Restart();
                return;
            }
            bool puedeModificar = SessionManager.Instancia.TienePermiso("Usuario.Modificar");
            bool puedeCrear = SessionManager.Instancia.TienePermiso("Usuario.Alta");

            if (_modo == ModoFormulario.Alta)
            {
                if (!puedeCrear)
                {
                    MessageBox.Show("No contás con permiso para dar de alta usuarios.");
                    Close();
                    return;
                }

                lblGestionUsuario.Tag = "NEW";
                chkActivo.Checked = true;
            }
            else
            {
                if (!puedeModificar)
                {
                    MessageBox.Show("No contás con permiso para modificar usuarios.");
                    Close();
                    return;
                }

                lblGestionUsuario.Tag = "EDIT";
            }

            CargarTreeRolesYPermisos();

            if (_modo == ModoFormulario.Edicion && _usuarioId.HasValue)
            {
                CargarUsuarioEnControles(_usuarioId.Value);
            }
            txtContraseña.UseSystemPasswordChar = true;
            txtConfirmarContraseña.UseSystemPasswordChar = true;
            chkMostrarContraseña.CheckedChanged += (s, ev) =>
            {
                bool ver = chkMostrarContraseña.Checked;
                txtContraseña.UseSystemPasswordChar = !ver;
                txtConfirmarContraseña.UseSystemPasswordChar = !ver;
            };
            // Mostrar/ocultar según rol
            //var puedeCrear = SessionManager.Instancia.UsuarioActual.TienePermiso("Usuario.Alta");
            //this.Text = puedeCrear
            //    ? "Gestión de Usuarios - Puede crear usuarios"
            //    : "Gestión de Usuarios - Solo lectura";

            

        }

        private void CargarTreeRolesYPermisos()
        {
            _suspendCheckEvents = true;
            tvPermisos.BeginUpdate();
            try
            {
                tvPermisos.Nodes.Clear();

                var nodoRoles = new TreeNode("Roles");
                var roles = UserAdminService.Instancia.ListarRoles();
                foreach (var r in roles)
                {
                    nodoRoles.Nodes.Add(new TreeNode(r.Nombre)
                    {
                        Tag = "ROL_" + r.Id
                    });
                }
                tvPermisos.Nodes.Add(nodoRoles);

                var nodoPerms = new TreeNode("Permisos");
                var arbolPerms = PermisoService.Instancia.ObtenerArbolPermisos();
                foreach (var p in arbolPerms)
                {
                    nodoPerms.Nodes.Add(PermisoTreeHelper.CrearNodo(p));
                }
                tvPermisos.Nodes.Add(nodoPerms);
            }
            finally
            {
                tvPermisos.EndUpdate();
                _suspendCheckEvents = false;
            }

            tvPermisos.ExpandAll();
        }


        private void CargarUsuarioEnControles(int id)
        {
            var u = UserAdminService.Instancia.ObtenerUsuarioCompleto(id);
            if (u == null)
            {
                MessageBox.Show("Usuario no encontrado.");
                Close();
                return;
            }

            txtEmail.Text = u.Email;
            txtNombre.Text = u.Nombre;
            chkActivo.Checked = u.Activo;

            MarcarTreeSegunUsuario(id);

        }

        private void MarcarTreeSegunUsuario(int usuarioId)
        {
            // roles del usuario
            var u = UserAdminService.Instancia.ObtenerUsuarioCompleto(usuarioId);
            var rolesUsuario = u.Roles?.Select(r => r.Id).ToList() ?? new List<int>();
            int? rolPrincipal = rolesUsuario.FirstOrDefault();

            // permisos directos del usuario (si tenés tabla UsuarioPermiso)
            var permisosDirectos = new DAO.UsuarioPermisoRepository().GetPermisosDirectos(usuarioId).Select(p => p.Id).ToHashSet();

            _suspendCheckEvents = true;
            try
            {
                foreach (TreeNode raiz in tvPermisos.Nodes)
                {
                    foreach (TreeNode hijo in raiz.Nodes)
                    {
                        if (hijo.Tag is string tag)
                        {
                            if (tag.StartsWith("ROL_"))
                            {
                                int rolId = int.Parse(tag.Substring(4));
                                hijo.Checked = rolPrincipal.HasValue && rolId == rolPrincipal.Value;
                            }
                            else if (tag.StartsWith("PERM_"))
                            {
                                int permId = int.Parse(tag.Substring(5));
                                hijo.Checked = permisosDirectos.Contains(permId);
                            }
                        }

                        // por si hay permisos anidados
                        MarcarRecursivoPermisos(hijo, permisosDirectos);
                    }
                }
            }
            finally
            {
                _suspendCheckEvents = false;
            }
        }

        private void MarcarRecursivoPermisos(TreeNode nodo, HashSet<int> permisosDirectos)
        {
            foreach (TreeNode h in nodo.Nodes)
            {
                if (h.Tag is string tag && tag.StartsWith("PERM_"))
                {
                    int permId = int.Parse(tag.Substring(5));
                    h.Checked = permisosDirectos.Contains(permId);
                }
                MarcarRecursivoPermisos(h, permisosDirectos);
            }
        }

        // arma un diccionario {permisoId -> PermisoNodo} para poder buscar rápido
        private void IndexarPermisos(IEnumerable<PermisoNodo> nodos, Dictionary<int, PermisoNodo> dic)
        {
            foreach (var n in nodos)
            {
                if (!dic.ContainsKey(n.Id))
                    dic.Add(n.Id, n);

                if (n.Hijos != null && n.Hijos.Count > 0)
                    IndexarPermisos(n.Hijos, dic);
            }
        }

        // dice si un permiso (buscadoId) está adentro del permiso raiz (raizId) usando el diccionario
        private bool PermisoEstaDentroDe(int raizId, int buscadoId, Dictionary<int, PermisoNodo> dic)
        {
            if (raizId == buscadoId)
                return true;

            if (!dic.TryGetValue(raizId, out var nodo))
                return false;

            foreach (var h in nodo.Hijos)
            {
                if (PermisoEstaDentroDe(h.Id, buscadoId, dic))
                    return true;
            }

            return false;
        }


        private void btnCancelar_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void TvPermisos_AfterCheck(object sender, TreeViewEventArgs e)
        {
            if (_suspendCheckEvents || e.Node == null)
                return;

            if (!e.Node.Checked)
                return;

            if (e.Node.Tag is string tag && tag.StartsWith("ROL_"))
            {
                _suspendCheckEvents = true;
                try
                {
                    foreach (TreeNode raiz in tvPermisos.Nodes)
                    {
                        foreach (TreeNode hijo in raiz.Nodes)
                        {
                            if (!ReferenceEquals(hijo, e.Node) && hijo.Tag is string otroTag && otroTag.StartsWith("ROL_"))
                                hijo.Checked = false;
                        }
                    }
                }
                finally
                {
                    _suspendCheckEvents = false;
                }
            }
        }

        private void TvPermisos_BeforeCheck(object sender, TreeViewCancelEventArgs e)
        {
            if (e.Node == null)
                return;

            if (e.Node.Tag is string tag)
            {
                if (tag.StartsWith("ROL_") || tag.StartsWith("PERM_"))
                    return;
            }

            e.Cancel = true;
            e.Node.Checked = false;
        }
    }
}
