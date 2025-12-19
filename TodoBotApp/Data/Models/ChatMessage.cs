namespace TodoBotApp.Data.Models
{
    public class ChatMessage
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? Response { get; set; } // Ответ бота (только для сообщений пользователя)
        public DateTime Timestamp { get; set; }
        public bool IsUserMessage { get; set; }
        public string? Intent { get; set; } // Для анализа намерений (только для сообщений пользователя)
        public double Confidence { get; set; } // Уверенность ответа (только для сообщений пользователя)
        public bool? UserFeedback { get; set; } // Обратная связь пользователя: true - правильно, false - неправильно, null - нет обратной связи
        public string? CorrectIntent { get; set; } // Правильный интент, указанный пользователем
    }

    public class Intent
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string PatternsJson { get; set; } = "[]"; // JSON массив строк для хранения в БД
        public string ResponsesJson { get; set; } = "[]"; // JSON массив строк для хранения в БД
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public int UsageCount { get; set; } = 0; // Счетчик использования интента

        // Вспомогательные свойства для работы в памяти (не сохраняются в БД)
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
