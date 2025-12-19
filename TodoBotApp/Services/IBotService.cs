using TodoBotApp.Data.Models;
using TodoBotApp.Data;
using Microsoft.EntityFrameworkCore;
using System.Threading;

namespace TodoBotApp.Services
{
    public interface IBotService
    {
        Task<(string Response, int UserMessageId)> ProcessMessageAsync(string message, string userId);
        Task<List<ChatMessage>> GetConversationHistoryAsync(string userId);
        Task<bool> DeleteConversationHistoryAsync(string userId);
        Task<bool> ProvideFeedbackAsync(int messageId, bool isCorrect, string? correctIntent = null);
        Task<List<string>> GetAvailableIntentsAsync();
        Task<bool> SendToExpertAsync(int messageId, string userId, string question);
        Task ReloadIntentsAsync();
    }

    public class BotService : IBotService
    {
        private readonly ApplicationDbContext _context;
        private readonly IExpertService _expertService;
        private readonly List<Intent> _intents = new();

        private bool _intentsLoaded = false;
        private readonly SemaphoreSlim _loadLock = new SemaphoreSlim(1, 1);

        public BotService(ApplicationDbContext context, IExpertService expertService)
        {
            _context = context;
            _expertService = expertService;
        }

        private async Task EnsureIntentsLoadedAsync()
        {
            if (_intentsLoaded) return;

            await _loadLock.WaitAsync();
            try
            {
                if (_intentsLoaded) return;

                await LoadIntentsAsync();
                _intentsLoaded = true;
            }
            finally
            {
                _loadLock.Release();
            }
        }

        private async Task LoadIntentsAsync()
        {
            try
            {
                // Загружаем интенты из базы данных
                var dbIntents = await _context.Intents.ToListAsync();
                
                if (dbIntents.Any())
                {
                    _intents.Clear();
                    _intents.AddRange(dbIntents);
                    
                    // Удаляем старое сообщение об отправке эксперту из всех интентов, если оно есть
                    var expertMessage = "Я не смог найти ответ на ваш вопрос. Ваш вопрос отправлен эксперту, и вы получите ответ в ближайшее время.";
                    bool needsSave = false;
                    
                    foreach (var intent in _intents)
                    {
                        if (intent.Responses.Contains(expertMessage))
                        {
                            intent.Responses.Remove(expertMessage);
                            needsSave = true;
                        }
                    }
                    
                    if (needsSave)
                    {
                        await _context.SaveChangesAsync();
                    }
                }
                else
                {
                    // Если интентов нет в БД, загружаем базовые и сохраняем их
                    LoadDefaultIntents();
                    await SaveIntentsToDatabaseAsync();
                }
            }
            catch
            {
                // Если ошибка при загрузке из БД, используем базовые интенты
                LoadDefaultIntents();
            }
        }

        public async Task ReloadIntentsAsync()
        {
            await _loadLock.WaitAsync();
            try
            {
                await LoadIntentsAsync();
                _intentsLoaded = true;
            }
            finally
            {
                _loadLock.Release();
            }
        }

