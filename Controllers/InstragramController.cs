using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq; 
using System.Text.RegularExpressions; 

namespace WhatsappBot.Controllers
{
    [ApiController]
    [Route("api/instagram")]
    public class InstagramController : ControllerBase
    {
        private static ConcurrentDictionary<string, bool> _procesandoChat = new();
        private static ConcurrentDictionary<string, ConcurrentQueue<string>> _audiosPendientes = new();
        private static ConcurrentDictionary<string, ConcurrentQueue<string>> _imagenesPendientes = new(); 

        // --- 🛡️ EL PATOVICA DE META ---
        [HttpGet]
        public IActionResult VerificarWebhook([FromQuery(Name = "hub.mode")] string mode, 
                                              [FromQuery(Name = "hub.verify_token")] string token, 
                                              [FromQuery(Name = "hub.challenge")] string challenge)
        {
            string miTokenSecreto = "viken_elissir_123"; // Mismo que ponés en Meta for Developers
            if (mode == "subscribe" && token == miTokenSecreto) return Content(challenge);
            return Unauthorized();
        }

        [HttpPost]
        public IActionResult ReceiveMessage([FromBody] JsonElement payloadBruto)
        {
            try
            {
                // Si no es un mensaje normal de Instagram, lo fletamos con 200 OK rápido
                if (!payloadBruto.TryGetProperty("entry", out JsonElement entryArray) || entryArray.GetArrayLength() == 0) return Ok("EVENT_RECEIVED");
                var entry = entryArray[0];
                
                if (!entry.TryGetProperty("messaging", out JsonElement messagingArray) || messagingArray.GetArrayLength() == 0) return Ok("EVENT_RECEIVED");
                var messaging = messagingArray[0];

                // Filtramos si es confirmación de lectura u otra cosa que no sea un mensaje
                if (!messaging.TryGetProperty("message", out JsonElement messageData)) return Ok("EVENT_RECEIVED");

                string numeroRemitente = messaging.GetProperty("sender").GetProperty("id").GetString() ?? ""; // En IG es un ID numérico largo, no un teléfono
                string textoMensaje = "";
                string urlImagen = "";
                string urlAudio = "";

                if (messageData.TryGetProperty("text", out JsonElement textElement))
                {
                    textoMensaje = textElement.GetString() ?? "";
                }
                else if (messageData.TryGetProperty("attachments", out JsonElement attachments))
                {
                    // Atajamos imágenes o audios de Instagram
                    var attachment = attachments[0];
                    string tipoAttachment = attachment.GetProperty("type").GetString() ?? "";
                    string payloadUrl = attachment.GetProperty("payload").GetProperty("url").GetString() ?? "";

                    if (tipoAttachment == "image")
                    {
                        urlImagen = payloadUrl;
                        textoMensaje = "[El usuario envió una imagen de referencia]";
                    }
                    else if (tipoAttachment == "audio")
                    {
                        urlAudio = payloadUrl;
                        textoMensaje = "[El usuario envió un mensaje de audio]";
                    }
                }

                textoMensaje = textoMensaje.Trim();
                if (string.IsNullOrEmpty(textoMensaje)) return Ok("EVENT_RECEIVED");

                // --- 👑 COMANDOS DE ADMIN (Solo para vos) ---
                // OJO: Acá tendrías que poner TU ID de Instagram, no tu número de teléfono
                string tuIDInstagram = "ACA_PONE_TU_ID_DE_IG"; 
                if (numeroRemitente == tuIDInstagram)
                {
                    if (textoMensaje.ToLower() == "activar_reporte")
                    {
                        BD.ConfigurarReporte(true);
                        _ = EnviarInstagramAsync(numeroRemitente, "✅ *Reporte diario ACTIVADO.*");
                        return Ok("EVENT_RECEIVED"); 
                    }
                    else if (textoMensaje.ToLower() == "desactivar_reporte")
                    {
                        BD.ConfigurarReporte(false);
                        _ = EnviarInstagramAsync(numeroRemitente, "❌ *Reporte diario DESACTIVADO.*");
                        return Ok("EVENT_RECEIVED");
                    }
                }
                // -------------------------------------------------

                // --- 🌙 CHICHE: HORARIO COMERCIAL ---
                DateTime horaArg = DateTime.UtcNow.AddHours(-3); 
                if (horaArg.Hour < 9 || horaArg.Hour >= 20) 
                {
                    Console.WriteLine($"🌙 Fuera de hora ({horaArg.Hour}hs). El bot se hace el dormido con {numeroRemitente} en IG.");
                    BD.GuardarMensajeEnBD(numeroRemitente, textoMensaje, false);
                    return Ok("EVENT_RECEIVED"); 
                }
                // ------------------------------------

                BD.RegistrarCliente(numeroRemitente);
                if (BD.TraerEstadoBot(numeroRemitente) == false) return Ok("EVENT_RECEIVED");

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
                    Console.WriteLine("⏳ Entró otro mensaje en IG. Seguimos esperando...");
                    return Ok("EVENT_RECEIVED"); 
                }

                _procesandoChat[numeroRemitente] = true;
                Console.WriteLine("⏳ PRIMER MENSAJE IG. Lanzando proceso en segundo plano...");

                _ = Task.Run(async () => 
                {
                    string mjeEspera = "👀 _Dame un segundito que estoy leyendo lo que me mandaste y ya te respondo..._";
                    await EnviarInstagramAsync(numeroRemitente, mjeEspera);

                    Random rnd = new Random();
                    int tiempoEsperaRandom = rnd.Next(40000, 360000); 
                    
                    Console.WriteLine($"🎲 [Modo Humano IG] Esperando {tiempoEsperaRandom / 1000} segundos...");
                    await Task.Delay(tiempoEsperaRandom);

                    string historial = BD.ObtenerHistorialChat(numeroRemitente);
                    
                    _audiosPendientes.TryRemove(numeroRemitente, out var audiosExtraidos);
                    List<string> listaAudios = audiosExtraidos != null ? audiosExtraidos.ToList() : new List<string>();

                    _imagenesPendientes.TryRemove(numeroRemitente, out var imagenesExtraidas);
                    List<string> listaImagenes = imagenesExtraidas != null ? imagenesExtraidas.ToList() : new List<string>();

                    Console.WriteLine($"🤖 Terminó la espera en IG. Consultando a Gemini...");
                    string respuestaIA = await GeminiService.ConsultarGemini(historial, listaAudios, listaImagenes);

                    // --- 🏷️ CHICHE: EXTRAER CATEGORÍA EN SECRETO ---
                    string categoriaDetectada = null;
                    var match = Regex.Match(respuestaIA, @"\[CAT:\s*(.*?)\]");
                    if (match.Success)
                    {
                        categoriaDetectada = match.Groups[1].Value.Trim();
                        respuestaIA = respuestaIA.Replace(match.Value, "").Trim(); 
                    }
                    // ------------------------------------------------

                    // --- 🚨 MAGIA SAAS: EL PASE A HUMANO 🚨 ---
                    if (respuestaIA.Contains("APAGAR_BOT") || respuestaIA.Contains("[PASAR_A_HUMANO]"))
                    {
                        Console.WriteLine("💰 ¡OLOR A PLATA EN IG! Apagando bot...");
                        BD.CambiarEstadoBot(numeroRemitente); 
                        
                        string mensajeCliente = "¡Excelente! Ya dejé todo anotado. Te paso con un asesor humano para que te pase los datos de pago. ¡En un ratito te escribe!";
                        await EnviarInstagramAsync(numeroRemitente, mensajeCliente);
                        
                        // 🌟 ACÁ MANGUEAMOS EL NOMBRE Y ARMAMOS EL MENSAJE COMO PEDISTE 🌟
                        string nombreIG = await ObtenerNombreUsuarioIG(numeroRemitente);
                        string detalleProductos = respuestaIA.Replace("APAGAR_BOT", "").Replace("[PASAR_A_HUMANO]", "").Trim();
                        string categoriaMsj = string.IsNullOrEmpty(categoriaDetectada) ? "un producto/servicio" : categoriaDetectada;

                        string tuNumeroReporte = "5491155841206@c.us"; 
                        string mensajeDueño = $"🚨 *¡ALERTA DE VENTA POR IG!*\nChe, el usuario: *{nombreIG}* está por hacer una venta de *{categoriaMsj}* y necesita un humano, andá cuando puedas.\n\n📝 *Detalle/Productos:* {detalleProductos}";
                        
                        await EnviarWhatsAppNotificacionAsync(tuNumeroReporte, mensajeDueño);

                        BD.GuardarMensajeEnBD(numeroRemitente, mensajeCliente, true, categoriaDetectada);
                        _procesandoChat[numeroRemitente] = false;
                        return; 
                    }
                    // ------------------------------------------

                    BD.GuardarMensajeEnBD(numeroRemitente, respuestaIA, true, categoriaDetectada);
                    _procesandoChat[numeroRemitente] = false; 

                    await EnviarInstagramAsync(numeroRemitente, respuestaIA);
                    Console.WriteLine($"✅ ¡ÉXITO! Respuesta enviada por IG.");
                });

                return Ok("EVENT_RECEIVED"); 
            }
            catch (Exception ex)
            {
                Console.WriteLine("❌ ERROR CRÍTICO EN IG: " + ex.Message);
                return Ok("EVENT_RECEIVED");
            }
        }

