using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using System;

using System.Net.Http;   // <--- Este arregla el error de HttpClient y StringContent
using System.Text;       // <--- Este arregla el error de Encoding
using System.Text.Json;

 


namespace WhatsappBot.Controllers
{
    [ApiController]
    [Route("api/whatsapp")] 
    public class WhatsappController : ControllerBase
    {
       

        [HttpPost]
        public async Task<IActionResult> ReceiveMessage([FromBody] WebhookPayload data)
        {
            // 1. Extraemos los datos básicos limpiando espacios en blanco
            string numeroRemitente = data.Telefono;
            string textoMensaje = data.Mensaje?.Trim() ?? "";
            bool loMandeYo = data.FromMe;

            // =======================================================
            // FASE 1: EL INTERCEPTOR (ADMINISTRACIÓN DEL DUEÑO)
            // =======================================================
            if (loMandeYo == true)
            {
                if (textoMensaje == "APAGAR_BOT")
                {
                    // Apagamos para este cliente en particular (o podés hacer una función global)
                    BD.CambiarEstadoBot(numeroRemitente); 
                    Console.WriteLine("🛑 Bot APAGADO " );
                    return Ok(); 
                }
                else if (textoMensaje == "PRENDER_BOT")
                {
                    BD.CambiarEstadoBot(numeroRemitente);
                    Console.WriteLine("✅ Bot PRENDIDO " );
                    return Ok(); 
                }
                else
                {
                    // Si vos hablás normal desde tu celular, el bot asume que tomaste el control
                    // y se apaga automáticamente para no pisarte. (Opcional, pero muy recomendado)
                    bool estadoActual = BD.TraerEstadoBot(numeroRemitente);
                    if (estadoActual == true) 
                    {
                        BD.CambiarEstadoBot(numeroRemitente); // Lo apaga
                    }
                    return Ok();
                }
            }

            // =======================================================
            // FASE 2: ATENCIÓN AL CLIENTE
            // =======================================================
            
            // Si llegamos acá, el mensaje es de un cliente real. Lo registramos por las dudas.
            BD.RegistrarCliente(numeroRemitente);

            // Verificamos si el bot está prendido para él
            bool botActivo = BD.TraerEstadoBot(numeroRemitente);
            if (botActivo == false)
            {
                // El bot está silenciado para este número. Ignoramos el mensaje.
                return Ok();
            }

            // GUARDAMOS EL MENSAJE DEL CLIENTE EN LA BD
            BD.GuardarMensajeEnBD(numeroRemitente, textoMensaje, false);

            // =======================================================
            // FASE 3: EL CEREBRO (GEMINI) Y LA MEMORIA
            // =======================================================
            
            // Traemos los últimos 10 mensajes para que la IA tenga contexto
            string historial = BD.ObtenerHistorialChat(numeroRemitente);

            // ¡Magia! Llamamos a Google
            string respuestaIA = await GeminiService.ConsultarGemini(historial, textoMensaje);

            // GUARDAMOS LA RESPUESTA DE LA IA EN LA BD
            BD.GuardarMensajeEnBD(numeroRemitente, respuestaIA, true);

            // =======================================================
            // FASE 4: SIMULACIÓN HUMANA Y ENVÍO
            // =======================================================
            
            // Calculamos cuánto tardaría un humano en escribir esto (Ej: 30 milisegundos por letra)
            int tiempoTipeo = respuestaIA.Length * 30;
            // Ponemos un límite para que no se quede esperando 1 minuto si el texto es muy largo
            if (tiempoTipeo > 8000) tiempoTipeo = 8000; 
            
            await Task.Delay(tiempoTipeo);

           

            Console.WriteLine($"🤖 Respuesta enviada a {numeroRemitente}: {respuestaIA}");

            return Ok();
        }
private async Task EnviarWhatsAppAsync(string numeroChatId, string mensaje)
        {
            // PEGA ACÁ TUS CLAVES DE GREEN API
            string idInstance = "7103525050";
            string apiTokenInstance = "97f6947c4156485892813fbcc53c033cac597c8a9a494c24ab";

            string url = $"https://api.green-api.com/waInstance{idInstance}/sendMessage/{apiTokenInstance}";

            using (HttpClient client = new HttpClient())
            {
                var payload = new
                {
                    chatId = numeroChatId, // Green-API necesita que termine en @c.us
                    message = mensaje
                };

                string jsonPayload = JsonSerializer.Serialize(payload);
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                await client.PostAsync(url, content);
            }
        }
      
    }
}