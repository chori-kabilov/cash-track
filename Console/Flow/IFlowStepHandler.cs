using Telegram.Bot;

namespace Console.Flow;

// Интерфейс обработчика шага диалогового потока
public interface IFlowStepHandler
{
    // Проверяет, может ли обработчик обработать данный шаг
    bool CanHandle(UserFlowStep step);
    
    // Обрабатывает текстовый ввод пользователя
    Task<bool> HandleAsync(
        ITelegramBotClient bot, 
        long chatId, 
        long userId, 
        string text, 
        UserFlowState flow, 
        Dictionary<long, UserFlowState> flowDict, 
        CancellationToken ct);
}
