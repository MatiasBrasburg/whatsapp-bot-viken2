public class WebhookPayload
{
    public string Telefono { get; set; }
    public string Mensaje { get; set; }
    public bool FromMe { get; set; } // Fundamental para saber si el mensaje lo mandaste vos
}