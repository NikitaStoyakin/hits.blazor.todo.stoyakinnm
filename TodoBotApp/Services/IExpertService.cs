using TodoBotApp.Data.Models;
using TodoBotApp.Data;
using Microsoft.EntityFrameworkCore;

namespace TodoBotApp.Services
{
    public interface IExpertService
    {
        Task<int> CreateExpertQuestionAsync(string userId, string question, int? chatMessageId = null);
        Task<List<ExpertQuestion>> GetPendingQuestionsAsync();
        Task<bool> AnswerQuestionAsync(int questionId, string expertId, string answer);
    }

    public class ExpertService : IExpertService
    {
        private readonly ApplicationDbContext _context;

        public ExpertService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<int> CreateExpertQuestionAsync(string userId, string question, int? chatMessageId = null)
        {
            var expertQuestion = new ExpertQuestion
            {
                UserId = userId,
                Question = question,
                Status = ExpertQuestionStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                RelatedChatMessageId = chatMessageId
            };

            _context.ExpertQuestions.Add(expertQuestion);
            await _context.SaveChangesAsync();

            return expertQuestion.Id;
        }

        public async Task<List<ExpertQuestion>> GetPendingQuestionsAsync()
        {
            return await _context.ExpertQuestions
                .Where(q => q.Status == ExpertQuestionStatus.Pending || q.Status == ExpertQuestionStatus.InProgress)
                .OrderBy(q => q.CreatedAt)
                .ToListAsync();
        }

        public async Task<bool> AnswerQuestionAsync(int questionId, string expertId, string answer)
        {
            try
            {
                var question = await _context.ExpertQuestions
                    .FirstOrDefaultAsync(q => q.Id == questionId);

                if (question == null) return false;

                // Проверяем, был ли вопрос уже отвечен ранее (это обновление ответа)
                bool isUpdate = question.Status == ExpertQuestionStatus.Answered && !string.IsNullOrEmpty(question.Answer);

                question.Answer = answer;
                question.ExpertId = expertId;
                question.AnsweredAt = DateTime.UtcNow;
                question.Status = ExpertQuestionStatus.Answered;

                await _context.SaveChangesAsync();

                // Обучаем бота на основе ответа эксперта (передаем флаг обновления)
                await LearnBotFromExpertAnswerAsync(question.Question, answer, isUpdate);

                return true;
            }
            catch
            {
                return false;
            }
        }

        private async Task LearnBotFromExpertAnswerAsync(string question, string expertAnswer, bool isUpdate = false)
        {
            try
            {
                var normalizedQuestion = question.ToLower().Trim();
                
                if (string.IsNullOrWhiteSpace(normalizedQuestion) || string.IsNullOrWhiteSpace(expertAnswer))
                    return;

                // Извлекаем ключевые слова из вопроса для создания уникального имени интента
                var questionWords = normalizedQuestion
                    .Split(new[] { ' ', ',', '.', '!', '?', ':', ';', '-', '_' }, StringSplitOptions.RemoveEmptyEntries)
                    .Where(w => w.Length >= 3) // Только слова длиннее 3 символов
                    .Take(4) // Берем первые 4 ключевых слова
                    .ToList();

                if (!questionWords.Any())
                {
                    // Если нет ключевых слов, используем первые символы вопроса
                    questionWords = new List<string> { normalizedQuestion.Substring(0, Math.Min(10, normalizedQuestion.Length)) };
                }

                // Создаем уникальное имя интента на основе вопроса
                var intentNameBase = string.Join("_", questionWords)
                    .Replace("?", "")
                    .Replace("!", "")
                    .Replace(".", "")
                    .Replace(",", "")
                    .ToLower();

                // Ограничиваем длину имени интента
                if (intentNameBase.Length > 40)
                {
                    intentNameBase = intentNameBase.Substring(0, 40);
                }

                var intentName = $"expert_{intentNameBase}";

                // Проверяем, существует ли уже интент с таким же паттерном или похожим
                var allIntents = await _context.Intents
                    .Where(i => i.Name.StartsWith("expert_"))
                    .ToListAsync();

                Intent? matchingIntent = null;

                // Ищем интент с похожим паттерном
                foreach (var intent in allIntents)
                {
                    foreach (var pattern in intent.Patterns)
                    {
                        // Проверяем схожесть вопросов (простой алгоритм)
                        if (AreQuestionsSimilar(normalizedQuestion, pattern))
                        {
                            matchingIntent = intent;
                            break;
                        }
                    }
                    if (matchingIntent != null) break;
                }

                if (matchingIntent != null)
                {
                    // Обновляем существующий интент
                    var patterns = matchingIntent.Patterns;
                    if (!patterns.Contains(normalizedQuestion))
                    {
                        patterns.Add(normalizedQuestion);
                        matchingIntent.Patterns = patterns;
                    }

                    // Для интентов эксперта всегда заменяем все ответы на новый ответ эксперта
                    // Это гарантирует, что бот будет использовать актуальный ответ эксперта
                    matchingIntent.Responses = new List<string> { expertAnswer };

                    matchingIntent.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    // Проверяем, не существует ли уже интент с таким именем
                    var existingIntent = await _context.Intents
                        .FirstOrDefaultAsync(i => i.Name == intentName);

                    if (existingIntent != null)
                    {
                        // Обновляем существующий интент
                        var patterns = existingIntent.Patterns;
                        if (!patterns.Contains(normalizedQuestion))
                        {
                            patterns.Add(normalizedQuestion);
                            existingIntent.Patterns = patterns;
                        }

                        // Для интентов эксперта всегда заменяем все ответы на новый ответ эксперта
                        existingIntent.Responses = new List<string> { expertAnswer };

                        existingIntent.UpdatedAt = DateTime.UtcNow;
                    }
                    else
                    {
                        // Создаем новый интент
                        var newIntent = new Intent
                        {
                            Name = intentName,
                            Patterns = new List<string> { normalizedQuestion },
                            Responses = new List<string> { expertAnswer },
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow,
                            UsageCount = 0
                        };

                        _context.Intents.Add(newIntent);
                    }
                }

                await _context.SaveChangesAsync();
            }
            catch
            {
                // Игнорируем ошибки при обучении
            }
        }

        private bool AreQuestionsSimilar(string question1, string question2)
        {
            // Простой алгоритм проверки схожести вопросов
            var words1 = question1.Split(new[] { ' ', ',', '.', '!', '?', ':', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length >= 3)
                .ToList();
            var words2 = question2.Split(new[] { ' ', ',', '.', '!', '?', ':', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length >= 3)
                .ToList();

            if (!words1.Any() || !words2.Any()) return false;

            var commonWords = words1.Intersect(words2).Count();
            var totalWords = Math.Max(words1.Count, words2.Count);

            // Если более 50% слов совпадают, считаем вопросы похожими
            return (double)commonWords / totalWords >= 0.5;
        }

    }
}

