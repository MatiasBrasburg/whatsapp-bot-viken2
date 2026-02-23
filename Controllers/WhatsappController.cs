using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Collections.Concurrent;

namespace WhatsappBot.Controllers
{
    [ApiController]
    [Route("api/whatsapp")]
    public class WhatsappController : ControllerBase
    {
        // Diccionarios en memoria para manejar la "Sala de Espera" y atrapar audios
        private static ConcurrentDictionary<string, bool> _procesandoChat = new();
        private static ConcurrentDictionary<string, string> _ultimoAudio = new();

        [HttpPost]
        public IActionResult ReceiveMessage([FromBody] JsonElement payloadBruto) // OJO: Le sacamos el "async Task" de arriba
        {
            try
            {
                if (!payloadBruto.TryGetProperty("typeWebhook", out JsonElement tipoWebhookElement)) return Ok();
                string tipoMensaje = tipoWebhookElement.GetString() ?? "";
                if (tipoMensaje != "incomingMessageReceived" && tipoMensaje != "outgoingMessageReceived") return Ok();

                var messageData = payloadBruto.GetProperty("messageData");
                string typeMessage = messageData.GetProperty("typeMessage").GetString() ?? "";
                
                string textoMensaje = "";
                string urlAudio = "";

                if (typeMessage == "textMessage")
                    textoMensaje = messageData.GetProperty("textMessageData").GetProperty("textMessage").GetString() ?? "";
                else if (typeMessage == "extendedTextMessage") 
                    textoMensaje = messageData.GetProperty("extendedTextMessageData").GetProperty("text").GetString() ?? "";
                else if (typeMessage == "audioMessage")
                {
                    urlAudio = messageData.GetProperty("fileMessageData").GetProperty("downloadUrl").GetString() ?? "";
                    textoMensaje = "[El cliente envió un audio]";
                }
                else return Ok(); 

                string numeroRemitenteCompleto = payloadBruto.GetProperty("senderData").GetProperty("sender").GetString() ?? "";
                string numeroRemitente = numeroRemitenteCompleto.Replace("@c.us", ""); 
                textoMensaje = textoMensaje.Trim();

                if (tipoMensaje == "outgoingMessageReceived") return Ok(); // Lo simplifico para no dar vueltas

                BD.RegistrarCliente(numeroRemitente);
                if (BD.TraerEstadoBot(numeroRemitente) == false) return Ok();

                // 1. Guardamos cada mensaje que va llegando
                BD.GuardarMensajeEnBD(numeroRemitente, textoMensaje, false);
                if (!string.IsNullOrEmpty(urlAudio)) _ultimoAudio[numeroRemitente] = urlAudio;

                // 2. LA MAGIA DE LOS 40 SEGUNDOS (Patovica de la puerta)
                if (_procesandoChat.TryGetValue(numeroRemitente, out bool estaProcesando) && estaProcesando)
                {
                    Console.WriteLine("⏳ Entró otro mensaje. Se guardó en BD. Seguimos en los 40s...");
                    return Ok(); // Ya hay un cronómetro corriendo, nos vamos.
                }

                _procesandoChat[numeroRemitente] = true;
                Console.WriteLine("⏳ PRIMER MENSAJE. Lanzando cronómetro de 40s de fondo...");

                // 3. TAREA DE FONDO (Para no colgar el Webhook)
                _ = Task.Run(async () => 
                {
                    await Task.Delay(40000); // Los 40s que pediste

                    string historial = BD.ObtenerHistorialChat(numeroRemitente);
                    _ultimoAudio.TryGetValue(numeroRemitente, out string audioParaGemini);

                    Console.WriteLine("🤖 Pasaron 40s. Consultando a Gemini...");
                    string respuestaIA = await GeminiService.ConsultarGemini(historial, audioParaGemini);

                    BD.GuardarMensajeEnBD(numeroRemitente, respuestaIA, true);
                    
                    // Apagamos semáforo y limpiamos audio
                    _procesandoChat[numeroRemitente] = false;
                    _ultimoAudio[numeroRemitente] = "";

                    await EnviarWhatsAppAsync(numeroRemitenteCompleto, respuestaIA);
                    Console.WriteLine($"✅ ¡ÉXITO! Respuesta unificada enviada a {numeroRemitente}.");
                });

                return Ok(); // Le decimos a Green API "OK" instantáneamente.
            }
            catch (Exception ex)
            {
                Console.WriteLine("❌ ERROR CRÍTICO DETECTADO: " + ex.Message);
                return Ok();
            }
        }
        
        





        
   

        private async Task EnviarWhatsAppAsync(string numeroChatId, string mensaje)
        {
            string idInstance = "7103525050";
            string apiTokenInstance = "97f6947c4156485892813fbcc53c033cac597c8a9a494c24ab";
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