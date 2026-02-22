using System;
using System.Data;
using System.Collections.Generic;
using System.Linq;
using Dapper;
using System.Data.SqlClient;

public static class BD
{
    // Lo ponemos adentro de una función para asegurar que NUNCA sea nulo y se lea bien
    private static string GetConnectionString()
    {
        return @"Server=.\SQLEXPRESS01;Database=whatsapp-bot-viken2;Trusted_Connection=True;TrustServerCertificate=True;";
    }

    public static string ObtenerHistorialChat(string telefono)
    {
        using (SqlConnection connection = new SqlConnection(GetConnectionString()))
        {
            string query = @"SELECT TOP 10 Texto, EsBot FROM Mensajes WHERE Telefono = @pTelefono ORDER BY Fecha DESC";
            var mensajes = connection.Query<dynamic>(query, new { pTelefono = telefono }).ToList();
            string historial = "";
            foreach (var m in mensajes)
            {
                string remitente = m.EsBot ? "Bot" : "Cliente";
                historial += $"{remitente}: {m.Texto} \n";
            }
            return historial;
        }
    }

    public static void RegistrarCliente(string Telefono)
    {
        try 
        {
            Console.WriteLine("      [DEBUG] 1. Leyendo cadena de conexión...");
            string connStr = GetConnectionString();
            
            Console.WriteLine("      [DEBUG] 2. Creando objeto de SQL...");
            using (SqlConnection connection = new SqlConnection(connStr))
            {
                Console.WriteLine("      [DEBUG] 3. Intentando ABRIR la conexión con el motor...");
                connection.Open(); 
                
                Console.WriteLine("      [DEBUG] 4. Conexión abierta. Guardando en tabla...");
                string query = @"IF NOT EXISTS (SELECT 1 FROM Clientes WHERE Telefono = @pTelefono)
                                 INSERT INTO Clientes (Telefono, BotActivo) VALUES (@pTelefono, 1)";
                connection.Execute(query, new { pTelefono = Telefono });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("\n❌ [ERROR DE SQL SERVER DETALLADO - CAJA NEGRA]:");
            // ex.ToString() nos va a escupir TODA la ruta del error, renglón por renglón
            Console.WriteLine(ex.ToString()); 
            Console.WriteLine("\n");
            throw; 
        }
    }

    public static bool TraerEstadoBot(string Telefono)
    {
        using (SqlConnection connection = new SqlConnection(GetConnectionString()))
        {
            string query = "SELECT BotActivo FROM Clientes WHERE Telefono = @pTelefono";
            bool? estado = connection.QueryFirstOrDefault<bool?>(query, new { pTelefono = Telefono });
            return estado ?? true;
        }
    }

    public static void CambiarEstadoBot(string Telefono)
    {
        bool estadoActual = TraerEstadoBot(Telefono);
        using (SqlConnection connection = new SqlConnection(GetConnectionString()))
        {
            string query = "UPDATE Clientes SET BotActivo = @pNuevoEstado WHERE Telefono = @pTelefono";
            connection.Execute(query, new { pNuevoEstado = !estadoActual, pTelefono = Telefono });
        }
    }

    public static void GuardarMensajeEnBD(string telefono, string texto, bool esBot)
    {
        using (SqlConnection connection = new SqlConnection(GetConnectionString()))
        {
            string query = "INSERT INTO Mensajes (Telefono, Texto, EsBot, Fecha) VALUES (@pTel, @pTexto, @pEsBot, GETDATE())";
            connection.Execute(query, new { pTel = telefono, pTexto = texto, pEsBot = esBot }); 
        }
    }
}