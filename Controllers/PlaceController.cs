using Microsoft.AspNetCore.Mvc;
using JoyTopBackend.Domain.Entities;
using JoyTopBackend.Domain.Interfaces;

namespace JoyTopBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PlaceController : ControllerBase
{
    private readonly IPlaceRepository _repository;
    private readonly IWebHostEnvironment _env;
    private readonly JoyTopBackend.Infrastructure.Persistence.AppDbContext _context;

    public PlaceController(IPlaceRepository repository, IWebHostEnvironment env, JoyTopBackend.Infrastructure.Persistence.AppDbContext context)
    {
        _repository = repository;
        _env = env;
        _context = context;
    }

    [HttpPost("upload-image")]
    public async Task<IActionResult> UploadImage(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "Fayl tanlanmagan" });

        var uploadsFolder = Path.Combine(_env.ContentRootPath, "wwwroot", "uploads", "places");
        if (!Directory.Exists(uploadsFolder))
            Directory.CreateDirectory(uploadsFolder);

        var fileExtension = Path.GetExtension(file.FileName);
        var uniqueFileName = Guid.NewGuid().ToString() + fileExtension;
        var filePath = Path.Combine(uploadsFolder, uniqueFileName);

        using (var fileStream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(fileStream);
        }

        var imageUrl = $"/uploads/places/{uniqueFileName}";
        return Ok(new { imageUrl });
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Place>>> GetPlaces([FromQuery] string? category)
    {
        if (string.IsNullOrEmpty(category))
        {
            return Ok(await _repository.GetAllAsync());
        }
        return Ok(await _repository.GetByCategoryAsync(category));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Place>> GetPlace(long id)
    {
        var place = await _repository.GetByIdAsync(id);
        if (place == null) return NotFound();
        return Ok(place);
    }

    [HttpPost]
    public async Task<ActionResult<Place>> CreatePlace(Place place)
    {
        var createdPlace = await _repository.AddAsync(place);
        return CreatedAtAction(nameof(GetPlace), new { id = createdPlace.Id }, createdPlace);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdatePlace(long id, Place place)
    {
        if (id != place.Id) return BadRequest();
        await _repository.UpdateAsync(place);
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeletePlace(long id)
    {
        await _repository.DeleteAsync(id);
        return NoContent();
    }

    public class RateRequest
    {
        public string UserPhone { get; set; } = string.Empty;
        public int Score { get; set; }
    }

    [HttpPost("{id}/rate")]
    public async Task<IActionResult> RatePlace(long id, [FromBody] RateRequest request)
    {
        if (request.Score < 1 || request.Score > 5) return BadRequest("Baho 1 dan 5 gacha bo'lishi kerak.");
        if (string.IsNullOrEmpty(request.UserPhone)) return BadRequest("Foydalanuvchi raqami kiritilmagan.");

        var place = await _repository.GetByIdAsync(id);
        if (place == null)
        {
            place = new Place
            {
                Id = id,
                Name = "Tashqi Joy",
                Category = "Hammasi",
                Address = "Tashqi Manzil"
            };
            _context.Places.Add(place);
            await _context.SaveChangesAsync();
        }

        // Check if user already rated
        var existingRating = _context.PlaceRatings.FirstOrDefault(r => r.PlaceId == id && r.UserPhone == request.UserPhone);
        if (existingRating != null)
        {
            existingRating.Score = request.Score;
        }
        else
        {
            _context.PlaceRatings.Add(new PlaceRating
            {
                PlaceId = id,
                UserPhone = request.UserPhone,
                Score = request.Score,
                CreatedAt = DateTime.UtcNow
            });
        }
        
        await _context.SaveChangesAsync();

        // Calculate average rating
        var allRatings = _context.PlaceRatings.Where(r => r.PlaceId == id).ToList();
        double avg = allRatings.Any() ? allRatings.Average(r => r.Score) : 0;
        
        place.Rating = Math.Round(avg, 1);
        await _repository.UpdateAsync(place);

        return Ok(new { success = true, newRating = place.Rating });
    }

    public class VoteRequest
    {
        public string DeviceId { get; set; } = string.Empty;
        public string VoteType { get; set; } = string.Empty; // "price" | "service" | "location"
        public string Value { get; set; } = string.Empty; // "1", "2", "3", "wrong"
    }

    [HttpPost("{id}/vote")]
    public async Task<IActionResult> VotePlace(long id, [FromBody] VoteRequest request)
    {
        if (string.IsNullOrEmpty(request.DeviceId)) return BadRequest("DeviceId is required");
        if (string.IsNullOrEmpty(request.VoteType)) return BadRequest("VoteType is required");
        if (string.IsNullOrEmpty(request.Value)) return BadRequest("Value is required");

        var place = await _repository.GetByIdAsync(id);
        if (place == null)
        {
            place = new Place
            {
                Id = id,
                Name = "Tashqi Joy",
                Category = "Hammasi",
                Address = "Tashqi Manzil"
            };
            _context.Places.Add(place);
            await _context.SaveChangesAsync();
        }

        var existingVote = _context.PlaceVotes.FirstOrDefault(v => v.PlaceId == id && v.DeviceId == request.DeviceId && v.VoteType == request.VoteType);
        if (existingVote != null)
        {
            if (existingVote.Value == request.Value)
            {
                _context.PlaceVotes.Remove(existingVote);
            }
            else
            {
                existingVote.Value = request.Value;
                existingVote.UpdatedAt = DateTime.UtcNow;
            }
        }
        else
        {
            _context.PlaceVotes.Add(new PlaceVote
            {
                PlaceId = id,
                DeviceId = request.DeviceId,
                VoteType = request.VoteType,
                Value = request.Value,
                CreatedAt = DateTime.UtcNow
            });
        }

        await _context.SaveChangesAsync();

        var updatedPlace = await _repository.GetByIdAsync(id);
        return Ok(updatedPlace);
    }
}
