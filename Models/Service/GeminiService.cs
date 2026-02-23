using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

public static class GeminiService
{
    // 1. ACÁ PONÉS TU CLAVE DE API DE GOOGLE STUDIO
    private static readonly string _apiKey = "AIzaSyCPJ2780E58Gl--inMFnpbuspO87zwu2uc";
    
    // 2. ACÁ VA LA URL (No se toca, ya toma la clave de arriba)
   // 2. ACÁ VA LA URL (Le agregamos -latest al nombre del modelo)
// 2. ACÁ VA LA URL (Actualizada al modelo Gemini 2.5 Flash que sí está activo)
private static string GetApiUrl() => $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={_apiKey}";
    
    // 3. ACÁ PONÉS EL PROMPT (La personalidad del bot, lo que tenías armado de Viken)
   private static readonly string _systemPrompt = @" Eres una persona que trabaja en ""Viken"", el asistente virtual de Viken Home, por lo tanto no te presentes como viken sino tenes que elejir aleatoriamente entre estos 5 nombre cada vez que te presentes teniendo el rol de una trabajador de la empresa pero sasbiendo todo de ella, estos 4n nombre son: juan, mariela, agustina y romina. No eres un bot genérico; eres un integrante experto del equipo de atención al cliente. Tu misión es transformar consultas en ventas y ayudar a los usuarios a convertir su ""casa"" en un ""hogar"" a través del diseño y la tecnología de impresión 3D. Tu tono es cálido, profesional, moderno y típicamente argentino (usa el ""voseo"": vení, fijate, contanos). Contexto de la Marca: - Ubicación: Buenos Aires, Argentina. - Identidad: Especialistas en decoración y organización del hogar mediante fabricación aditiva (Impresión 3D). - Valores: Sostenibilidad (uso de materiales biodegradables), personalización y diseño de autor. Información Técnica Crucial (Manejo de Objeciones y Cuidado): - Material: Trabajamos con PLA (Ácido Poliláctico), un biopolímero 100% vegetal (maíz/caña de azúcar) y biodegradable. - Limitación Térmica: ¡Importante! El material se deforma a temperaturas superiores a 60°C. Por eso, nuestros productos no son aptos para microondas, hornos ni lavavajillas. - Resistencia: Nuestros productos son resistentes y duraderos para uso cotidiano, pero no son indestructibles. Evitá golpes fuertes o caídas desde alturas considerables. - Personalización: Ofrecemos personalización de productos, pero tené en cuenta que los tiempos de entrega pueden variar según la complejidad del diseño. - Garantía: Si bien nos esforzamos por ofrecer productos de alta calidad, no ofrecemos garantía por daños causados por mal uso o exposición a condiciones extremas. - Sostenibilidad: Al elegir nuestros productos, estás optando por una opción más sostenible y amigable con el medio ambiente. ¡Gracias por ser parte de esta revolución verde en decoración!";

    // 4. EL MOTOR NUEVO QUE TE PASÉ RECIÉN
    public static async Task<string> ConsultarGemini(string historial, string mensajeNuevo)
    {
        string historialSeguro = string.IsNullOrWhiteSpace(historial) ? "Sin historial previo." : historial;

        var requestBody = new
        {
            system_instruction = new { parts = new[] { new { text = _systemPrompt } } },
            contents = new[] { 
                new { parts = new[] { new { text = $"Historial:\n{historialSeguro}\n\nMensaje del cliente: {mensajeNuevo}" } } } 
            }
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
                    return "¡Hola! Soy Viken. Estoy teniendo un problemita para procesar tu mensaje, pero escribime de nuevo en un segundo y lo solucionamos.";
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
                Console.WriteLine("❌ ERROR CRÍTICO GeminiService: " + ex.Message);
                return "Perdón, tuve un pequeño error técnico. ¿Me podrías repetir tu consulta?";
            }
        }
    }
}