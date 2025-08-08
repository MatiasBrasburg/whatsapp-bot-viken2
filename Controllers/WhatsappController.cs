using Microsoft.AspNetCore.Mvc;
using Twilio.TwiML;
using System.Linq;
using Twilio.TwiML.Messaging;

namespace WhatsappBot.Controllers
{
    [ApiController]
    [Route("api/whatsapp")] // <-- CAMBIADO para que coincida con Twilio
    public class WhatsappController : ControllerBase
    {
        [HttpPost]
        public IActionResult ReceiveMessage([FromForm] string Body, [FromForm] string From)
        {
            var response = new MessagingResponse();
            var lowerBody = Body?.Trim().ToLower();

            // Si el cuerpo está vacío o no coincide con ninguna opción válida → mostrar menú
            if (string.IsNullOrWhiteSpace(lowerBody) ||
                !(new[] { "1", "2", "3", "4", "5", "6", "7", "8", "9" }.Contains(lowerBody)))
            {
                response.Message("👋 ¡Hola! ¿En qué puedo ayudarte?\n" +
                                 "1️⃣ Ver catálogo de productos 3D\n" +
                                 "2️⃣ Cotizar un producto 3D\n" +
                                 "3️⃣ Hablar con un agente\n" +
                                 "4️⃣ Cotizar una maqueta 3D\n" +
                                 "5️⃣ Ver ubicación de la empresa\n" +
                                 "6️⃣ Ver redes sociales\n" +
                                 "7️⃣ Ver política de cambios y devoluciones\n" +
                                 "8️⃣ Ver política de envíos\n" +
                                 "9️⃣ Ver nuestra página web");
            }
            else if (lowerBody == "1")
            {
                response.Message("📦 Acá tenés nuestro catálogo de productos 3D: [link o lista]");
            }
            else if (lowerBody == "2")
            {
                response.Message("🛠️ Para cotizar, enviame:\n- Qué querés imprimir\n- Medidas aproximadas\n- Si tenés archivo STL o foto de referencia del objeto");
            }
            else if (lowerBody == "3")
            {
                response.Message("🧑‍💼 Para hablar con un agente, por favor envianos un mensaje con las especificaciones necesarias y te responderemos lo antes posible.");
            }
            else if (lowerBody == "4")
            {
                response.Message("🏗️ Para cotizar una maqueta 3D, por favor envíanos:\n- Descripción del proyecto\n- Medidas aproximadas\n- Si tenés archivo STL o foto de referencia\n- Fecha deseada de entrega\n- Color y material preferido");
            }
            else if (lowerBody == "5")
            {
                response.Message("📍 Nuestro showroom está en Distrito Arcos:\nhttps://www.google.com/maps/place/Distrito+Arcos...\nTe esperamos de lunes a viernes de 9 a 18 hs.");
            }
            else if (lowerBody == "6")
            {
                response.Message("📱 Instagram: https://www.instagram.com/vikenhome_");
            }
            else if (lowerBody == "7")
            {
                response.Message("🔁 Nuestra política de cambios y devoluciones es la siguiente: [TEXTO LARGO]");
            }
            else if (lowerBody == "8")
            {
                response.Message("🚚 Política de envíos: [TEXTO LARGO]");
            }
            else if (lowerBody == "9")
            {
                response.Message("🌐 Nuestra página web es: https://viken.com.ar/");
            }

            return Content(response.ToString(), "application/xml");
        }
    }
}
