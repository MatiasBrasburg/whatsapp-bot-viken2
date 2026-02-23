using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq; // CLAVE PARA CONVERTIR LA COLA EN LISTA

namespace WhatsappBot.Controllers
{
    [ApiController]
    [Route("api/whatsapp")]
    public class WhatsappController : ControllerBase
    {
        private static ConcurrentDictionary<string, bool> _procesandoChat = new();
        // AHORA ES UNA COLA (CANASTA) PARA GUARDAR VARIOS AUDIOS
        private static ConcurrentDictionary<string, ConcurrentQueue<string>> _audiosPendientes = new();

        [HttpPost]
        public IActionResult ReceiveMessage([FromBody] JsonElement payloadBruto)
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
                    textoMensaje = "[El usuario envió un mensaje de audio]";
                }
                else return Ok(); 

                string numeroRemitenteCompleto = payloadBruto.GetProperty("senderData").GetProperty("sender").GetString() ?? "";
                string numeroRemitente = numeroRemitenteCompleto.Replace("@c.us", ""); 
                textoMensaje = textoMensaje.Trim();

                if (tipoMensaje == "outgoingMessageReceived") return Ok(); 

                BD.RegistrarCliente(numeroRemitente);
                if (BD.TraerEstadoBot(numeroRemitente) == false) return Ok();

                BD.GuardarMensajeEnBD(numeroRemitente, textoMensaje, false);
                
                // ATAJAMOS MULTIPLES AUDIOS EN LA COLA
                if (!string.IsNullOrEmpty(urlAudio))
                {
                    var colaAudios = _audiosPendientes.GetOrAdd(numeroRemitente, _ => new ConcurrentQueue<string>());
                    colaAudios.Enqueue(urlAudio);
                }

                if (_procesandoChat.TryGetValue(numeroRemitente, out bool estaProcesando) && estaProcesando)
                {
                    Console.WriteLine("⏳ Entró otro mensaje/audio. Seguimos esperando los 40s...");
                    return Ok(); 
                }

                _procesandoChat[numeroRemitente] = true;
                Console.WriteLine("⏳ PRIMER MENSAJE. Lanzando cronómetro de 40s...");

                _ = Task.Run(async () => 
                {
                    await Task.Delay(40000); 

                    string historial = BD.ObtenerHistorialChat(numeroRemitente);
                    
                    // SACAMOS TODOS LOS AUDIOS JUNTOS Y VACIAMOS LA CANASTA
                    _audiosPendientes.TryRemove(numeroRemitente, out var audiosExtraidos);
                    List<string> listaAudios = audiosExtraidos != null ? audiosExtraidos.ToList() : new List<string>();

                    Console.WriteLine($"🤖 Pasaron 40s. Consultando a Gemini con {listaAudios.Count} audios...");
                    string respuestaIA = await GeminiService.ConsultarGemini(historial, listaAudios);


                    // --- 🚨 MAGIA SAAS: EL PASE A HUMANO 🚨 ---
                    if (respuestaIA.Contains("[PASAR_A_HUMANO]"))
                    {
                        Console.WriteLine("💰 ¡OLOR A PLATA! Apagando bot y avisando al dueño...");
                        
                        // 1. Apagamos el bot para este cliente
                        BD.CambiarEstadoBot(numeroRemitente); 
                        
                        // 2. Le mandamos el mensaje elegante al cliente
                        string mensajeCliente = "¡Excelente! Ya dejé todo anotado. Te paso con un asesor humano para que te pase los datos de pago y coordine el envío con vos. ¡En un ratito te escribe!";
                        await EnviarWhatsAppAsync(numeroRemitenteCompleto, mensajeCliente);
                        
                        // 3. TE AVISAMOS A VOS (Reemplazá por tu número con el formato de Green API)
                        string tuNumero = "5491155841206@c.us"; // <-- ACÁ PONÉ EL NÚMERO DEL DUEÑO DEL LOCAL
                        string mensajeDueño = $"🚨 *¡ALERTA DE VENTA!*\nEl número {numeroRemitente} quiere pagar o cerrar pedido. El bot ya se apagó solo. ¡Entrá al WhatsApp y pasale el Alias, campeón!";
                        await EnviarWhatsAppAsync(tuNumero, mensajeDueño);

                        // 4. Guardamos en la base de datos y limpiamos
                        BD.GuardarMensajeEnBD(numeroRemitente, mensajeCliente, true);
                        _procesandoChat[numeroRemitente] = false;
                        return; // 🛑 Cortamos la ejecución para que no haga más nada
                    }
                    // ------------------------------------------

                    // Si no es una venta, el código sigue normal
                    BD.GuardarMensajeEnBD(numeroRemitente, respuestaIA, true);
                    _procesandoChat[numeroRemitente] = false; 

                    await EnviarWhatsAppAsync(numeroRemitenteCompleto, respuestaIA);
                    Console.WriteLine($"✅ ¡ÉXITO! Respuesta unificada enviada a {numeroRemitente}.");
                    BD.GuardarMensajeEnBD(numeroRemitente, respuestaIA, true);
                    _procesandoChat[numeroRemitente] = false; 

                    await EnviarWhatsAppAsync(numeroRemitenteCompleto, respuestaIA);
                    Console.WriteLine($"✅ ¡ÉXITO! Respuesta unificada enviada a {numeroRemitente}.");
                });

                return Ok(); 
            }
            catch (Exception ex)
            {
                Console.WriteLine("❌ ERROR CRÍTICO DETECTADO: " + ex.Message);
                return Ok();
            }
        }
        
        // ... (Tu método EnviarWhatsAppAsync queda igual abajo de esto)
        
        






   

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