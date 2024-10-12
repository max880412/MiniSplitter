namespace MiniSplitter.Models
{
    public class Operator
    {
        public long OpId { get; set; }
        public string OpUsername { get; set; }
        public long OpChannel { get; set; }
        public bool IsActive { get; set; }
        public int AssignedClientsToday { get; set; }
    }
}
