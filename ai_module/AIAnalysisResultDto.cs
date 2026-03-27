using System.Text.Json.Serialization;

namespace logger_client.ai_module
{
    public class AIAnalysisResultDto
    {
        public int Confidence { get; set; }

        [JsonConverter(typeof(SingleOrArrayConverter<string>))]
        public List<string> Description { get; set; } = new();

        [JsonConverter(typeof(SingleOrArrayConverter<string>))]
        public List<string> Solutions { get; set; } = new();
    }
}
