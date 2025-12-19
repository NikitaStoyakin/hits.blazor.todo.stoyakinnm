# Обзор проекта TodoBotApp

## 1. Аутентификация и авторизация пользователей

**Функция:** Регистрация, вход, выход, управление аккаунтом, роли пользователей

**Файлы:**
- `TodoBotApp/Program.cs` - настройка Identity, инициализация ролей, создание тестового пользователя Expert
- `TodoBotApp/Data/ApplicationUser.cs` - модель пользователя
- `TodoBotApp/Data/ApplicationDbContext.cs` - контекст базы данных с Identity
- `TodoBotApp/Components/Account/Pages/Login.razor` - страница входа
- `TodoBotApp/Components/Account/Pages/Register.razor` - страница регистрации
- `TodoBotApp/Components/Account/Pages/Manage/*.razor` - управление профилем (13 файлов)
- `TodoBotApp/Components/Account/IdentityRevalidatingAuthenticationStateProvider.cs` - провайдер состояния аутентификации
- `TodoBotApp/Components/Account/IdentityUserAccessor.cs` - доступ к пользователю
- `TodoBotApp/Components/Account/IdentityRedirectManager.cs` - управление перенаправлениями
- `TodoBotApp/Components/Account/IdentityNoOpEmailSender.cs` - заглушка для отправки email
- `TodoBotApp/Components/Routes.razor` - маршрутизация с авторизацией
- `TodoBotApp/Components/Account/Shared/RedirectToLogin.razor` - перенаправление на логин
- `TodoBotApp/Components/Account/Shared/RedirectToHome.razor` - перенаправление на главную

## 2. Чат-бот поддержки

**Функция:** Общение с ботом, обработка сообщений, анализ намерений, ответы

**Файлы:**
- `TodoBotApp/Components/Pages/Chat.razor` - страница чата
- `TodoBotApp/Components/Chat/ChatWindow.razor` - компонент окна чата (UI, отправка сообщений, обратная связь)
- `TodoBotApp/Services/IBotService.cs` - сервис бота (основная логика)
  - `ProcessMessageAsync` - обработка сообщений пользователя
  - `AnalyzeIntent` - анализ намерений пользователя
  - `GetResponse` - получение ответа бота
  - `GetConversationHistoryAsync` - получение истории чата
  - `DeleteConversationHistoryAsync` - удаление истории
  - `ProvideFeedbackAsync` - обработка обратной связи пользователя
  - `SendToExpertAsync` - отправка вопроса эксперту
  - `ReloadIntentsAsync` - перезагрузка интентов
  - `TryLearnFromUnknownMessage` - обучение на неизвестных сообщениях
  - `LearnFromFrequentMessage` - обучение на частых сообщениях
  - `LearnFromSimilarMessages` - обучение на похожих сообщениях
  - `AddPatternToIntent` - добавление паттерна к интенту
- `TodoBotApp/Data/Models/ChatMessage.cs` - модель сообщения чата
- `TodoBotApp/Data/Models/Intent.cs` - модель интента (намерения)

## 3. Панель эксперта

**Функция:** Просмотр вопросов, на которые бот не смог ответить, ответы эксперта

**Файлы:**
- `TodoBotApp/Components/Pages/Expert.razor` - страница панели эксперта (UI, список вопросов, форма ответа)
- `TodoBotApp/Services/IExpertService.cs` - сервис эксперта
  - `CreateExpertQuestionAsync` - создание вопроса для эксперта
  - `GetPendingQuestionsAsync` - получение вопросов, ожидающих ответа
  - `AnswerQuestionAsync` - ответ эксперта на вопрос
  - `LearnBotFromExpertAnswerAsync` - обучение бота на ответе эксперта
  - `AreQuestionsSimilar` - проверка схожести вопросов

## 4. Обучение бота

**Функция:** Автоматическое обучение бота на основе ответов экспертов и обратной связи пользователей

**Файлы:**
- `TodoBotApp/Services/IBotService.cs`
  - `TryLearnFromUnknownMessage` - обучение на неизвестных сообщениях
  - `LearnFromFrequentMessage` - обучение на частых сообщениях
  - `LearnFromSimilarMessages` - обучение на похожих сообщениях
  - `AddPatternToIntent` - добавление паттерна к интенту
  - `ProvideFeedbackAsync` - обучение на основе обратной связи
- `TodoBotApp/Services/IExpertService.cs`
  - `LearnBotFromExpertAnswerAsync` - создание интента из ответа эксперта
  - `AreQuestionsSimilar` - проверка схожести вопросов для объединения интентов

