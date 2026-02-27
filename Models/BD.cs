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



public static List<string> ObtenerClientesPendientes()
    {
        using (SqlConnection connection = new SqlConnection(GetConnectionString()))
        {
            // Esta magia de SQL busca a todos los clientes que tengan el bot prendido
            // y cuyo ÚLTIMO mensaje guardado haya sido de ellos (EsBot = 0)
            string query = @"
                SELECT c.Telefono 
                FROM Clientes c
                CROSS APPLY (
                    SELECT TOP 1 EsBot 
                    FROM Mensajes m 
                    WHERE m.Telefono = c.Telefono 
                    ORDER BY Fecha DESC
                ) ultMensaje
                WHERE c.BotActivo = 1 AND ultMensaje.EsBot = 0";
            
            return connection.Query<string>(query).ToList();
        }
    }



public static void GuardarMensajeEnBD(string telefono, string texto, bool esBot, string categoria = null)
    {
        using (SqlConnection connection = new SqlConnection(GetConnectionString()))
        {
            // Modificamos el Insert para que ataje la categoría (si le pasamos una)
            string query = "INSERT INTO Mensajes (Telefono, Texto, EsBot, Fecha, Categoria) VALUES (@pTel, @pTexto, @pEsBot, GETDATE(), @pCat)";
            connection.Execute(query, new { pTel = telefono, pTexto = texto, pEsBot = esBot, pCat = categoria }); 
        }
    }

    public static void ConfigurarReporte(bool activar)
    {
        using (SqlConnection connection = new SqlConnection(GetConnectionString()))
        {
            // 1. Magia Negra: Si la tabla Mensajes no tiene la columna 'Categoria', la crea sola.
            string queryColumna = @"IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE Name = N'Categoria' AND Object_ID = Object_ID(N'Mensajes'))
                                    ALTER TABLE Mensajes ADD Categoria NVARCHAR(100) NULL";
            connection.Execute(queryColumna);

            // 2. Guarda tu configuración
            string queryTabla = @"IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Configuracion' and xtype='U') CREATE TABLE Configuracion (Clave varchar(50) PRIMARY KEY, Valor varchar(50))";
            connection.Execute(queryTabla);

            string queryUpdate = @"IF EXISTS (SELECT 1 FROM Configuracion WHERE Clave='ReporteDiario') UPDATE Configuracion SET Valor = @val WHERE Clave='ReporteDiario' ELSE INSERT INTO Configuracion (Clave, Valor) VALUES ('ReporteDiario', @val)";
            connection.Execute(queryUpdate, new { val = activar ? "1" : "0" });
        }
    }

    // Actualizamos el reporte para que lea las categorías
    public static (int clientes, int ventas, string topTemas, string topVentas) ObtenerMetricasDelDia()
    {
        try
        {
            using (SqlConnection connection = new SqlConnection(GetConnectionString()))
            {
                int clientes = connection.QueryFirstOrDefault<int>("SELECT COUNT(DISTINCT Telefono) FROM Mensajes WHERE CAST(Fecha AS DATE) = CAST(GETDATE() AS DATE) AND EsBot = 0");
                int ventas = connection.QueryFirstOrDefault<int>("SELECT COUNT(*) FROM Mensajes WHERE CAST(Fecha AS DATE) = CAST(GETDATE() AS DATE) AND EsBot = 1 AND Texto LIKE '%asesor humano%'");
                
                // Busca de qué se habló más hoy
                var topConsultas = connection.Query("SELECT TOP 3 Categoria, COUNT(*) as Cantidad FROM Mensajes WHERE CAST(Fecha AS DATE) = CAST(GETDATE() AS DATE) AND Categoria IS NOT NULL AND EsBot = 1 GROUP BY Categoria ORDER BY Cantidad DESC").ToList();
                // Busca qué temas terminaron en PASE_A_HUMANO
                var topVentas = connection.Query("SELECT TOP 3 Categoria, COUNT(*) as Cantidad FROM Mensajes WHERE CAST(Fecha AS DATE) = CAST(GETDATE() AS DATE) AND Texto LIKE '%asesor humano%' AND Categoria IS NOT NULL GROUP BY Categoria ORDER BY Cantidad DESC").ToList();

                string strConsultas = topConsultas.Any() ? string.Join(", ", topConsultas.Select(x => $"{x.Categoria} ({x.Cantidad})")) : "Ninguno claro";
                string strVentas = topVentas.Any() ? string.Join(", ", topVentas.Select(x => $"{x.Categoria} ({x.Cantidad})")) : "Ninguno claro";

                return (clientes, ventas, strConsultas, strVentas);
            }
        }
        catch { return (0, 0, "Error", "Error"); }
    }








public static void ReactivarBot(string telefono)
{
    using (SqlConnection connection = new SqlConnection(GetConnectionString()))
    {
        connection.Open();
        string query = "UPDATE Clientes SET EstadoBot = 1 WHERE Telefono = @Telefono";
        connection.Execute(query, new { Telefono = telefono });
    }
}






}