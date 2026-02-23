using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

public static class GeminiService
{
    // 1. ACÁ PONÉS TU CLAVE DE API DE GOOGLE STUDIO
 
    
    // 2. ACÁ VA LA URL (No se toca, ya toma la clave de arriba)
   // 2. ACÁ VA LA URL (Le agregamos -latest al nombre del modelo)
// 2. ACÁ VA LA URL (Actualizada al modelo Gemini 2.5 Flash que sí está activo)
private static string GetApiUrl() => $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={_apiKey}";
    
    // 3. ACÁ PONÉS EL PROMPT (La personalidad del bot, lo que tenías armado de Viken)
   private static readonly string _systemPrompt = @"Sos 'Viken', el asistente virtual experto de Viken Home (Impresión 3D ecológica en Buenos Aires).
REGLAS ESTRICTAS E INQUEBRANTABLES:
1. NUNCA escribas testamentos. Tus respuestas deben tener MÁXIMO 2 o 3 oraciones cortas.
2. Comportate como un humano chateando en WhatsApp. Si el usuario escribe corto, respondé corto.
3. NUNCA te presentes diciendo 'Mi nombre es...' más de una vez. Ya saben que sos Viken. No pidas disculpas por errores de sistema.
4. Tono: Argentino de barrio pero súper profesional (usá el voseo: vení, fijate, contanos).
5. Tu objetivo es ayudar y vender, pero despacio. Hacé UNA sola pregunta al final para mantener la charla viva, no abrumes al cliente con opciones si no las pidió.
6. Si el usuario te mandó varios mensajes cortos seguidos (los verás en el historial), respondé a la idea general con un solo mensaje unificado.";

    // 4. EL MOTOR NUEVO QUE TE PASÉ RECIÉN
   // Le agregamos el parámetro "urlAudio" a la firma
    public static async Task<string> ConsultarGemini(string historial, string urlAudio = null)
    {
        string historialSeguro = string.IsNullOrWhiteSpace(historial) ? "Sin historial previo." : historial;

        // Armamos el JSON dinámicamente. Primero va el texto (el historial completo).
        var partsList = new System.Collections.Generic.List<object>();
        partsList.Add(new { text = $"Historial:\n{historialSeguro}\n\nRespondé unificando la idea." });

        // Si hay un link de audio, lo descargamos y lo enchufamos
        if (!string.IsNullOrWhiteSpace(urlAudio))
        {
            try
            {
                using (HttpClient clientAudio = new HttpClient())
                {
                    byte[] audioBytes = await clientAudio.GetByteArrayAsync(urlAudio);
                    partsList.Add(new {
                        inline_data = new {
                            mime_type = "audio/ogg", // Formato de WhatsApp
                            data = Convert.ToBase64String(audioBytes) // A Gemini le gusta así
                        }
                    });
                }
            }
            catch { Console.WriteLine("❌ Error descargando el audio."); }
        }

        var requestBody = new
        {
            system_instruction = new { parts = new[] { new { text = _systemPrompt } } },
            contents = new[] { new { parts = partsList.ToArray() } }
        };

        string jsonString = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(jsonString, Encoding.UTF8, "application/json");

        using (HttpClient client = new HttpClient())
        {
            try
            {
                HttpResponseMessage response = await client.PostAsync(GetApiUrl(), content);
                
                if (!response.IsSuccessStatusCode)
                {
                    string errorGoogle = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"\n❌ EXPLOTÓ GEMINI: {response.StatusCode}\nMotivo: {errorGoogle}\n");
                    return "¡Hola! Estoy teniendo un problemita para procesar tu mensaje.";
                }

                string responseBody = await response.Content.ReadAsStringAsync();
                using (JsonDocument doc = JsonDocument.Parse(responseBody))
                {
                    return doc.RootElement.GetProperty("candidates")[0]
                                          .GetProperty("content")
                                          .GetProperty("parts")[0]
                                          .GetProperty("text").GetString()?.Trim() ?? "Sin respuesta";
                }
            }
            catch (Exception ex)
            {
                return "Perdón, tuve un pequeño error técnico. ¿Me repetís?";
            }
        }
    }
}