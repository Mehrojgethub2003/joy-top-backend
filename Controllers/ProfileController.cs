using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using JoyTopBackend.Infrastructure.Persistence;
using JoyTopBackend.Domain.Entities;
using System.Security.Claims;

namespace JoyTopBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProfileController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _env;

    public ProfileController(AppDbContext context, IWebHostEnvironment env)
    {
        _context = context;
        _env = env;
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetProfile()
    {
        var phone = User.Claims.FirstOrDefault(c => c.Type == "phone")?.Value;
        if (string.IsNullOrEmpty(phone)) return Unauthorized();

        var user = await _context.Users.FirstOrDefaultAsync(u => u.PhoneNumber == phone);
        if (user == null) return NotFound(new { message = "Profil topilmadi" });

        return Ok(user);
    }

    [HttpPatch("me")]
    public async Task<IActionResult> UpdateProfile([FromBody] User update)
    {
        var phone = User.Claims.FirstOrDefault(c => c.Type == "phone")?.Value;
        if (string.IsNullOrEmpty(phone)) return Unauthorized();

        var user = await _context.Users.FirstOrDefaultAsync(u => u.PhoneNumber == phone);
        
        if (user == null)
        {
            user = new User
            {
                PhoneNumber = phone,
                FirstName = update.FirstName,
                LastName = update.LastName,
                ImageUrl = update.ImageUrl
            };
            _context.Users.Add(user);
        }
        else
        {
            user.FirstName = update.FirstName;
            user.LastName = update.LastName;
            user.ImageUrl = update.ImageUrl;
        }

        await _context.SaveChangesAsync();
        return Ok(user);
    }

    [HttpPost("upload-image")]
    public async Task<IActionResult> UploadImage(IFormFile file)
    {
        var phone = User.Claims.FirstOrDefault(c => c.Type == "phone")?.Value;
        if (string.IsNullOrEmpty(phone)) return Unauthorized();

        var user = await _context.Users.FirstOrDefaultAsync(u => u.PhoneNumber == phone);
        if (user == null) return NotFound(new { message = "Profil topilmadi" });

        if (file == null || file.Length == 0)
            return BadRequest(new { message = "Fayl tanlanmagan" });

        var uploadsFolder = Path.Combine(_env.ContentRootPath, "wwwroot", "uploads", "profiles");
        if (!Directory.Exists(uploadsFolder))
            Directory.CreateDirectory(uploadsFolder);

        var fileExtension = Path.GetExtension(file.FileName);
        var uniqueFileName = Guid.NewGuid().ToString() + fileExtension;
        var filePath = Path.Combine(uploadsFolder, uniqueFileName);

        using (var fileStream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(fileStream);
        }

        // Host name could be retrieved from Request, or you can just return the relative path
        var imageUrl = $"/uploads/profiles/{uniqueFileName}";
        user.ImageUrl = imageUrl;
        await _context.SaveChangesAsync();

        return Ok(user);
    }
}