        public async Task<(string Response, int UserMessageId)> ProcessMessageAsync(string message, string userId)
        {
            // Убеждаемся, что интенты загружены
            // Если интенты уже загружены, перезагружаем их для получения новых интентов от экспертов
            if (_intentsLoaded)
            {
                await ReloadIntentsAsync();
            }
            else
            {
                await EnsureIntentsLoadedAsync();
            }

            var intent = AnalyzeIntent(message);
            var response = GetResponse(intent, message);

            // Если интент неизвестен (низкая уверенность), отправляем эксперту
            bool shouldSendToExpert = false;
            if (intent.Confidence < 0.3 && intent.Name == "unknown")
            {
                // Проверяем, не отправлен ли уже этот вопрос эксперту
                var normalizedMessage = message.ToLower().Trim();
                var existingQuestion = await _context.ExpertQuestions
                    .Where(q => q.UserId == userId && 
                               q.Question.ToLower().Trim() == normalizedMessage &&
                               (q.Status == ExpertQuestionStatus.Pending || 
                                q.Status == ExpertQuestionStatus.InProgress ||
                                q.Status == ExpertQuestionStatus.Answered))
                    .FirstOrDefaultAsync();

                if (existingQuestion == null)
                {
                    shouldSendToExpert = true;
                    response = "Я не смог найти ответ на ваш вопрос. Ваш вопрос отправлен эксперту, и вы получите ответ в ближайшее время.";
                }
            }
            else if (intent.Confidence < 0.5 && intent.Name == "unknown")
            {
                // Пытаемся обучиться
                await TryLearnFromUnknownMessage(message, response);
            }
            else if (intent.Name != "unknown")
            {
                // Увеличиваем счетчик использования интента
                await IncrementIntentUsageAsync(intent.Name);
            }

            // Сохраняем сообщение пользователя
            var userMessage = new ChatMessage
            {
                UserId = userId,
                Message = message,
                Response = response,
                Timestamp = DateTime.UtcNow,
                IsUserMessage = true,
                Intent = intent.Name,
                Confidence = intent.Confidence
            };

            // Сохраняем ответ бота
            var botMessage = new ChatMessage
            {
                UserId = userId,
                Message = response,
                Response = null,
                Timestamp = DateTime.UtcNow,
                IsUserMessage = false,
                Intent = null,
                Confidence = 0
            };

            _context.ChatMessages.Add(userMessage);
            _context.ChatMessages.Add(botMessage);
            await _context.SaveChangesAsync();

            // Если нужно отправить эксперту, создаем вопрос после сохранения сообщения
            if (shouldSendToExpert)
            {
                await _expertService.CreateExpertQuestionAsync(userId, message, userMessage.Id);
            }

            return (response, userMessage.Id);
        }

