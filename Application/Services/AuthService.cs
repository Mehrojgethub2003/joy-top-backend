using System.Collections.Concurrent;
using JoyTopBackend.Application.Interfaces;

namespace JoyTopBackend.Application.Services;

public class AuthService
{
    private readonly ITelegramService _telegramService;
    private static readonly ConcurrentDictionary<string, string> _otps = new();
    private static readonly ConcurrentDictionary<string, string> _pendingOtps = new();

    public AuthService(ITelegramService telegramService)
    {
        _telegramService = telegramService;
    }

    private string NormalizePhone(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return string.Empty;
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.Length == 9) digits = "998" + digits;
        return "+" + digits;
    }

    public async Task<bool> RequestOtp(string phoneNumber)
    {
        var normalizedPhone = NormalizePhone(phoneNumber);
        var code = new Random().Next(1000, 9999).ToString();
        _otps[normalizedPhone] = code;

        var sent = await _telegramService.SendMessageAsync(normalizedPhone, $"Sizning Joy Top uchun tasdiqlash kodingiz: {code}");
        
        if (!sent)
        {
            _pendingOtps[normalizedPhone] = code;
        }

        return sent;
    }

    public async Task CheckAndSendPendingOtp(string phoneNumber)
    {
        var normalizedPhone = NormalizePhone(phoneNumber);
        if (_pendingOtps.TryRemove(normalizedPhone, out var code))
        {
            await _telegramService.SendMessageAsync(normalizedPhone, $"Sizning Joy Top uchun tasdiqlash kodingiz: {code}");
        }
    }

    public bool VerifyOtp(string phoneNumber, string code)
    {
        var normalizedPhone = NormalizePhone(phoneNumber);
        if (_otps.TryGetValue(normalizedPhone, out var savedCode) && savedCode == code)
        {
            _otps.TryRemove(normalizedPhone, out _);
            return true;
        }
        return false;
    }
}
