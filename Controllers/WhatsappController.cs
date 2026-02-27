using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq; 
using System.Text.RegularExpressions; // AGREGADO: Para buscar las etiquetas [CAT: ]

namespace WhatsappBot.Controllers
{
    [ApiController]
    [Route("api/whatsapp")]
    public class WhatsappController : ControllerBase
    {
        private static ConcurrentDictionary<string, bool> _procesandoChat = new();
        private static ConcurrentDictionary<string, ConcurrentQueue<string>> _audiosPendientes = new();
        private static ConcurrentDictionary<string, ConcurrentQueue<string>> _imagenesPendientes = new(); 

        [HttpPost]
        public IActionResult ReceiveMessage([FromBody] JsonElement payloadBruto)
        {
            try
            {
                if (!payloadBruto.TryGetProperty("typeWebhook", out JsonElement tipoWebhookElement)) return Ok();
                string tipoMensaje = tipoWebhookElement.GetString() ?? "";

                // --- 📞 CHICHE: EL ATAJADOR DE LLAMADAS ---
                if (tipoMensaje == "incomingCall")
                {
                    string numeroLlamador = payloadBruto.GetProperty("from").GetString() ?? "";
                    Console.WriteLine($"📞 ¡Llamada rechazada del ansioso: {numeroLlamador}!");
                    
                    _ = EnviarWhatsAppAsync(numeroLlamador, "¡Hola! 🚫 Disculpá, pero esta línea es solo para mensajes de texto y audios. Escribime tu consulta por acá y te atiendo en un ratito. ¡Gracias!");
                    return Ok();
                }
                // -------------------------------------------------

                if (tipoMensaje != "incomingMessageReceived" && tipoMensaje != "outgoingMessageReceived") return Ok();

                var messageData = payloadBruto.GetProperty("messageData");
                string typeMessage = messageData.GetProperty("typeMessage").GetString() ?? "";
                
                string textoMensaje = "";
                string urlAudio = "";
                string urlImagen = "";

                if (typeMessage == "textMessage")
                    textoMensaje = messageData.GetProperty("textMessageData").GetProperty("textMessage").GetString() ?? "";
                else if (typeMessage == "extendedTextMessage") 
                    textoMensaje = messageData.GetProperty("extendedTextMessageData").GetProperty("text").GetString() ?? "";
                else if (typeMessage == "audioMessage")
                {
                    urlAudio = messageData.GetProperty("fileMessageData").GetProperty("downloadUrl").GetString() ?? "";
                    textoMensaje = "[El usuario envió un mensaje de audio]";
                }
                else if (typeMessage == "imageMessage")
                {
                    urlImagen = messageData.GetProperty("fileMessageData").GetProperty("downloadUrl").GetString() ?? "";
                    textoMensaje = "[El usuario envió una imagen de referencia]";
                }
                else return Ok(); 

                string numeroRemitenteCompleto = payloadBruto.GetProperty("senderData").GetProperty("sender").GetString() ?? "";
                string numeroRemitente = numeroRemitenteCompleto.Replace("@c.us", ""); 
                textoMensaje = textoMensaje.Trim();

                // --- 👑 COMANDOS DE ADMIN (Solo para vos) ---
                string tuNumero = "5491155841206"; 
                if (numeroRemitente == tuNumero)
                {
                    if (textoMensaje.ToLower() == "activar_reporte")
                    {
                        BD.ConfigurarReporte(true);
                        _ = EnviarWhatsAppAsync(numeroRemitenteCompleto, "✅ *Reporte diario ACTIVADO.*\nTe va a llegar todos los días a las 20:05 hs un resumen de las ventas.");
                        return Ok(); 
                    }
                    else if (textoMensaje.ToLower() == "desactivar_reporte")
                    {
                        BD.ConfigurarReporte(false);
                        _ = EnviarWhatsAppAsync(numeroRemitenteCompleto, "❌ *Reporte diario DESACTIVADO.*");
                        return Ok();
                    }
                }
                // -------------------------------------------------

                if (tipoMensaje == "outgoingMessageReceived") return Ok(); 

                // --- 🌙 CHICHE: HORARIO COMERCIAL ---
                DateTime horaArg = DateTime.UtcNow.AddHours(-3); 
                if (horaArg.Hour < 9 || horaArg.Hour >= 20) 
                {
                    Console.WriteLine($"🌙 Fuera de hora ({horaArg.Hour}hs). El bot se hace el dormido con {numeroRemitente}.");
                    BD.GuardarMensajeEnBD(numeroRemitente, textoMensaje, false);
                    return Ok(); 
                }
                // ------------------------------------

                BD.RegistrarCliente(numeroRemitente);
                
                if (BD.TraerEstadoBot(numeroRemitente) == false) return Ok();
// Acá termina tu código del Horario Comercial...



// --- 🟢 CHICHE: REACTIVAR BOT MANUALMENTE ---
if (textoMensaje.ToLower() == "prender bot")
{
    Console.WriteLine($"♻️ {numeroRemitente} pidió reactivar el bot.");
    BD.ReactivarBot(numeroRemitente); 
    _ = EnviarWhatsAppAsync(numeroRemitenteCompleto, "🤖 *¡Bot reactivado!* Hola de nuevo, ¿en qué te puedo ayudar?");
    return Ok(); 
}
// --------------------------------------------

// Cambiá el if que tenías por este con el Console.WriteLine así no te volvés loco adivinando
if (BD.TraerEstadoBot(numeroRemitente) == false) 
{
    Console.WriteLine($"🔇 Bot APAGADO para {numeroRemitente}. Me hago el sordo.");
    return Ok(); 
}

BD.GuardarMensajeEnBD(numeroRemitente, textoMensaje, false);
// Acá sigue el código de las colas de audio e imagen...
                BD.GuardarMensajeEnBD(numeroRemitente, textoMensaje, false);
                
                if (!string.IsNullOrEmpty(urlAudio))
                {
                    var colaAudios = _audiosPendientes.GetOrAdd(numeroRemitente, _ => new ConcurrentQueue<string>());
                    colaAudios.Enqueue(urlAudio);
                }

                if (!string.IsNullOrEmpty(urlImagen))
                {
                    var colaImagenes = _imagenesPendientes.GetOrAdd(numeroRemitente, _ => new ConcurrentQueue<string>());
                    colaImagenes.Enqueue(urlImagen);
                }

                if (_procesandoChat.TryGetValue(numeroRemitente, out bool estaProcesando) && estaProcesando)
                {
                    Console.WriteLine("⏳ Entró otro mensaje/audio/foto. Seguimos esperando...");
                    return Ok(); 
                }

                _procesandoChat[numeroRemitente] = true;
                Console.WriteLine("⏳ PRIMER MENSAJE. Lanzando proceso en segundo plano...");

                _ = Task.Run(async () => 
                {
                   


                    Random rnd = new Random();
                    int tiempoEsperaRandom = rnd.Next(40000, 360000); 
                    
                    Console.WriteLine($"🎲 [Modo Humano] Esperando {tiempoEsperaRandom / 1000} segundos antes de responder a {numeroRemitente}...");
                    await Task.Delay(tiempoEsperaRandom);

                    string historial = BD.ObtenerHistorialChat(numeroRemitente);
                    
                    _audiosPendientes.TryRemove(numeroRemitente, out var audiosExtraidos);
                    List<string> listaAudios = audiosExtraidos != null ? audiosExtraidos.ToList() : new List<string>();

                    _imagenesPendientes.TryRemove(numeroRemitente, out var imagenesExtraidas);
                    List<string> listaImagenes = imagenesExtraidas != null ? imagenesExtraidas.ToList() : new List<string>();

                    Console.WriteLine($"🤖 Terminó la espera. Consultando a Gemini...");
                    string respuestaIA = await GeminiService.ConsultarGemini(historial, listaAudios, listaImagenes);

                    // --- 🏷️ CHICHE: EXTRAER CATEGORÍA EN SECRETO ---
                    string categoriaDetectada = null;
                    var match = Regex.Match(respuestaIA, @"\[CAT:\s*(.*?)\]");
                    if (match.Success)
                    {
                        categoriaDetectada = match.Groups[1].Value.Trim();
                        // Borramos la etiqueta para que el cliente no lea comandos robóticos
                        respuestaIA = respuestaIA.Replace(match.Value, "").Trim(); 
                    }
                    // ------------------------------------------------

                    // --- 🚨 MAGIA SAAS: EL PASE A HUMANO 🚨 ---
                    if (respuestaIA.Contains("APAGAR_BOT") || respuestaIA.Contains("[PASAR_A_HUMANO]"))
                    {
                        Console.WriteLine("💰 ¡OLOR A PLATA! Apagando bot y avisando al dueño...");
                        BD.CambiarEstadoBot(numeroRemitente); 
                        
                        string mensajeCliente = "¡Excelente! Ya dejé todo anotado. Te paso con un asesor humano para que te pase los datos de pago y coordine el envío con vos. ¡En un ratito te escribe!";
                        await EnviarWhatsAppAsync(numeroRemitenteCompleto, mensajeCliente);
                        
                        string tuNumeroReporte = "5491155841206@c.us"; 
                        string mensajeDueño = $"🚨 *¡ALERTA DE VENTA!*\nEl número {numeroRemitente} quiere pagar o cerrar pedido. El bot ya se apagó solo. ¡Entrá al WhatsApp y pasale el Alias, campeón!";
                        await EnviarWhatsAppAsync(tuNumeroReporte, mensajeDueño);

                        // Agregamos la categoría al guardar
                        BD.GuardarMensajeEnBD(numeroRemitente, mensajeCliente, true, categoriaDetectada);
                        _procesandoChat[numeroRemitente] = false;
                        return; 
                    }
                    // ------------------------------------------

                    BD.GuardarMensajeEnBD(numeroRemitente, respuestaIA, true, categoriaDetectada);
                    _procesandoChat[numeroRemitente] = false; 

                    await EnviarWhatsAppAsync(numeroRemitenteCompleto, respuestaIA);
                    Console.WriteLine($"✅ ¡ÉXITO! Respuesta enviada a {numeroRemitente}.");
                });

                return Ok(); 
            }
            catch (Exception ex)
            {
                Console.WriteLine("❌ ERROR CRÍTICO DETECTADO: " + ex.Message);
                return Ok();
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
}