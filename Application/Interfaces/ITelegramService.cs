namespace JoyTopBackend.Application.Interfaces;

public interface ITelegramService
{
    Task<bool> SendMessageAsync(string phoneNumber, string message);
}
