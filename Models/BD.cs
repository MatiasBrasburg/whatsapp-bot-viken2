using System.Data;
using System.Collections.Generic;
using System.Linq;
using Dapper;
using Microsoft.Data.SqlClient;
//using Npgsql; // Cambio Clave: Usamos el driver de PostgreSQL
using Microsoft.Extensions.Configuration;
using System; // Necesario si usaras config, pero usaremos Environment

public static class BD
{
    
    private static string GetConnectionString()
{
    // Para SQL Server Local (SSMS)
    // Server=. significa que usa tu propia computadora
    // Database=VikenBotDB es el nombre de la BD que creaste en el paso anterior
    // Trusted_Connection=True le dice que use tu usuario de Windows (sin contraseña)
    return "Server=.;Database=whatsapp-bot-viken2;Trusted_Connection=True;TrustServerCertificate=True;";
}
public static string ObtenerHistorialChat(string telefono)
{
    using (SqlConnection connection = new SqlConnection(GetConnectionString()))
    {
        // Traemos los últimos mensajes ordenados por fecha
        string query = @"SELECT TOP 10 Texto, EsBot FROM Mensajes 
                         WHERE Telefono = @pTelefono ORDER BY Fecha DESC";
        
        var mensajes = connection.Query<dynamic>(query, new { pTelefono = telefono }).ToList();
        
        // Los unimos en un solo string gigante para el Prompt
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
    using (SqlConnection connection = new SqlConnection(GetConnectionString()))
    {
        // Usamos IF NOT EXISTS para que no tire error si el cliente ya existe
        string query = @"IF NOT EXISTS (SELECT 1 FROM Clientes WHERE Telefono = @pTelefono)
                         INSERT INTO Clientes (Telefono, BotActivo) VALUES (@pTelefono, 1)";
        

        connection.Execute(query, new { pTelefono = Telefono });
    }
}


public static bool TraerEstadoBot(string Telefono)
{
    using (SqlConnection connection = new SqlConnection(GetConnectionString()))
    {
        string query = "SELECT BotActivo FROM Clientes WHERE Telefono = @pTelefono";
        // Traemos solo el valor booleano directamente
        bool? estado = connection.QueryFirstOrDefault<bool?>(query, new { pTelefono = Telefono });
        
        // Si es null (cliente nuevo), devolvemos true por defecto
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
            using (Microsoft.Data.SqlClient.SqlConnection connection = new Microsoft.Data.SqlClient.SqlConnection("Server=.;Database=whatsapp-bot-viken2;Trusted_Connection=True;TrustServerCertificate=True;"))
            {
                string query = "INSERT INTO Mensajes (Telefono, Texto, EsBot, Fecha) VALUES (@pTel, @pTexto, @pEsBot, GETDATE())";
                Dapper.SqlMapper.Execute(connection, query, new { pTel = telefono, pTexto = texto, pEsBot = esBot });
            }
        }































































































































} 