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
                // 1. Verificamos qué tipo de aviso nos manda Green-API
                string tipoMensaje = payloadBruto.GetProperty("typeWebhook").GetString();
                
                // Solo nos importan los mensajes entrantes o los que mandás vos desde tu celu
                if (tipoMensaje != "incomingMessageReceived" && tipoMensaje != "outgoingMessageReceived") 
                    return Ok();

                var messageData = payloadBruto.GetProperty("messageData");
                
                // Si mandan un audio o foto, por ahora lo ignoramos (solo leemos texto)
                if (messageData.GetProperty("typeMessage").GetString() != "textMessage") 
                    return Ok();

                // 2. Extraemos el teléfono y el mensaje
                string numeroRemitenteCompleto = payloadBruto.GetProperty("senderData").GetProperty("sender").GetString();
                string numeroRemitente = numeroRemitenteCompleto.Replace("@c.us", ""); // Limpiamos el formato de GreenAPI
                
                string textoMensaje = messageData.GetProperty("textMessageData").GetProperty("textMessage").GetString().Trim();

                bool loMandeYo = (tipoMensaje == "outgoingMessageReceived");

                // =======================================================
                // FASE 1: COMANDOS DEL DUEÑO
                // =======================================================
                if (loMandeYo)
                {
                    if (textoMensaje == "APAGAR_BOT")
                    {
                        BD.CambiarEstadoBot(numeroRemitente); 
                        Console.WriteLine("🛑 Bot APAGADO para " + numeroRemitente);
                        return Ok(); 
                    }
                    else if (textoMensaje == "PRENDER_BOT")
                    {
                        BD.CambiarEstadoBot(numeroRemitente);
                        Console.WriteLine("✅ Bot PRENDIDO para " + numeroRemitente);
                        return Ok(); 
                    }
                    return Ok(); // Si lo mandaste vos pero no es un comando, ignoramos
                }

                // =======================================================
                // FASE 2: ATENCIÓN AL CLIENTE Y BASE DE DATOS
                // =======================================================
                BD.RegistrarCliente(numeroRemitente);

                bool botActivo = BD.TraerEstadoBot(numeroRemitente);
                if (botActivo == false) return Ok();

                BD.GuardarMensajeEnBD(numeroRemitente, textoMensaje, false);

                // =======================================================
                // FASE 3: EL CEREBRO (GEMINI)
                // =======================================================
                string historial = BD.ObtenerHistorialChat(numeroRemitente);
                string respuestaIA = await GeminiService.ConsultarGemini(historial, textoMensaje);

                BD.GuardarMensajeEnBD(numeroRemitente, respuestaIA, true);

                // =======================================================
                // FASE 4: SIMULACIÓN HUMANA Y ENVÍO A GREEN-API
                // =======================================================
                int tiempoTipeo = respuestaIA.Length * 30;
                if (tiempoTipeo > 8000) tiempoTipeo = 8000; 
                await Task.Delay(tiempoTipeo);

                await EnviarWhatsAppAsync(numeroRemitenteCompleto, respuestaIA);

                Console.WriteLine($"🤖 Respuesta enviada a {numeroRemitente}: {respuestaIA}");
                return Ok();
            }
            catch (Exception ex)
            {
                // Si Green-API manda algo raro, lo atajamos acá para que no explote
                Console.WriteLine("Aviso: Formato de mensaje ignorado. " + ex.Message);
                return Ok();
            }
        }

        private async Task EnviarWhatsAppAsync(string numeroChatId, string mensaje)
        {
            // Tus claves de Green API (Ya las puse por vos)
            string idInstance = "7103525050";
            string apiTokenInstance = "97f6947c4156485892813fbcc53c033cac597c8a9a494c24ab";

            string url = $"https://api.green-api.com/waInstance{idInstance}/sendMessage/{apiTokenInstance}";

            using (HttpClient client = new HttpClient())
            {
                var payload = new
                {
                    chatId = numeroChatId, 
                    message = mensaje
                };

                string jsonPayload = JsonSerializer.Serialize(payload);
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                await client.PostAsync(url, content);
            }
        }
    }
}