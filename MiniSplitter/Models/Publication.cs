// Models/Publication.cs
namespace MiniSplitter.Models
{
    public class Publication
    {
        public int PublicationId { get; set; } // Clave primaria autoincremental
        public long ChannelId { get; set; } // ID de Telegram del canal al que se enviará la publicación
        public string MediaType { get; set; } // "text", "photo", "video", "audio", "document"
        public string MediaFilePath { get; set; } // Ruta al archivo almacenado (si aplica)
        public string Text { get; set; } // Texto de la publicación
        public DateTime ScheduledTime { get; set; } // Hora programada para enviar la publicación
        public bool IsSent { get; set; } // Indica si la publicación ya fue enviada
    }
}
