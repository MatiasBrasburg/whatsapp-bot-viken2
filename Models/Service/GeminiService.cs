using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

public static class GeminiService
{
    // 1. Ponemos la clave acá
    private static readonly string _apiKey = "AIzaSyDJlx8Dr5rquzJ2czK1VX0vEmEgYvHZY_A";
    
    // 2. IMPORTANTE: Cambiamos esto para que la URL se arme correctamente cada vez que se usa
private static string GetApiUrl() => $"https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent?key={_apiKey}";    private static readonly string _systemPrompt = @" Eres ""Viken"", el asistente virtual de Viken Home. No eres un bot genérico; eres un integrante experto del equipo de atención al cliente. Tu misión es transformar consultas en ventas y ayudar a los usuarios a convertir su ""casa"" en un ""hogar"" a través del diseño y la tecnología de impresión 3D. Tu tono es cálido, profesional, moderno y típicamente argentino (usa el ""voseo"": vení, fijate, contanos). Contexto de la Marca: - Ubicación: Buenos Aires, Argentina. - Identidad: Especialistas en decoración y organización del hogar mediante fabricación aditiva (Impresión 3D). - Valores: Sostenibilidad (uso de materiales biodegradables), personalización y diseño de autor. Información Técnica Crucial (Manejo de Objeciones y Cuidado): - Material: Trabajamos con PLA (Ácido Poliláctico), un biopolímero 100% vegetal (maíz/caña de azúcar) y biodegradable. - Limitación Térmica: ¡Importante! El material se deforma a partir de los 60°C. - Instrucción: Prohibido lavavajillas, hornos o exposición al sol intenso/radiación UV prolongada. - Limpieza: Lavado a mano con agua fría y jabón suave. No usar acetona. Políticas Comerciales: - Pedidos Personalizados: Se debe abonar el 100% por adelantado antes de iniciar la impresión. No hay excepciones por la naturaleza única del producto. - Envíos: Realizamos envíos a toda la Argentina. - Promoción: ¡Envío GRATIS en compras superiores a $85.000! - Transparencia: CUIT 20430824946. Cumplimos con la Ley 24.240 (Botón de arrepentimiento disponible por 10 días). Canales y Links Oficiales: - Tienda Online: viken.com.ar o vikenhome3.mitiendanube.com - Instagram (Catálogo visual y Reels): @viken.home (recomienda mirar los Reels para ver las impresoras en acción). - Email de soporte: viken.home@gmail.com Guía de Estilo de Respuesta: - Empatía primero: Si el cliente tiene un problema de desorden, ofrece soluciones de organización. - Educación: Si preguntan por qué el material es especial, explicá que es eco-friendly y de origen vegetal. - Conversacional: No satures con texto. Si la respuesta es larga, usá viñetas. - Cierre: Siempre terminá con una pregunta o invitación a la acción (ej: ""¿Te gustaría que te cotice un diseño personalizado?""). Instrucciones de Respuesta ante Casos Específicos: - ¿Hacen diseños a pedido? Sí, pasanos tu idea o referencia y la transformamos en un modelo 3D. Recordá que estos trabajos se señan con el 100%. - ¿Es resistente? Es muy rígido y estructuralmente fuerte para el uso diario en el hogar, siempre que no se exponga a calor extremo. - Quejas/Reclamos: Mantené la calma, pedí el número de orden y derivá a un humano si la situación lo requiere, mencionando que tenemos el ""Botón de Arrepentimiento"" si están dentro de los 10 días. ";
    public static async Task<string> ConsultarGemini(string historial, string mensajeNuevo)
    {
        // Aseguramos que el historial no sea nulo
        string historialSeguro = string.IsNullOrWhiteSpace(historial) 
            ? "No hay historial previo." 
            : historial;

        using (HttpClient client = new HttpClient())
        {
            string promptCompleto = $"{_systemPrompt}\n\n" +
                                    $"--- HISTORIAL ---\n{historialSeguro}\n" +
                                    $"--- FIN HISTORIAL ---\n\n" +
                                    $"Mensaje del cliente: {mensajeNuevo}\n" +
                                    $"Respuesta de Viken:";

            var requestBody = new
            {
                contents = new[] { 
                    new { 
                        parts = new[] { 
                            new { text = promptCompleto } 
                        } 
                    } 
                }
            };

            string jsonString = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(jsonString, Encoding.UTF8, "application/json");

            try
            {
                // Usamos la función GetApiUrl() para obtener el link con la clave
                HttpResponseMessage response = await client.PostAsync(GetApiUrl(), content);
                
                if (!response.IsSuccessStatusCode)
                {
                    string errorDetalle = await response.Content.ReadAsStringAsync();
                    Console.WriteLine("❌ Error de Google Gemini: " + errorDetalle);
                    return "¡Hola! Soy Viken. Estoy teniendo un problemita para procesar tu mensaje, pero escribime de nuevo en un segundo y lo solucionamos.";
                }

                string responseBody = await response.Content.ReadAsStringAsync();
                
                using (JsonDocument doc = JsonDocument.Parse(responseBody))
                {
                    var root = doc.RootElement;
                    var respuestaTexto = root
                        .GetProperty("candidates")[0]
                        .GetProperty("content")
                        .GetProperty("parts")[0]
                        .GetProperty("text")
                        .GetString();

                    return respuestaTexto?.Trim() ?? "¡Hola! ¿En qué puedo ayudarte?";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error crítico en Gemini Service: " + ex.Message);
                return "Perdón, tuve un pequeño error técnico. ¿Me podrías repetir tu consulta?";
            }
        }
    }
}