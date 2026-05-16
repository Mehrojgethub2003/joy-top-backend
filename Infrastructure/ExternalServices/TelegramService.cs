using Telegram.Bot;
using System.Text.Json;
using JoyTopBackend.Application.Interfaces;

namespace JoyTopBackend.Infrastructure.ExternalServices;

public class TelegramService : ITelegramService
{
    private readonly ITelegramBotClient _botClient;
    private readonly string _usersFilePath = "telegram_users.json";

    public TelegramService()
    {
        // USER tomonidan berilgan token
        _botClient = new TelegramBotClient("8660750923:AAEfbTny23F2C-8PAAJCV9c02dVbep3u6HY");
    }

    private string NormalizePhone(string phone)
    {
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.Length == 9) digits = "998" + digits;
        return "+" + digits;
    }

    public async Task<bool> SendMessageAsync(string phoneNumber, string message)
    {
        if (!System.IO.File.Exists(_usersFilePath)) return false;

        var json = await System.IO.File.ReadAllTextAsync(_usersFilePath);
        var users = JsonSerializer.Deserialize<Dictionary<string, long>>(json);

        var formattedPhone = NormalizePhone(phoneNumber);
        
        if (users != null)
        {
            foreach (var user in users)
            {
                if (NormalizePhone(user.Key) == formattedPhone)
                {
                    try {
                        Console.WriteLine($"\x1b[33m⏳ [BOT] Sending OTP to {formattedPhone} (ChatId: {user.Value})...\x1b[0m");
                        await _botClient.SendMessage(user.Value, message);
                        Console.WriteLine($"\x1b[32m✅ [BOT] OTP sent successfully to {formattedPhone}\x1b[0m");
                        return true;
                    } catch (Exception ex) {
                        Console.WriteLine($"\x1b[31m❌ [BOT] Failed to send OTP to {formattedPhone}: {ex.Message}\x1b[0m");
                        return false;
                    }
                }
            }
        }

        return false;
    }
}
