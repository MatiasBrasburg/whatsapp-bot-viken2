using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using System;
using System.Net.Http;   
using System.Text;       
using System.Text.Json;

namespace WhatsappBot.Controllers
{
    [ApiController]
    [Route("api/whatsapp")] 
    public class WhatsappController : ControllerBase
    {
        [HttpPost]
        public async Task<IActionResult> ReceiveMessage([FromBody] JsonElement payloadBruto)
        {
            try
            {
                // Leemos con cuidado para evitar errores de campos vacíos
                if (!payloadBruto.TryGetProperty("typeWebhook", out JsonElement tipoWebhookElement)) return Ok();
                string tipoMensaje = tipoWebhookElement.GetString() ?? "";
                if (tipoMensaje != "incomingMessageReceived" && tipoMensaje != "outgoingMessageReceived") return Ok();

                var messageData = payloadBruto.GetProperty("messageData");
                string typeMessage = messageData.GetProperty("typeMessage").GetString() ?? "";
                
                string textoMensaje = "";
                if (typeMessage == "textMessage")
                    textoMensaje = messageData.GetProperty("textMessageData").GetProperty("textMessage").GetString() ?? "";
                else if (typeMessage == "extendedTextMessage") 
                    textoMensaje = messageData.GetProperty("extendedTextMessageData").GetProperty("text").GetString() ?? "";
                else 
                    return Ok(); 

                string numeroRemitenteCompleto = payloadBruto.GetProperty("senderData").GetProperty("sender").GetString() ?? "";
                string numeroRemitente = numeroRemitenteCompleto.Replace("@c.us", ""); 
                
                textoMensaje = textoMensaje.Trim();
                bool loMandeYo = (tipoMensaje == "outgoingMessageReceived");

                if (loMandeYo)
                {
                    if (textoMensaje == "APAGAR_BOT") { BD.CambiarEstadoBot(numeroRemitente); return Ok(); }
                    else if (textoMensaje == "PRENDER_BOT") { BD.CambiarEstadoBot(numeroRemitente); return Ok(); }
                    return Ok(); 
                }

                Console.WriteLine("\n====================================");
                Console.WriteLine("📍 PASO 1: Conectando a BD (Registrar Cliente)...");
                BD.RegistrarCliente(numeroRemitente);

                Console.WriteLine("📍 PASO 2: Verificando estado del bot...");
                bool botActivo = BD.TraerEstadoBot(numeroRemitente);
                if (botActivo == false) return Ok();

                Console.WriteLine("📍 PASO 3: Guardando mensaje del cliente...");
                BD.GuardarMensajeEnBD(numeroRemitente, textoMensaje, false);

                Console.WriteLine("📍 PASO 4: Obteniendo historial de chat...");
                string historial = BD.ObtenerHistorialChat(numeroRemitente);

                Console.WriteLine("📍 PASO 5: Consultando a la IA (Gemini)...");
                string respuestaIA = await GeminiService.ConsultarGemini(historial, textoMensaje);

                Console.WriteLine("📍 PASO 6: Guardando respuesta de la IA...");
                BD.GuardarMensajeEnBD(numeroRemitente, respuestaIA, true);

                Console.WriteLine("📍 PASO 7: Enviando mensaje por WhatsApp...");
                int tiempoTipeo = respuestaIA.Length * 30;
                if (tiempoTipeo > 8000) tiempoTipeo = 8000; 
                await Task.Delay(tiempoTipeo);

                await EnviarWhatsAppAsync(numeroRemitenteCompleto, respuestaIA);

                Console.WriteLine($"✅ ¡ÉXITO! Respuesta enviada a {numeroRemitente}.");
                Console.WriteLine("====================================\n");
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