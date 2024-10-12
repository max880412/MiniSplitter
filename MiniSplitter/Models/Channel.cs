namespace MiniSplitter.Models
{
    public class Channel
    {
        public int Id { get; set; } // Clave primaria autoincremental de la tabla
        public long ChanId { get; set; } // ID de Telegram del canal
        public string ChanName { get; set; }
        public bool IsActive { get; set; }
    }
}