        // --- 🕵️‍♂️ FUNCIÓN NUEVA: BUSCAR NOMBRE DE USUARIO IG ---
        private async Task<string> ObtenerNombreUsuarioIG(string psid)
        {
            try
            {
                string token = Environment.GetEnvironmentVariable("META_PAGE_TOKEN");
                string url = $"https://graph.facebook.com/v18.0/{psid}?fields=name,username&access_token={token}";
                
                using (HttpClient client = new HttpClient())
                {
                    var res = await client.GetAsync(url);
                    if (!res.IsSuccessStatusCode) return $"ID: {psid}"; // Si falla, te manda el número para zafar
                    
                    using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
                    // Busca el nombre real o el @usuario
                    return doc.RootElement.TryGetProperty("name", out var n) ? n.GetString() : doc.RootElement.GetProperty("username").GetString();
                }
            }
            catch 
            { 
                return $"ID: {psid}"; 
            }
        }

        // --- EL MOTOR DE ENVÍO DE INSTAGRAM ---
        private async Task EnviarInstagramAsync(string senderId, string mensaje)
        {
            string pageAccessToken = Environment.GetEnvironmentVariable("META_PAGE_TOKEN");
            string url = $"https://graph.facebook.com/v18.0/me/messages?access_token={pageAccessToken}";

            using (HttpClient client = new HttpClient())
            {
                var payload = new 
                { 
                    recipient = new { id = senderId }, 
                    message = new { text = mensaje } 
                };
                string jsonPayload = JsonSerializer.Serialize(payload);
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                await client.PostAsync(url, content);
            }
        }

        // --- MOTORCITO AUXILIAR PARA AVISARTE A VOS POR WHATSAPP ---
        private async Task EnviarWhatsAppNotificacionAsync(string numeroChatId, string mensaje)
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