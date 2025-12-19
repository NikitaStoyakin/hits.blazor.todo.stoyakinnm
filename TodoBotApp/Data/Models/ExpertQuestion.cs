namespace TodoBotApp.Data.Models
{
    public class ExpertQuestion
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string Question { get; set; } = string.Empty;
        public string? Answer { get; set; }
        public string? ExpertId { get; set; } // ID эксперта, который ответил
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? AnsweredAt { get; set; }
        public ExpertQuestionStatus Status { get; set; } = ExpertQuestionStatus.Pending;
        public int? RelatedChatMessageId { get; set; } // Связь с сообщением в чате
    }

    public enum ExpertQuestionStatus
    {
        Pending = 0,    // Ожидает ответа
        InProgress = 1, // Эксперт работает над вопросом
        Answered = 2,   // Ответ дан
        Resolved = 3    // Вопрос решен
    }
}

