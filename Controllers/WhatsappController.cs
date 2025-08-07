using Microsoft.AspNetCore.Mvc;
using Twilio.TwiML;

namespace WhatsappBot.Controllers
{
    [ApiController]
    [Route("whatsapp")]
    public class WhatsappController : ControllerBase
    {
        [HttpPost]
        public IActionResult ReceiveMessage([FromForm] string Body, [FromForm] string From)
        {
            var response = new MessagingResponse();
            var lowerBody = Body?.Trim().ToLower();

            if (string.IsNullOrWhiteSpace(lowerBody))
            {
                response.Message("No se recibió ningún mensaje. Por favor escribí algo.");
            }
            else if (lowerBody != "")
            {
                response.Message("¡Hola! ¿En qué puedo ayudarte?\n" +
                                 "1. Ver catálogo de productos 3D\n" +
                                 "2. Cotizar un producto 3D (el cual no está en su web)\n" +
                                 "3. Hablar con un agente\n" +
                                 "4. Cotizar una maqueta 3D \n" +
                                 "5. Ver ubicación de la empresa\n" +
                                 "6. Ver redes sociales\n" +
                                 "7. Ver política de cambios y devoluciones\n" +
                                 "8. Ver política de envíos\n" +
                                 "9. Ver nuestra página web");
            }
            else if (lowerBody.Contains("1"))
                response.Message("Acá tenés nuestro catálogo de productos 3D: [link o lista]");
            else if (lowerBody.Contains("2"))
                response.Message("Para cotizar, enviame:\n- Qué querés imprimir\n- Medidas aproximadas\n- Si tenés archivo STL o foto de referencia del objeto");
            else if (lowerBody.Contains("3"))
                response.Message("Para hablar con un agente, por favor envianos un mensaje con las especificaciones necesarias y te responderemos lo antes posible.");
            else if (lowerBody.Contains("4"))
                response.Message("Para cotizar una maqueta 3D, por favor envíanos:\n- Descripción del proyecto\n- Medidas aproximadas\n- Si tenés archivo STL o foto de referencia\n- Fecha deseada de entrega\n- Color y material preferido");
            else if (lowerBody.Contains("5"))
                response.Message("Nuestro showroom está en Distrito Arcos: https://www.google.com/maps/place/Distrito+Arcos...\nTe esperamos de lunes a viernes de 9 a 18 hs.");
            else if (lowerBody.Contains("6"))
                response.Message("Instagram: https://www.instagram.com/vikenhome_");
            else if (lowerBody.Contains("7"))
                response.Message("Nuestra política de cambios y devoluciones es la siguiente: [TEXTO LARGO]");
            else if (lowerBody.Contains("8"))
                response.Message("Política de envíos: [TEXTO LARGO]");
            else if (lowerBody.Contains("9"))
                response.Message("Nuestra página web es: https://viken.com.ar/");
            else
                response.Message("Opción no reconocida. Escribí 'hola' para ver el menú de opciones.");

            return Content(response.ToString(), "application/xml");
        }
    }
}
