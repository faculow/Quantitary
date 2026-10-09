using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Http;

namespace Quantitary.Models
{
    public static class BD
    {
        // Cadena de conexión a tu base de datos en SQL Server
        private static string connectionString = @"Server=localhost;Database=Quantitary;Trusted_Connection=True;TrustServerCertificate=True;";

        // 1. REGISTRARSE
        public static bool Registrarse(Usuario usuario)
        {
            // Validar primero si el mail ya está registrado
            if (ExisteEmail(usuario.Mail))
            {
                return false;
            }

            string query = @"INSERT INTO Usuarios (nombreUsuario, contrasena, mail, numeroTelefono, empresaID, rolID) 
                            VALUES (@NombreUsuario, @Contrasena, @Mail, @NumeroTelefono, @EmpresaID, @RolID)";

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@NombreUsuario", usuario.NombreUsuario);
                    command.Parameters.AddWithValue("@Contrasena", usuario.Contrasena);
                    command.Parameters.AddWithValue("@Mail", usuario.Mail);
                    command.Parameters.AddWithValue("@NumeroTelefono", (object?)usuario.NumeroTelefono ?? DBNull.Value);
                    command.Parameters.AddWithValue("@EmpresaID", usuario.EmpresaID);
                    command.Parameters.AddWithValue("@RolID", usuario.RolID);

                    connection.Open();
                    int filasAfectadas = command.ExecuteNonQuery();
                    return filasAfectadas > 0;
                }
            }
        }

        // Método auxiliar para verificar si un mail ya existe
        private static bool ExisteEmail(string mail)
        {
            string query = "SELECT COUNT(*) FROM Usuarios WHERE mail = @Mail";

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@Mail", mail);
                    connection.Open();
                    int cantidad = Convert.ToInt32(command.ExecuteScalar());
                    return cantidad > 0;
                }
            }
        }

        // 2. INICIAR SESIÓN (Verifica credenciales y guarda datos en la Session)
        public static bool IniciarSesion(string mail, string contrasena, HttpContext httpContext)
        {
            Usuario? usuarioEncontrado = null;

            string query = @"SELECT id, nombreUsuario, contrasena, mail, numeroTelefono, empresaID, rolID 
                            FROM Usuarios 
                            WHERE mail = @Mail AND contrasena = @Contrasena";

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@Mail", mail);
                    command.Parameters.AddWithValue("@Contrasena", contrasena);

                    connection.Open();
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            usuarioEncontrado = new Usuario
                            {
                                Id = Convert.ToInt32(reader["id"]),
                                NombreUsuario = reader["nombreUsuario"].ToString() ?? "",
                                Contrasena = reader["contrasena"].ToString() ?? "",
                                Mail = reader["mail"].ToString() ?? "",
                                NumeroTelefono = reader["numeroTelefono"] != DBNull.Value ? reader["numeroTelefono"].ToString() : null,
                                EmpresaID = Convert.ToInt32(reader["empresaID"]),
                                RolID = Convert.ToInt32(reader["rolID"])
                            };
                        }
                    }
                }
            }

            // Si las credenciales eran correctas, se guarda la información en la Session
            if (usuarioEncontrado != null)
            {
                httpContext.Session.SetInt32("UsuarioID", usuarioEncontrado.Id);
                httpContext.Session.SetString("NombreUsuario", usuarioEncontrado.NombreUsuario);
                httpContext.Session.SetInt32("EmpresaID", usuarioEncontrado.EmpresaID);
                httpContext.Session.SetInt32("RolID", usuarioEncontrado.RolID);

                return true;
            }

            return false;
        }

        // 3. CERRAR SESIÓN
        public static void CerrarSesion(HttpContext httpContext)
        {
            httpContext.Session.Clear();
        }

        // 4. OBTENER ID DEL USUARIO LOGUEADO (Útil para validar vistas)
        public static int? ObtenerUsuarioLogueadoID(HttpContext httpContext)
        {
            return httpContext.Session.GetInt32("UsuarioID");
        }
    }
}