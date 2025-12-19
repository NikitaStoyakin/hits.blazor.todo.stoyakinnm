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

                bool isUpdate = question.Status == ExpertQuestionStatus.Answered && !string.IsNullOrEmpty(question.Answer);

                question.Answer = answer;
                question.ExpertId = expertId;
                question.AnsweredAt = DateTime.UtcNow;
                question.Status = ExpertQuestionStatus.Answered;

                await _context.SaveChangesAsync();

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

                var questionWords = normalizedQuestion
                    .Split(new[] { ' ', ',', '.', '!', '?', ':', ';', '-', '_' }, StringSplitOptions.RemoveEmptyEntries)
                    .Where(w => w.Length >= 3)
                    .Take(4)
                    .ToList();

                if (!questionWords.Any())
                {
                    questionWords = new List<string> { normalizedQuestion.Substring(0, Math.Min(10, normalizedQuestion.Length)) };
                }

                var intentNameBase = string.Join("_", questionWords)
                    .Replace("?", "")
                    .Replace("!", "")
                    .Replace(".", "")
                    .Replace(",", "")
                    .ToLower();

                if (intentNameBase.Length > 40)
                {
                    intentNameBase = intentNameBase.Substring(0, 40);
                }

                var intentName = $"expert_{intentNameBase}";

                var allIntents = await _context.Intents
                    .Where(i => i.Name.StartsWith("expert_"))
                    .ToListAsync();

                Intent? matchingIntent = null;

                foreach (var intent in allIntents)
                {
                    foreach (var pattern in intent.Patterns)
                    {
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
                    var patterns = matchingIntent.Patterns;
                    if (!patterns.Contains(normalizedQuestion))
                    {
                        patterns.Add(normalizedQuestion);
                        matchingIntent.Patterns = patterns;
                    }

                    matchingIntent.Responses = new List<string> { expertAnswer };

                    matchingIntent.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    var existingIntent = await _context.Intents
                        .FirstOrDefaultAsync(i => i.Name == intentName);

                    if (existingIntent != null)
                    {
                        var patterns = existingIntent.Patterns;
                        if (!patterns.Contains(normalizedQuestion))
                        {
                            patterns.Add(normalizedQuestion);
                            existingIntent.Patterns = patterns;
                        }

                        existingIntent.Responses = new List<string> { expertAnswer };

                        existingIntent.UpdatedAt = DateTime.UtcNow;
                    }
                    else
                    {
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
            }
        }

        private bool AreQuestionsSimilar(string question1, string question2)
        {
            var words1 = question1.Split(new[] { ' ', ',', '.', '!', '?', ':', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length >= 3)
                .ToList();
            var words2 = question2.Split(new[] { ' ', ',', '.', '!', '?', ':', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length >= 3)
                .ToList();

            if (!words1.Any() || !words2.Any()) return false;

            var commonWords = words1.Intersect(words2).Count();
            var totalWords = Math.Max(words1.Count, words2.Count);

            return (double)commonWords / totalWords >= 0.5;
        }

    }
}

