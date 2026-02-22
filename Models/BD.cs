using System;
using System.Data;
using System.Collections.Generic;
using System.Linq;
using Dapper;
using Microsoft.Data.SqlClient;

public static class BD
{
       private static string _connectionString = @"Server=localhost;
DataBase=whatsapp-bot-viken2;Integrated Security=True;TrustServerCertificate=True;";


    public static string ObtenerHistorialChat(string telefono)
    {
        using (SqlConnection connection = new SqlConnection(_connectionString))
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
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                // Forzamos a abrir la base de datos ACÁ. Si falla, nos va a dar el error real.
                connection.Open(); 
                
                string query = @"IF NOT EXISTS (SELECT 1 FROM Clientes WHERE Telefono = @pTelefono)
                                 INSERT INTO Clientes (Telefono, BotActivo) VALUES (@pTelefono, 1)";
                connection.Execute(query, new { pTelefono = Telefono });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("\n❌ [ERROR DE SQL SERVER]: No se pudo conectar a la base de datos.");
            Console.WriteLine("DETALLE: " + ex.Message + "\n");
            throw; // Corta la ejecución para que no explote otra cosa
        }
    }

    public static bool TraerEstadoBot(string Telefono)
    {
        using (SqlConnection connection = new SqlConnection(_connectionString))
        {
            string query = "SELECT BotActivo FROM Clientes WHERE Telefono = @pTelefono";
            bool? estado = connection.QueryFirstOrDefault<bool?>(query, new { pTelefono = Telefono });
            return estado ?? true;
        }
    }

    public static void CambiarEstadoBot(string Telefono)
    {
        bool estadoActual = TraerEstadoBot(Telefono);
        using (SqlConnection connection = new SqlConnection(_connectionString))
        {
            string query = "UPDATE Clientes SET BotActivo = @pNuevoEstado WHERE Telefono = @pTelefono";
            connection.Execute(query, new { pNuevoEstado = !estadoActual, pTelefono = Telefono });
        }
    }

    public static void GuardarMensajeEnBD(string telefono, string texto, bool esBot)
    {
        using (SqlConnection connection = new SqlConnection(_connectionString))
        {
            string query = "INSERT INTO Mensajes (Telefono, Texto, EsBot, Fecha) VALUES (@pTel, @pTexto, @pEsBot, GETDATE())";
            connection.Execute(query, new { pTel = telefono, pTexto = texto, pEsBot = esBot }); 
        }
    }
}