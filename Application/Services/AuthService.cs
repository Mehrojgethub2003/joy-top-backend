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

    public async Task<bool> RequestOtp(string phoneNumber)
    {
        var code = new Random().Next(1000, 9999).ToString();
        _otps[phoneNumber] = code;

        var sent = await _telegramService.SendMessageAsync(phoneNumber, $"Sizning Joy Top uchun tasdiqlash kodingiz: {code}");
        
        if (!sent)
        {
            _pendingOtps[phoneNumber] = code;
        }

        return sent;
    }

    public async Task CheckAndSendPendingOtp(string phoneNumber)
    {
        if (_pendingOtps.TryRemove(phoneNumber, out var code))
        {
            await _telegramService.SendMessageAsync(phoneNumber, $"Sizning Joy Top uchun tasdiqlash kodingiz: {code}");
        }
    }

    public bool VerifyOtp(string phoneNumber, string code)
    {
        if (_otps.TryGetValue(phoneNumber, out var savedCode) && savedCode == code)
        {
            _otps.TryRemove(phoneNumber, out _);
            return true;
        }
        return false;
    }
}
