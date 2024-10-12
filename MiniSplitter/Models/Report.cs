namespace MiniSplitter.Models
{
    public class Report
    {
        public int ReportId { get; set; }
        public DateTime ReportDate { get; set; }
        public int TotalClients { get; set; }
        public int TotalOperators { get; set; }
    }
}
