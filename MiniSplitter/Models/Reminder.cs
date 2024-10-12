namespace MiniSplitter.Models
{
    public class Reminder
    {
        public int RemId { get; set; }
        public long RemClientId { get; set; } // 0 para todos los clientes
        public string RemMediaType { get; set; } // "text", "photo", "video", "audio", "document"
        public string RemMediaFilePath { get; set; } // Ruta al archivo almacenado
        public string RemText { get; set; }
        public DateTime RemTime { get; set; }
        public bool RemSended { get; set; }
    }
}