## 5. Управление интентами (намерениями)

**Функция:** Хранение и управление интентами бота (паттерны вопросов и ответы)

**Файлы:**
- `TodoBotApp/Services/IBotService.cs`
  - `LoadIntentsAsync` - загрузка интентов из БД
  - `SaveIntentsToDatabaseAsync` - сохранение интентов в БД
  - `LoadDefaultIntents` - загрузка базовых интентов
  - `GetAvailableIntentsAsync` - получение списка доступных интентов
  - `IncrementIntentUsageAsync` - увеличение счетчика использования интента
- `TodoBotApp/Data/Models/Intent.cs` - модель интента
- `TodoBotApp/Data/ApplicationDbContext.cs` - DbSet<Intent>

## 6. База данных и модели данных

**Функция:** Хранение данных приложения

**Файлы:**
- `TodoBotApp/Data/ApplicationDbContext.cs` - контекст Entity Framework
- `TodoBotApp/Data/ApplicationUser.cs` - модель пользователя
- `TodoBotApp/Data/Models/ChatMessage.cs` - модель сообщения чата
- `TodoBotApp/Data/Models/Intent.cs` - модель интента
- `TodoBotApp/Data/Models/ExpertQuestion.cs` - модель вопроса эксперту
- `TodoBotApp/Data/Migrations/*.cs` - миграции базы данных (6 миграций)

## 7. Навигация и макет

**Функция:** Навигационное меню, основной макет приложения

**Файлы:**
- `TodoBotApp/Components/Layout/MainLayout.razor` - основной макет
- `TodoBotApp/Components/Layout/MainLayout.razor.css` - стили макета
- `TodoBotApp/Components/Layout/NavMenu.razor` - навигационное меню
- `TodoBotApp/Components/Layout/NavMenu.razor.css` - стили меню
- `TodoBotApp/Components/App.razor` - корневой компонент приложения
- `TodoBotApp/Components/Routes.razor` - маршрутизация

## 8. Страницы приложения

**Функция:** Основные страницы интерфейса

**Файлы:**
- `TodoBotApp/Components/Pages/Home.razor` - главная страница
- `TodoBotApp/Components/Pages/Chat.razor` - страница чата
- `TodoBotApp/Components/Pages/Expert.razor` - панель эксперта
- `TodoBotApp/Components/Pages/Error.razor` - страница ошибки

## 9. Конфигурация и настройки

**Функция:** Настройки приложения, подключение к БД, переменные окружения

**Файлы:**
- `TodoBotApp/Program.cs` - точка входа, настройка сервисов, middleware
- `TodoBotApp/appsettings.json` - основные настройки
- `TodoBotApp/appsettings.Development.json` - настройки для разработки
- `TodoBotApp/Properties/launchSettings.json` - настройки запуска
- `TodoBotApp/TodoBotApp.csproj` - файл проекта

## 10. Статические ресурсы

**Функция:** CSS, изображения, шрифты

**Файлы:**
- `TodoBotApp/wwwroot/app.css` - основные стили
- `TodoBotApp/wwwroot/bootstrap/*` - Bootstrap CSS
- `TodoBotApp/wwwroot/favicon.png` - иконка сайта

## 11. Компоненты Identity (ASP.NET Core Identity)

**Функция:** Стандартные компоненты для работы с Identity (регистрация, вход, управление аккаунтом)

**Файлы:**
- `TodoBotApp/Components/Account/Pages/*.razor` - страницы Identity (20+ файлов)
- `TodoBotApp/Components/Account/Shared/*.razor` - общие компоненты Identity
- `TodoBotApp/Components/Account/IdentityComponentsEndpointRouteBuilderExtensions.cs` - расширения для маршрутизации Identity

## Основные потоки данных:

1. **Пользователь → Бот:**
   - `ChatWindow.razor` → `IBotService.ProcessMessageAsync()` → анализ интента → ответ

2. **Неизвестный вопрос → Эксперт:**
   - `IBotService.ProcessMessageAsync()` → `IExpertService.CreateExpertQuestionAsync()` → `Expert.razor`

3. **Эксперт → Бот (обучение):**
   - `Expert.razor` → `IExpertService.AnswerQuestionAsync()` → `LearnBotFromExpertAnswerAsync()` → создание/обновление интента

4. **Обратная связь → Обучение:**
   - `ChatWindow.razor` → `IBotService.ProvideFeedbackAsync()` → отправка эксперту или обучение

