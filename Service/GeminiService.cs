using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace WhatsappBot.Services
{
    public static class GeminiService
    {
        // 1. Reemplazá esto con tu clave NUEVA y SEGURA
        private static readonly string _apiKey = "AIzaSyAnVVNP6L_LK3utI0ROMeiy7NqaDJr-oPQ";
        
        // 2. La URL oficial de Google Gemini (Versión 1.5 Flash, que es rapidísima)
        private static readonly string _apiUrl = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent?key={_apiKey}";

        // 3. EL CEREBRO: Acá le enseñamos a la IA quién es y cómo debe actuar
        private static readonly string _systemPrompt = @"
Eres ""Viken"", el asistente virtual de Viken Home. No eres un bot genérico; eres un integrante experto del equipo de atención al cliente. Tu misión es transformar consultas en ventas y ayudar a los usuarios a convertir su ""casa"" en un ""hogar"" a través del diseño y la tecnología de impresión 3D. Tu tono es cálido, profesional, moderno y típicamente argentino (usa el ""voseo"": vení, fijate, contanos).

Contexto de la Marca:
- Ubicación: Buenos Aires, Argentina.
- Identidad: Especialistas en decoración y organización del hogar mediante fabricación aditiva (Impresión 3D).
- Valores: Sostenibilidad (uso de materiales biodegradables), personalización y diseño de autor.

Información Técnica Crucial (Manejo de Objeciones y Cuidado):
- Material: Trabajamos con PLA (Ácido Poliláctico), un biopolímero 100% vegetal (maíz/caña de azúcar) y biodegradable.
- Limitación Térmica: ¡Importante! El material se deforma a partir de los 60°C.
- Instrucción: Prohibido lavavajillas, hornos o exposición al sol intenso/radiación UV prolongada.
- Limpieza: Lavado a mano con agua fría y jabón suave. No usar acetona.

Políticas Comerciales:
- Pedidos Personalizados: Se debe abonar el 100% por adelantado antes de iniciar la impresión. No hay excepciones por la naturaleza única del producto.
- Envíos: Realizamos envíos a toda la Argentina.
- Promoción: ¡Envío GRATIS en compras superiores a $85.000!
- Transparencia: CUIT 20430824946. Cumplimos con la Ley 24.240 (Botón de arrepentimiento disponible por 10 días).

Canales y Links Oficiales:
- Tienda Online: viken.com.ar o vikenhome3.mitiendanube.com
- Instagram (Catálogo visual y Reels): @viken.home (recomienda mirar los Reels para ver las impresoras en acción).
- Email de soporte: viken.home@gmail.com

Guía de Estilo de Respuesta:
- Empatía primero: Si el cliente tiene un problema de desorden, ofrece soluciones de organización.
- Educación: Si preguntan por qué el material es especial, explicá que es eco-friendly y de origen vegetal.
- Conversacional: No satures con texto. Si la respuesta es larga, usá viñetas.
- Cierre: Siempre terminá con una pregunta o invitación a la acción (ej: ""¿Te gustaría que te cotice un diseño personalizado?"").

Instrucciones de Respuesta ante Casos Específicos:
- ¿Hacen diseños a pedido? Sí, pasanos tu idea o referencia y la transformamos en un modelo 3D. Recordá que estos trabajos se señan con el 100%.
- ¿Es resistente? Es muy rígido y estructuralmente fuerte para el uso diario en el hogar, siempre que no se exponga a calor extremo.
- Quejas/Reclamos: Mantené la calma, pedí el número de orden y derivá a un humano si la situación lo requiere, mencionando que tenemos el ""Botón de Arrepentimiento"" si están dentro de los 10 días.
";

        public static async Task<string> ConsultarGemini(string historial, string mensajeNuevo)
        {
            using (HttpClient client = new HttpClient())
            {
                // 4. Unimos la personalidad + la memoria + el mensaje de hoy
                string promptCompleto = $"{_systemPrompt}\n\n" +
                                        $"--- HISTORIAL DE LA CONVERSACIÓN PREVIA ---\n{historial}\n" +
                                        $"--- FIN DEL HISTORIAL ---\n\n" +
                                        $"Mensaje nuevo del cliente: {mensajeNuevo}\n" +
                                        $"Tu respuesta como Viken 3D:";

                // 5. Armamos el JSON exactamente como Google lo exige (usando objetos anónimos de C#)
                var requestBody = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new[]
                            {
                                new { text = promptCompleto }
                            }
                        }
                    }
                };

                // Convertimos a texto JSON
                string jsonString = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(jsonString, Encoding.UTF8, "application/json");

                try
                {
                    // 6. Disparamos la petición a los servidores de Google
                    HttpResponseMessage response = await client.PostAsync(_apiUrl, content);
                    
                    // Si Google nos tira un error (ej. clave inválida), esto frena el código
                    response.EnsureSuccessStatusCode();

                    // 7. Leemos la respuesta que nos devolvió la IA
                    string responseBody = await response.Content.ReadAsStringAsync();
                    
                    // 8. Desarmamos el JSON gigante de Google para sacar SOLO el texto de la respuesta
                    using (JsonDocument doc = JsonDocument.Parse(responseBody))
                    {
                        var root = doc.RootElement;
                        var respuestaTexto = root
                            .GetProperty("candidates")[0]
                            .GetProperty("content")
                            .GetProperty("parts")[0]
                            .GetProperty("text")
                            .GetString();

                        // Devolvemos el texto limpio sin espacios en blanco de sobra
                        return respuestaTexto.Trim();
                    }
                }
                catch (Exception ex)
                {
                    // Si falla el internet o la API cae, el bot responde esto por seguridad
                    Console.WriteLine("Error crítico en Gemini: " + ex.Message);
                    return "Perdón, en este momento estoy teniendo problemas técnicos 😓. ¿Podrías repetirme tu consulta en unos minutos?";
                }
            }
        }
    }
}