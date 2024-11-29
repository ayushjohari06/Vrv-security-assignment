namespace CrudApi.Models
{
    public class ExportRequest
    {
        public Dictionary<string, string> Filters { get; set; }
        public string Format { get; set; }
    }
}
