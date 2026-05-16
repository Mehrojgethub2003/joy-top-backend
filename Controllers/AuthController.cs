using Microsoft.AspNetCore.Mvc;
using JoyTopBackend.Application.Services;

namespace JoyTopBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AuthService _authService;
    private readonly TokenService _tokenService;

    public AuthController(AuthService authService, TokenService tokenService)
    {
        _authService = authService;
        _tokenService = tokenService;
    }

    [HttpPost("request-otp")]
    public async Task<IActionResult> RequestOtp([FromBody] OtpRequest request)
    {
        var sent = await _authService.RequestOtp(request.PhoneNumber);
        if (sent)
        {
            return Ok(new { message = "OTP yuborildi" });
        }
        return Ok(new { message = "Botdan ro'yxatdan o'ting", needsRegistration = true });
    }

    [HttpPost("verify-otp")]
    public IActionResult VerifyOtp([FromBody] VerifyRequest request)
    {
        var verified = _authService.VerifyOtp(request.PhoneNumber, request.Code);
        if (verified)
        {
            var token = _tokenService.GenerateToken(request.PhoneNumber);
            return Ok(new { accessToken = token, refreshToken = token }); // Hozircha bir xil
        }
        return BadRequest(new { message = "Kod noto'g'ri" });
    }
}

public record OtpRequest(string PhoneNumber);
public record VerifyRequest(string PhoneNumber, string Code);
