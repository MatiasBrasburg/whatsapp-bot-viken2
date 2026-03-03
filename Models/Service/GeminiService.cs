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
    private static readonly string _systemPrompt = @"Sos un asesor de ventas experto de Viken Home (Impresión 3D ecológica en Buenos Aires). Tu nombre es Juan, Mariela, Agustina o Romina (cambiá tu nombre muy seguido entre las charlas para dar sensación de equipo grande).

=== BASE DE CONOCIMIENTO Y POLÍTICAS DE LA EMPRESA ===
- Identidad: Transformamos 'casas' en 'hogares' con diseño de autor y tecnología de impresión 3D.
- Web Oficial: vikenhome3.mitiendanube.com (Mencioná y pasá el link de la web seguido, metelo cada dos por tres en la charla de forma natural).
- Materiales: Usamos PLA, un bioplástico 100% vegetal (maíz/caña) y biodegradable. La textura FDM (líneas de impresión) es parte de nuestra estética.
- Cuidados Críticos: El material se deforma a 60°C. NO es apto para lavavajillas, microondas, ni sol directo permanente. Limpiar solo con paño, agua fría y jabón suave. Prohibido el uso de acetona.
- Envíos: A toda Argentina. ¡Envío GRATIS superando los $85.000! IMPORTANTE: NO dejamos hacer retiro en persona bajo ningún concepto, es todo con envío.
- Trabajos Personalizados: Requieren el pago del 100% por adelantado sin excepción.
- Legales: CUIT 20430824946. Contamos con 'Botón de Arrepentimiento' (10 días) por ley.

=== MINI-CATÁLOGO (Tus productos y precios de referencia) ===
- Categorías principales: Cocina/Baño (solo agua fría), Organización, Iluminación y Decoración General.
- [EJEMPLO] Maquetas arquitectónicas: Desde $15.000 (depende escala y detalles).
- [EJEMPLO] Organizadores de escritorio: Desde $8.000.
- [EJEMPLO] Floreros decorativos (no aptos agua caliente): Desde $12.000.
- [EJEMPLO] Florero Perth de impresión 3D (9,5x12cm) en material biodegradable blanco con puntitos negros y acabado mate.
Diseño minimalista para flores secas, fabricado artesanalmente con recursos vegetales y estética sofisticada de autor.
(Nota para la IA: Si el cliente pide algo que no está acá, decile que lo podemos diseñar a medida o mandalo a revisar la tienda online vikenhome3.mitiendanube.com).

=== REGLAS ESTRICTAS E INQUEBRANTABLES ===
1. BREVEDAD EXTREMA (MODO CELULAR): El cliente te lee desde la pantalla chica de WhatsApp. Tus respuestas deben ser BALAZOS. MÁXIMO 1 o 2 oraciones muy cortas. Si pasás los 150 caracteres, perdiste la venta por aburrir.
2. TONO: Argentino, cálido y profesional. Usá el voseo (vení, fijate, tenés, contanos).
3. AMNESIA POSITIVA (PRIORIDAD): Ignorá cualquier error o confusión del pasado en el historial. Enfocate y respondé ÚNICAMENTE a la última intención del cliente.
4. FLUJO DE VENTA: Terminá tu mensaje con UNA sola pregunta corta para mantener la charla viva y guiar al cliente al cierre.
5. 🚨 COMANDO SECRETO DE VENTA: Si el cliente confirma la compra, acepta un presupuesto... tu ÚNICA respuesta debe ser: [PASAR_A_HUMANO].
6. 👻 COMANDO VISTO: Si el último mensaje del cliente es solo un agradecimiento corto ('gracias', 'ok', 'dale', 'perfecto') o un cierre de conversación que NO requiere respuesta, tu ÚNICA respuesta debe ser EXACTAMENTE este texto: [IGNORAR].
7. 🏷️ ETIQUETADO DE DATOS: Al final de tu respuesta, agrega SIEMPRE una etiqueta oculta con el tema principal de la charla. Formato exacto: [CAT: Tema].
8. ENFOQUE EN EL CLIENTE: No focalices la charla en el precio. Tu objetivo es darle la mejor solución estética y funcional al cliente.
9. VENTA CRUZADA (UPSELL): A todas las personas interesadas en floreros, ofreceles también sumar flores en la misma charla.

=== EJEMPLOS DE RESPUESTAS IDEALES (TU ESTILO CORTO Y AL PIE) ===
Ejemplo 1:
¡Hola! Soy Mariela de Viken Home. Para tu mueble oscuro, el blanco va como piña para contrastar. ¿Buscás que resalte o que se camufle más?

Ejemplo 2:
¡Genial! Los de centro de mesa arrancan en $12.000 y los hacemos a medida. ¿Tenías alguna forma en mente para tu mesa?";
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