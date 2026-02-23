using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;

public static class GeminiService
{
    // Ahora la va a ir a buscar al archivo .env mágicamente
    private static readonly string _apiKey = Environment.GetEnvironmentVariable("GEMINI_KEY");
    
    // 2. URL de Gemini 2.5 Flash
    private static string GetApiUrl() => $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={_apiKey}";
    
    // 3. EL BOZAL MEJORADO (Regla 7 es la clave)
    private static readonly string _systemPrompt = @"Sos un asesor de ventas experto de Viken Home (Impresión 3D ecológica en Buenos Aires). Tu nombre es Juan, Mariela, Agustina o Romina (elegí uno y presentate solo una vez).

=== BASE DE CONOCIMIENTO Y POLÍTICAS DE LA EMPRESA ===
- Identidad: Transformamos 'casas' en 'hogares' con diseño de autor y tecnología de impresión 3D.
- Web Oficial: vikenhome3.mitiendanube.com
- Materiales: Usamos PLA, un bioplástico 100% vegetal (maíz/caña) y biodegradable. La textura FDM (líneas de impresión) es parte de nuestra estética.
- Cuidados Críticos: El material se deforma a 60°C. NO es apto para lavavajillas, microondas, ni sol directo permanente. Limpiar solo con paño, agua fría y jabón suave. Prohibido el uso de acetona.
- Envíos: A toda Argentina. ¡Envío GRATIS superando los $85.000!
- Trabajos Personalizados: Requieren el pago del 100% por adelantado sin excepción.
- Legales: CUIT 20430824946. Contamos con 'Botón de Arrepentimiento' (10 días) por ley.

=== MINI-CATÁLOGO (Tus productos y precios de referencia) ===
- Categorías principales: Cocina/Baño (solo agua fría), Organización, Iluminación y Decoración General.
- [EJEMPLO] Maquetas arquitectónicas: Desde $15.000 (depende escala y detalles).
- [EJEMPLO] Organizadores de escritorio: Desde $8.000.
- [EJEMPLO] Floreros decorativos (no aptos agua caliente): Desde $12.000.
(Nota para la IA: Si el cliente pide algo que no está acá, decile que lo podemos diseñar a medida o mandalo a revisar la tienda online vikenhome3.mitiendanube.com).

=== REGLAS ESTRICTAS E INQUEBRANTABLES ===
1. BREVEDAD EXTREMA: NUNCA escribas testamentos. Tus respuestas deben tener MÁXIMO 2 o 3 oraciones cortas.
2. TONO: Argentino, cálido y profesional. Usá el voseo (vení, fijate, tenés, contanos).
3. AMNESIA POSITIVA (PRIORIDAD): Ignorá cualquier error o confusión del pasado en el historial. Enfocate y respondé ÚNICAMENTE a la última intención del cliente.
4. FLUJO DE VENTA: Terminá tu mensaje con UNA sola pregunta corta para mantener la charla viva y guiar al cliente al cierre.
5. 🚨 COMANDO SECRETO DE VENTA: Si el cliente confirma la compra, acepta un presupuesto... tu ÚNICA respuesta debe ser: [PASAR_A_HUMANO].
6. 👻 COMANDO VISTO: Si el último mensaje del cliente es solo un agradecimiento corto ('gracias', 'ok', 'dale', 'perfecto') o un cierre de conversación que NO requiere respuesta, tu ÚNICA respuesta debe ser EXACTAMENTE este texto: [IGNORAR].
7. 🏷️ ETIQUETADO DE DATOS: Al final de tu respuesta (incluso si usas [PASAR_A_HUMANO] o [IGNORAR]), agrega SIEMPRE una etiqueta oculta con el tema principal de la charla. Formato exacto: [CAT: Tema]. 
Ejemplos de temas: [CAT: Floreros], [CAT: Macetas], [CAT: Envíos], [CAT: Precios], [CAT: Personalizados], [CAT: Otro].
Ejemplo tuyo: '¡Hola! Sí, hacemos envíos gratis a partir de $85.000. [CAT: Envíos]'";

    // AGREGADO: Recibe List<string> urlImagenes
    public static async Task<string> ConsultarGemini(string historial, List<string> urlAudios = null, List<string> urlImagenes = null)
    {
        string historialSeguro = string.IsNullOrWhiteSpace(historial) ? "Sin historial previo." : historial;

        var partsList = new List<object>();
        
        string textoParaIA = $"[INICIO DEL HISTORIAL]\n{historialSeguro}\n[FIN DEL HISTORIAL]\n\nINSTRUCCIÓN: Lee el historial, observá las fotos si hay, y respondé ÚNICAMENTE a los últimos mensajes.";
        partsList.Add(new { text = textoParaIA });

        // PROCESAMOS AUDIOS
        if (urlAudios != null && urlAudios.Count > 0)
        {
            using (HttpClient clientAudio = new HttpClient())
            {
                foreach (var url in urlAudios)
                {
                    try
                    {
                        byte[] audioBytes = await clientAudio.GetByteArrayAsync(url);
                        partsList.Add(new { inlineData = new { mimeType = "audio/ogg", data = Convert.ToBase64String(audioBytes) } });
                    }
                    catch (Exception ex) { Console.WriteLine($"❌ ERROR descargando audio: {ex.Message}"); }
                }
            }
        }

        // PROCESAMOS FOTOS
        if (urlImagenes != null && urlImagenes.Count > 0)
        {
            using (HttpClient clientImg = new HttpClient())
            {
                foreach (var url in urlImagenes)
                {
                    try
                    {
                        byte[] imgBytes = await clientImg.GetByteArrayAsync(url);
                        partsList.Add(new { inlineData = new { mimeType = "image/jpeg", data = Convert.ToBase64String(imgBytes) } });
                        Console.WriteLine("📸 ¡Foto descargada y empaquetada para Gemini!");
                    }
                    catch (Exception ex) { Console.WriteLine($"❌ ERROR descargando foto: {ex.Message}"); }
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
                    return doc.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString()?.Trim() ?? "Sin respuesta";
                }
            }
            catch (Exception ex)
            {
                return "Perdón, tuve un pequeño error técnico. ¿Me repetís?";
            }
        }
    }
}