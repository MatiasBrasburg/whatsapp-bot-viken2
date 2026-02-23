using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;

public static class GeminiService
{
    // 1. ACÁ PONÉS TU CLAVE DE NUEVO
    private static readonly string _apiKey = ""; 
    
    // 2. URL de Gemini 2.5 Flash
    private static string GetApiUrl() => $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={_apiKey}";
    
    // 3. EL BOZAL MEJORADO (Regla 7 es la clave)
  private static readonly string _systemPrompt = @"Sos un vendedor de Viken Home (Impresión 3D ecológica en Buenos Aires). Tu nombre es Juan, Mariela, Agustina o Romina (elegí uno y mantenelo). 
REGLAS ESTRICTAS:
1. NUNCA escribas testamentos. MÁXIMO 2 o 3 oraciones cortas.
2. Comportate como un humano. Tono argentino de barrio pero profesional (usá el voseo: vení, fijate).
3. NUNCA te presentes diciendo 'Mi nombre es...' más de una vez. No pidas disculpas por errores técnicos.
4. Tu objetivo es ayudar y vender. Hacé UNA sola pregunta al final para mantener la charla viva.
5. PRIORIDAD ABSOLUTA: Enfocate ÚNICAMENTE en responder la ÚLTIMA intención del cliente.
6. ATENCIÓN HUMANA: Si el cliente acepta una compra, pide datos bancarios, precios finales o dice que quiere transferir, tu ÚNICA respuesta debe ser EXACTAMENTE el siguiente texto: [PASAR_A_HUMANO]. No agregues ni una sola palabra más, solo ese texto.";
    public static async Task<string> ConsultarGemini(string historial, List<string> urlAudios = null)
    {
        string historialSeguro = string.IsNullOrWhiteSpace(historial) ? "Sin historial." : historial;

        var partsList = new List<object>();
        
        // LA TRAMPA PARA LA IA: Le encerramos el historial entre corchetes y le damos la orden final abajo.
        string textoParaIA = $"[INICIO DEL HISTORIAL PARA DARTE CONTEXTO]\n{historialSeguro}\n[FIN DEL HISTORIAL]\n\nINSTRUCCIÓN OBLIGATORIA: Lee el historial, pero respondé EXCLUSIVAMENTE a los ÚLTIMOS mensajes del cliente. Ignorá cualquier problema de audio o confusión anterior.";
        
        partsList.Add(new { text = textoParaIA });

        if (urlAudios != null && urlAudios.Count > 0)
        {
            using (HttpClient clientAudio = new HttpClient())
            {
                foreach (var url in urlAudios)
                {
                    try
                    {
                        byte[] audioBytes = await clientAudio.GetByteArrayAsync(url);
                        partsList.Add(new {
                            inlineData = new {
                                mimeType = "audio/ogg",
                                data = Convert.ToBase64String(audioBytes) 
                            }
                        });
                        Console.WriteLine("🎤 ¡Audio extra procesado y sumado al paquete!");
                    }
                    catch (Exception ex) { Console.WriteLine($"❌ ERROR audio: {ex.Message}"); }
                }
            }
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
                if (!response.IsSuccessStatusCode) return "¡Hola! Estoy teniendo un problemita para procesar tu mensaje.";

                string responseBody = await response.Content.ReadAsStringAsync();
                using (JsonDocument doc = JsonDocument.Parse(responseBody))
                {
                    return doc.RootElement.GetProperty("candidates")[0]
                                          .GetProperty("content")
                                          .GetProperty("parts")[0]
                                          .GetProperty("text").GetString()?.Trim() ?? "Sin respuesta";
                }
            }
            catch (Exception)
            {
                return "Perdón, tuve un pequeño error técnico. ¿Me repetís?";
            }
        }
    }
}