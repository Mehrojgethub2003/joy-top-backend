using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using System.Text.Json;
using JoyTopBackend.Application.Services;

namespace JoyTopBackend.Infrastructure.ExternalServices;

public class TelegramBotManager : BackgroundService
{
    private readonly ITelegramBotClient _botClient;
    private readonly string _usersFilePath = "telegram_users.json";
    private readonly ILogger<TelegramBotManager> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public TelegramBotManager(ILogger<TelegramBotManager> logger, IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _botClient = new TelegramBotClient("8660750923:AAEfbTny23F2C-8PAAJCV9c02dVbep3u6HY");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = Array.Empty<UpdateType>()
        };

        _botClient.StartReceiving(
            updateHandler: HandleUpdateAsync,
            errorHandler: HandleErrorAsync,
            receiverOptions: receiverOptions,
            cancellationToken: stoppingToken
        );

        _logger.LogInformation("🤖 Joy Top Telegram Bot eshitishni boshladi...");
        
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken ct)
    {
        if (update.Message is not { } message) return;

        var chatId = message.Chat.Id;

        if (message.Text == "/start")
        {
            var replyMarkup = new ReplyKeyboardMarkup(new[]
            {
                KeyboardButton.WithRequestContact("📲 Telefon raqamni ulashish")
            }) { ResizeKeyboard = true, OneTimeKeyboard = true };

            await botClient.SendMessage(
                chatId: chatId,
                text: "Xush kelibsiz! Joy Top tizimidan OTP kod olishingiz uchun telefon raqamingizni ulashishingiz kerak.",
                replyMarkup: replyMarkup,
                cancellationToken: ct
            );
            return;
        }

        if (message.Contact is not null)
        {
            var rawPhone = message.Contact.PhoneNumber;
            var phone = new string(rawPhone.Where(char.IsDigit).ToArray());
            
            if (phone.Length == 9) phone = "998" + phone;
            
            await SaveUserMapping(phone, chatId);

            await botClient.SendMessage(
                chatId: chatId,
                text: "✅ Rahmat! Telefon raqamingiz tizimga bog'landi. Endi ilovadan kod so'rasangiz, shu yerga yuboriladi.",
                replyMarkup: new ReplyKeyboardRemove(),
                cancellationToken: ct
            );

            using (var scope = _scopeFactory.CreateScope())
            {
                var authService = scope.ServiceProvider.GetRequiredService<AuthService>();
                await authService.CheckAndSendPendingOtp(phone);
            }
        }
    }

    private Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken ct)
    {
        _logger.LogError("Bot Error: {msg}", exception.Message);
        return Task.CompletedTask;
    }

    private async Task SaveUserMapping(string phone, long chatId)
    {
        Dictionary<string, long> users = new();
        if (System.IO.File.Exists(_usersFilePath))
        {
            try {
                var json = await System.IO.File.ReadAllTextAsync(_usersFilePath);
                users = JsonSerializer.Deserialize<Dictionary<string, long>>(json) ?? new();
            } catch { }
        }

        users[phone] = chatId;
        await System.IO.File.WriteAllTextAsync(_usersFilePath, JsonSerializer.Serialize(users));
    }
}