        public async Task<bool> SendToExpertAsync(int messageId, string userId, string question)
        {
            try
            {
                // Проверяем, не отправлен ли уже этот вопрос эксперту
                var existingQuestion = await _context.ExpertQuestions
                    .FirstOrDefaultAsync(q => q.UserId == userId && 
                                              q.RelatedChatMessageId == messageId);

                if (existingQuestion == null)
                {
                    await _expertService.CreateExpertQuestionAsync(userId, question, messageId > 0 ? messageId : null);
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<List<string>> GetAvailableIntentsAsync()
        {
            await EnsureIntentsLoadedAsync();
            return _intents.Select(i => i.Name).ToList();
        }

        public async Task<List<ChatMessage>> GetConversationHistoryAsync(string userId)
        {
            var chatMessages = await _context.ChatMessages
                .Where(cm => cm.UserId == userId)
                .OrderBy(cm => cm.Timestamp)
                .Take(50) 
                .ToListAsync();

            // Проверяем, есть ли ответы экспертов на вопросы пользователя
            // Добавляем ответы экспертов только для тех вопросов, которые связаны с конкретными сообщениями
            var expertQuestions = await _context.ExpertQuestions
                .Where(q => q.UserId == userId && 
                           q.Status == ExpertQuestionStatus.Answered && 
                           !string.IsNullOrEmpty(q.Answer) &&
                           q.RelatedChatMessageId.HasValue)
                .ToListAsync();

            foreach (var question in expertQuestions)
            {
                // Находим соответствующее сообщение пользователя
                var relatedUserMessage = chatMessages.FirstOrDefault(m => 
                    m.Id == question.RelatedChatMessageId.Value);

                if (relatedUserMessage == null)
                    continue;

                // Проверяем, не использовал ли бот уже этот ответ через интент
                // Если после сообщения пользователя есть ответ бота с таким же текстом, значит бот уже использовал ответ эксперта
                var botUsedExpertAnswer = chatMessages.Any(m => 
                    !m.IsUserMessage && 
                    m.Message == question.Answer &&
                    m.Timestamp > relatedUserMessage.Timestamp);

                // Если бот уже использовал ответ эксперта, не добавляем сообщение "Ответ эксперта"
                if (botUsedExpertAnswer)
                    continue;

                // Проверяем, не добавлено ли уже сообщение с ответом эксперта для этого вопроса
                var existingExpertMessage = chatMessages.FirstOrDefault(m => 
                    m.Message.Contains("Ответ эксперта") && 
                    m.Message.Contains(question.Answer ?? "") &&
                    m.Timestamp > relatedUserMessage.Timestamp);

                if (existingExpertMessage == null)
                {
                    // Создаем сообщение с ответом эксперта с временной меткой после сообщения пользователя
                    var expertMessage = new ChatMessage
                    {
                        UserId = userId,
                        Message = $"👨‍💼 Ответ эксперта:\n\n{question.Answer}",
                        Response = null,
                        // Временная метка должна быть после сообщения пользователя, но не раньше времени ответа эксперта
                        Timestamp = (question.AnsweredAt ?? DateTime.UtcNow) > relatedUserMessage.Timestamp 
                            ? (question.AnsweredAt ?? DateTime.UtcNow) 
                            : relatedUserMessage.Timestamp.AddSeconds(1),
                        IsUserMessage = false,
                        Intent = null,
                        Confidence = 0
                    };

                    _context.ChatMessages.Add(expertMessage);
                    chatMessages.Add(expertMessage);
                }
            }

            if (expertQuestions.Any())
            {
                await _context.SaveChangesAsync();
            }

            return chatMessages.OrderBy(m => m.Timestamp).ToList();
        }

        public async Task<bool> DeleteConversationHistoryAsync(string userId)
        {
            try
            {
                var userMessages = await _context.ChatMessages
                    .Where(cm => cm.UserId == userId)
                    .ToListAsync();

                if (userMessages.Any())
                {
                    _context.ChatMessages.RemoveRange(userMessages);
                    await _context.SaveChangesAsync();
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        private (string Name, double Confidence) AnalyzeIntent(string message)
        {
            var normalizedMessage = message.ToLower().Trim();
            var bestMatch = ("unknown", 0.1);
            var bestSimilarity = 0.0;

            foreach (var intent in _intents)
            {
                foreach (var pattern in intent.Patterns)
                {
                    // Точное совпадение
                    if (normalizedMessage == pattern)
                    {
                        return (intent.Name, 0.95);
                    }

                    // Частичное совпадение (содержит паттерн)
                    if (normalizedMessage.Contains(pattern) || pattern.Contains(normalizedMessage))
                    {
                        var similarity = CalculateSimilarity(normalizedMessage, pattern);
                        if (similarity > bestSimilarity)
                        {
                            bestSimilarity = similarity;
                            bestMatch = (intent.Name, Math.Min(0.9, 0.5 + similarity * 0.4));
                        }
                    }
                }
            }

            // Если нашли достаточно похожий паттерн, возвращаем его
            if (bestSimilarity >= 0.5)
            {
                return bestMatch;
            }

            return ("unknown", 0.1);
        }

        private double CalculateSimilarity(string message1, string message2)
        {
            var words1 = message1.Split(new[] { ' ', ',', '.', '!', '?', ':', ';', '-', '_' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length >= 2)
                .ToList();
            var words2 = message2.Split(new[] { ' ', ',', '.', '!', '?', ':', ';', '-', '_' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length >= 2)
                .ToList();

            if (!words1.Any() || !words2.Any()) return 0.0;

            var commonWords = words1.Intersect(words2).Count();
            var totalWords = Math.Max(words1.Count, words2.Count);

            return (double)commonWords / totalWords;
        }

        private bool AreQuestionsSimilarForExpert(string question1, string question2)
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

        private string GetResponse((string Name, double Confidence) intent, string message)
        {
            var foundIntent = _intents.FirstOrDefault(i => i.Name == intent.Name);
            if (foundIntent != null && foundIntent.Responses.Any())
            {
                var random = new Random();
                var response = foundIntent.Responses[random.Next(foundIntent.Responses.Count)];
                
                // Убираем старое сообщение об отправке эксперту, если оно случайно попало в ответы
                var oldExpertMessage = "Я не смог найти ответ на ваш вопрос. Ваш вопрос отправлен эксперту, и вы получите ответ в ближайшее время.";
                if (response == oldExpertMessage)
                {
                    // Если это старое сообщение, возвращаем стандартное
                    return "Извините, я не совсем понимаю ваш вопрос. Можете переформулировать?";
                }
                
                return response;
            }

            return "Извините, я не совсем понимаю ваш вопрос. Можете переформулировать?";
        }

        private void LoadDefaultIntents()
        {
            _intents.Clear();
            
            _intents.Add(new Intent
            {
                Name = "greeting",
                Patterns = new List<string> { "привет", "здравствуй", "добрый день", "hello", "hi" },
                Responses = new List<string> { "Привет! Чем могу помочь?", "Здравствуйте! Задавайте ваш вопрос", "Добрый день! Как я могу вам помочь?" }
            });

            _intents.Add(new Intent
            {
                Name = "help",
                Patterns = new List<string> { "помощь", "помоги", "не работает", "проблема", "ошибка" },
                Responses = new List<string> { "Опишите вашу проблему подробнее", "Попробуйте перезагрузить страницу", "Сейчас перенаправлю вас к специалисту" }
            });

            _intents.Add(new Intent
            {
                Name = "thanks",
                Patterns = new List<string> { "спасибо", "благодарю", "thanks", "мерси" },
                Responses = new List<string> { "Пожалуйста! Обращайтесь ещё", "Рад был помочь!", "Всегда готов помочь!" }
            });

            _intents.Add(new Intent
            {
                Name = "farewell",
                Patterns = new List<string> { "пока", "до свидания", "всего доброго", "goodbye" },
                Responses = new List<string> { "До свидания! Возвращайтесь", "Всего хорошего!", "Буду рад помочь снова" }
            });
        }

        private async Task SaveIntentsToDatabaseAsync()
        {
            try
            {
                foreach (var intent in _intents)
                {
                    var existingIntent = await _context.Intents
                        .FirstOrDefaultAsync(i => i.Name == intent.Name);

                    if (existingIntent == null)
                    {
                        intent.CreatedAt = DateTime.UtcNow;
                        intent.UpdatedAt = DateTime.UtcNow;
                        _context.Intents.Add(intent);
                    }
                }
                await _context.SaveChangesAsync();
            }
            catch
            {
                // Игнорируем ошибки при сохранении
            }
        }

        // Подход 1: Обучение на основе частоты повторений
        private async Task TryLearnFromUnknownMessage(string message, string response)
        {
            try
            {
                var normalizedMessage = message.ToLower().Trim();
                
                // Проверяем, сколько раз это сообщение уже встречалось
                var messageCount = await _context.ChatMessages
                    .Where(cm => cm.IsUserMessage && 
                                cm.Message.ToLower().Trim() == normalizedMessage &&
                                cm.Intent == "unknown")
                    .CountAsync();

                // Если сообщение повторяется 3+ раза, пытаемся обучиться
                if (messageCount >= 3)
                {
                    await LearnFromFrequentMessage(normalizedMessage);
                }
                else
                {
                    // Пытаемся найти похожие сообщения по контексту или структуре
                    await LearnFromSimilarMessages(normalizedMessage);
                }
            }
            catch
            {
                // Игнорируем ошибки при обучении
            }
        }

        // Обучение на основе частоты - если сообщение часто повторяется
        private async Task LearnFromFrequentMessage(string normalizedMessage)
        {
            try
            {
                // Ищем, какие интенты чаще всего встречаются в диалогах с этим сообщением
                // Анализируем контекст - что было до и после этого сообщения
                var messagesWithContext = await _context.ChatMessages
                    .Where(cm => cm.IsUserMessage)
                    .OrderBy(cm => cm.Timestamp)
                    .ToListAsync();

                // Находим все вхождения этого сообщения
                var messageIndices = new List<int>();
                for (int i = 0; i < messagesWithContext.Count; i++)
                {
                    if (messagesWithContext[i].Message.ToLower().Trim() == normalizedMessage)
                    {
                        messageIndices.Add(i);
                    }
                }

                // Анализируем контекст вокруг каждого вхождения
                var intentCandidates = new Dictionary<string, int>();
                
                foreach (var idx in messageIndices)
                {
                    // Смотрим сообщение перед этим (если есть)
                    if (idx > 0)
                    {
                        var prevMessage = messagesWithContext[idx - 1];
                        if (prevMessage.Intent != null && prevMessage.Intent != "unknown")
                        {
                            intentCandidates[prevMessage.Intent] = 
                                intentCandidates.GetValueOrDefault(prevMessage.Intent, 0) + 1;
                        }
                    }

                    // Смотрим сообщение после этого (если есть)
                    if (idx < messagesWithContext.Count - 1)
                    {
                        var nextMessage = messagesWithContext[idx + 1];
                        if (nextMessage.Intent != null && nextMessage.Intent != "unknown")
                        {
                            intentCandidates[nextMessage.Intent] = 
                                intentCandidates.GetValueOrDefault(nextMessage.Intent, 0) + 1;
                        }
                    }
                }

                // Выбираем наиболее вероятный интент
                var bestIntent = intentCandidates
                    .OrderByDescending(kvp => kvp.Value)
                    .FirstOrDefault();

                if (bestIntent.Key != null && bestIntent.Value >= 2)
                {
                    await AddPatternToIntent(bestIntent.Key, normalizedMessage);
                }
            }
            catch
            {
                // Игнорируем ошибки
            }
        }

        // Обучение на основе похожих сообщений (улучшенный алгоритм)
        private async Task LearnFromSimilarMessages(string normalizedMessage)
        {
            try
            {
                // Извлекаем все слова (не только длиннее 3 символов)
                var words = normalizedMessage
                    .Split(new[] { ' ', ',', '.', '!', '?', ':', ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Where(w => w.Length >= 2) // Минимум 2 символа
                    .ToList();

                if (!words.Any()) return;

                // Ищем сообщения, которые содержат хотя бы 2 общих слова
                var similarMessages = await _context.ChatMessages
                    .Where(cm => cm.IsUserMessage && 
                                cm.Intent != null && 
                                cm.Intent != "unknown")
                    .ToListAsync();

                var intentMatches = new Dictionary<string, int>();

                foreach (var msg in similarMessages)
                {
                    var msgWords = msg.Message.ToLower()
                        .Split(new[] { ' ', ',', '.', '!', '?', ':', ';' }, StringSplitOptions.RemoveEmptyEntries)
                        .Where(w => w.Length >= 2)
                        .ToList();

                    // Считаем количество общих слов
                    var commonWords = words.Intersect(msgWords).Count();
                    
                    if (commonWords >= 2 && msg.Intent != null)
                    {
                        intentMatches[msg.Intent] = 
                            intentMatches.GetValueOrDefault(msg.Intent, 0) + commonWords;
                    }
                }

                // Выбираем интент с наибольшим количеством совпадений
                var bestMatch = intentMatches
                    .OrderByDescending(kvp => kvp.Value)
                    .FirstOrDefault();

                if (bestMatch.Key != null && bestMatch.Value >= 3)
                {
                    await AddPatternToIntent(bestMatch.Key, normalizedMessage);
                }
            }
            catch
            {
                // Игнорируем ошибки
            }
        }

        // Добавление паттерна к интенту
        private async Task AddPatternToIntent(string intentName, string pattern)
        {
            try
            {
                var existingIntent = await _context.Intents
                    .FirstOrDefaultAsync(i => i.Name == intentName);

                if (existingIntent != null)
                {
                    var patterns = existingIntent.Patterns;
                    
                    if (!patterns.Contains(pattern))
                    {
                        patterns.Add(pattern);
                        existingIntent.Patterns = patterns;
                        existingIntent.UpdatedAt = DateTime.UtcNow;
                        
                        // Обновляем в памяти
                        var inMemoryIntent = _intents.FirstOrDefault(i => i.Name == intentName);
                        if (inMemoryIntent != null)
                        {
                            inMemoryIntent.Patterns = patterns;
                        }
                        
                        await _context.SaveChangesAsync();
                    }
                }
            }
            catch
            {
                // Игнорируем ошибки
            }
        }

        // Подход 2: Обучение на основе обратной связи пользователя
        public async Task<bool> ProvideFeedbackAsync(int messageId, bool isCorrect, string? correctIntent = null)
        {
            try
            {
                var message = await _context.ChatMessages
                    .FirstOrDefaultAsync(cm => cm.Id == messageId);

                if (message == null || !message.IsUserMessage) return false;

                message.UserFeedback = isCorrect;

                // Если пользователь недоволен ответом (isCorrect = false), отправляем вопрос эксперту
                if (!isCorrect)
                {
                    // Убеждаемся, что интенты загружены
                    await EnsureIntentsLoadedAsync();

                    var normalizedQuestion = message.Message.ToLower().Trim();
                    var hasExpertAnswer = false;
                    
                    // Сначала проверяем, есть ли уже ответ эксперта на похожий вопрос (через интенты)
                    // Ищем интенты, созданные экспертом (начинаются с "expert_")
                    var expertIntents = _intents.Where(i => i.Name.StartsWith("expert_")).ToList();
                    foreach (var expertIntent in expertIntents)
                    {
                        foreach (var pattern in expertIntent.Patterns)
                        {
                            // Проверяем схожесть вопроса с паттерном
                            if (AreQuestionsSimilarForExpert(normalizedQuestion, pattern))
                            {
                                hasExpertAnswer = true;
                                break;
                            }
                        }
                        if (hasExpertAnswer) break;
                    }

                    // Если есть ответ эксперта на похожий вопрос - всегда отправляем эксперту для повторной проверки
                    // Если нет ответа эксперта - проверяем, не отправлен ли уже этот вопрос для этого сообщения
                    bool shouldSendToExpert = false;
                    
                    if (hasExpertAnswer)
                    {
                        // Если есть ответ эксперта, отправляем даже если уже был вопрос для этого сообщения
                        shouldSendToExpert = true;
                    }
                    else
                    {
                        // Если нет ответа эксперта, проверяем, не отправлен ли уже этот вопрос для этого сообщения
                        var existingQuestionForMessage = await _context.ExpertQuestions
                            .FirstOrDefaultAsync(q => q.UserId == message.UserId && 
                                                      q.RelatedChatMessageId == messageId);
                        
                        if (existingQuestionForMessage == null)
                        {
                            shouldSendToExpert = true;
                        }
                    }

                    if (shouldSendToExpert)
                    {
                        // Отправляем вопрос эксперту
                        // Если есть ответ эксперта - это повторная отправка (пользователь недоволен)
                        // Если нет ответа эксперта - это новая отправка
                        await _expertService.CreateExpertQuestionAsync(message.UserId, message.Message, messageId);
                    }

                    if (!string.IsNullOrEmpty(correctIntent))
                    {
                        message.CorrectIntent = correctIntent;
                        
                        // Добавляем паттерн к указанному интенту
                        await AddPatternToIntent(correctIntent, message.Message.ToLower().Trim());
                        
                        // Обновляем интент сообщения
                        message.Intent = correctIntent;
                        message.Confidence = 0.9;
                    }
                }
                else if (isCorrect && message.Intent != null)
                {
                    // Подтверждаем правильность - увеличиваем уверенность
                    message.Confidence = Math.Min(1.0, message.Confidence + 0.1);
                }

                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private async Task IncrementIntentUsageAsync(string intentName)
        {
            try
            {
                var intent = await _context.Intents
                    .FirstOrDefaultAsync(i => i.Name == intentName);

                if (intent != null)
                {
                    intent.UsageCount++;
                    intent.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                }
            }
            catch
            {
                // Игнорируем ошибки
            }
        }
    }
}
