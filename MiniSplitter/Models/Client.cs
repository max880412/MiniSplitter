namespace MiniSplitter.Models
{
    public class Client
    {
        public long ClientId { get; set; }
        public string ClientName { get; set; }
        public string ClientFirstName { get; set; }
        public long ClientChannelId { get; set; }
        public long ClientOperatorId { get; set; }
        public bool IsActive { get; set; }
        public bool ClientWrote { get; set; }
        public int ClientThreadId { get; set; }
        public DateTime ClientEntryDate { get; set; }
    }
}
