using Microsoft.Extensions.Hosting;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions; // AGREGADO: Para extraer la categoría

public class DespertadorService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Console.WriteLine("⏰ Despertador del bot activado en segundo plano (No consume recursos)...");

        while (!stoppingToken.IsCancellationRequested)
        {
            DateTime horaArg = DateTime.UtcNow.AddHours(-3);
            
            // --- TURNO MAÑANA: 09:00 AM (Responder pendientes) ---
            if (horaArg.Hour == 9 && horaArg.Minute == 0)
            {
                Console.WriteLine("☀️ ¡Buen día! Son las 9 AM. Revisando mensajes de la madrugada...");
                
                List<string> pendientes = BD.ObtenerClientesPendientes();
                
                foreach (var telefono in pendientes)
                {
                    string historial = BD.ObtenerHistorialChat(telefono);
                    
                    Console.WriteLine($"🤖 Evaluando mensaje pendiente de: {telefono}");
                    string respuestaIA = await GeminiService.ConsultarGemini(historial);

                    // --- 🏷️ CHICHE: EXTRAER CATEGORÍA EN SECRETO ---
                    string categoriaDetectada = null;
                    var match = Regex.Match(respuestaIA, @"\[CAT:\s*(.*?)\]");
                    if (match.Success)
                    {
                        categoriaDetectada = match.Groups[1].Value.Trim();
                        respuestaIA = respuestaIA.Replace(match.Value, "").Trim(); 
                    }
                    // ------------------------------------------------

                    // --- 👻 CHICHE: EL VISTO INTELIGENTE ---
                    if (respuestaIA.Contains("[IGNORAR]"))
                    {
                        Console.WriteLine($"👻 El cliente {telefono} solo cerró la charla. Clavando visto...");
                        BD.GuardarMensajeEnBD(telefono, "✅ [Bot clavó el visto estratégicamente]", true, categoriaDetectada);
                        continue; 
                    }
                    // ---------------------------------------

                    // --- 🎲 TIEMPO DE ESPERA RANDOM ---
                    Random rnd = new Random();
                    int tiempoEsperaRandom = rnd.Next(40000, 360000); // Entre 40 segs y 6 mins
                    Console.WriteLine($"🎲 [Modo Humano] Esperando {tiempoEsperaRandom / 1000} segundos antes de responderle a {telefono}...");
                    await Task.Delay(tiempoEsperaRandom, stoppingToken);

                    // --- 🚨 PASE A HUMANO ---
                    if (respuestaIA.Contains("[PASAR_A_HUMANO]"))
                    {
                        BD.CambiarEstadoBot(telefono); 
                        string mensajeCliente = "¡Buen día! Ya dejé todo anotado. Te paso con un asesor humano para coordinar todo. ¡En un ratito te escribe!";
                        await EnviarWhatsAppAsync(telefono + "@c.us", mensajeCliente);
                        
                        string tuNumero = "5491155841206@c.us"; 
                        string mensajeDueño = $"🚨 *¡VENTA MATUTINA!*\nEl {telefono} quiere pagar. ¡Entrá y pasale el Alias!";
                        await EnviarWhatsAppAsync(tuNumero, mensajeDueño);
                        
                        BD.GuardarMensajeEnBD(telefono, mensajeCliente, true, categoriaDetectada);
                    }
                    else
                    {
                        await EnviarWhatsAppAsync(telefono + "@c.us", respuestaIA);
                        BD.GuardarMensajeEnBD(telefono, respuestaIA, true, categoriaDetectada);
                    }
                }
                
                await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);
            }
            // --- 📊 TURNO NOCHE: 20:05 PM (Reporte Diario) ---
            else if (horaArg.Hour == 20 && horaArg.Minute == 5)
            {
                if (BD.ReporteActivado())
                {
                    Console.WriteLine("📊 Generando reporte diario para el dueño...");
                    var metricas = BD.ObtenerMetricasDelDia();
                    
                    string mensajeReporte = $"📊 *RESUMEN DEL DÍA (Viken Home)* 📊\n\n" +
                                            $"🗣️ Clientes de hoy: *{metricas.clientes}*\n" +
                                            $"💰 Intenciones de compra: *{metricas.ventas}*\n\n" +
                                            $"🔍 *Top Temas Preguntados:*\n{metricas.topTemas}\n\n" +
                                            $"🎯 *Temas que generaron ventas:*\n{metricas.topVentas}\n\n" +
                                            $"¡A descansar, campeón! 🌙\n" +
                                            $"_(Para desactivar, decime 'desactivar_reporte')_";
                    
                    string tuNumero = "5491155841206@c.us";
                    await EnviarWhatsAppAsync(tuNumero, mensajeReporte);
                }
                
                await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);
            }
            // -------------------------------------------------
            else
            {
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
    }

    private async Task EnviarWhatsAppAsync(string numeroChatId, string mensaje)
    {
        string idInstance = Environment.GetEnvironmentVariable("GREEN_API_INSTANCE");
        string apiTokenInstance = Environment.GetEnvironmentVariable("GREEN_API_TOKEN");
        string url = $"https://api.green-api.com/waInstance{idInstance}/sendMessage/{apiTokenInstance}";

        using (HttpClient client = new HttpClient())
        {
            var payload = new { chatId = numeroChatId, message = mensaje };
            string jsonPayload = JsonSerializer.Serialize(payload);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
            await client.PostAsync(url, content);
        }
    }
}