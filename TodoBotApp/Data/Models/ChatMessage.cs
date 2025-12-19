namespace TodoBotApp.Data.Models
{
    public class ChatMessage
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? Response { get; set; }
        public DateTime Timestamp { get; set; }
        public bool IsUserMessage { get; set; }
        public string? Intent { get; set; }
        public double Confidence { get; set; }
        public bool? UserFeedback { get; set; }
        public string? CorrectIntent { get; set; }
    }

    public class Intent
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string PatternsJson { get; set; } = "[]";
        public string ResponsesJson { get; set; } = "[]";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public int UsageCount { get; set; } = 0;

        [System.Text.Json.Serialization.JsonIgnore]
        public List<string> Patterns
        {
            get => System.Text.Json.JsonSerializer.Deserialize<List<string>>(PatternsJson) ?? new();
            set => PatternsJson = System.Text.Json.JsonSerializer.Serialize(value);
        }

        [System.Text.Json.Serialization.JsonIgnore]
        public List<string> Responses
        {
            get => System.Text.Json.JsonSerializer.Deserialize<List<string>>(ResponsesJson) ?? new();
            set => ResponsesJson = System.Text.Json.JsonSerializer.Serialize(value);
        }
    }
}
