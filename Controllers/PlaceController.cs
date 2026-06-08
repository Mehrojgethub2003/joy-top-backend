using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
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
    private readonly JoyTopBackend.Infrastructure.ExternalServices.OsmSyncService _osmSyncService;

    public PlaceController(
        IPlaceRepository repository, 
        IWebHostEnvironment env, 
        JoyTopBackend.Infrastructure.Persistence.AppDbContext context,
        JoyTopBackend.Infrastructure.ExternalServices.OsmSyncService osmSyncService)
    {
        _repository = repository;
        _env = env;
        _context = context;
        _osmSyncService = osmSyncService;
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
        if (place == null)
        {
            // Agar OSM/tashqi joy bo'lsa va hali baholanmagan/ovoz berilmagan bo'lsa, 404 o'rniga default bo'sh obyekt qaytaramiz
            if (id >= 1000000)
            {
                return Ok(new Place
                {
                    Id = id,
                    Name = "Tashqi Joy",
                    Category = "Hammasi",
                    Address = "",
                    Description = "",
                    Images = new List<string>(),
                    Rating = 0
                });
            }
            return NotFound();
        }
        return Ok(place);
    }

    [HttpPost]
    public async Task<ActionResult<Place>> CreatePlace(Place place)
    {
        if (place.Id == 0)
        {
            // Auto-generate a unique ID below 1,000,000 for local places to avoid collision with global OSM IDs
            long newId = 1;
            var localIds = _context.Places
                .Where(p => p.Id < 1000000)
                .Select(p => p.Id)
                .ToList()
                .ToHashSet();
            while (localIds.Contains(newId))
            {
                newId++;
            }
            place.Id = newId;
        }

        // If the user is authenticated, we automatically assign their verified phone number from the token!
        var phone = User.Claims.FirstOrDefault(c => c.Type == "phone")?.Value;
        if (!string.IsNullOrEmpty(phone))
        {
            place.PhoneNumber = phone;
        }

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

    private async Task<Place> EnsurePlaceExistsAsync(
        long id,
        string? name,
        string? category,
        string? address,
        double? latitude,
        double? longitude,
        string? description,
        List<string>? images)
    {
        var place = await _repository.GetByIdAsync(id);
        if (place == null)
        {
            place = new Place
            {
                Id = id,
                Name = name ?? "Tashqi Joy",
                Category = category ?? "Hammasi",
                Address = address ?? "Tashqi Manzil",
                Latitude = latitude ?? 0,
                Longitude = longitude ?? 0,
                Description = description ?? "",
                Images = images ?? new List<string>()
            };
            _context.Places.Add(place);
            await _context.SaveChangesAsync();
        }
        else if (place.Name == "Tashqi Joy" && !string.IsNullOrEmpty(name))
        {
            place.Name = name;
            place.Category = category ?? place.Category;
            place.Address = address ?? place.Address;
            place.Latitude = latitude ?? place.Latitude;
            place.Longitude = longitude ?? place.Longitude;
            place.Description = description ?? place.Description;
            place.Images = images ?? place.Images;
            await _repository.UpdateAsync(place);
        }
        return place;
    }

    public class RateRequest
    {
        public string UserPhone { get; set; } = string.Empty;
        public int Score { get; set; }
        public string? Name { get; set; }
        public string? Category { get; set; }
        public string? Address { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string? Description { get; set; }
        public List<string>? Images { get; set; }
    }

    [HttpPost("{id}/rate")]
    public async Task<IActionResult> RatePlace(long id, [FromBody] RateRequest request)
    {
        if (request.Score < 1 || request.Score > 5) return BadRequest("Baho 1 dan 5 gacha bo'lishi kerak.");
        if (string.IsNullOrEmpty(request.UserPhone)) return BadRequest("Foydalanuvchi raqami kiritilmagan.");

        var place = await EnsurePlaceExistsAsync(
            id,
            request.Name,
            request.Category,
            request.Address,
            request.Latitude,
            request.Longitude,
            request.Description,
            request.Images
        );

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
        public string? Name { get; set; }
        public string? Category { get; set; }
        public string? Address { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string? Description { get; set; }
        public List<string>? Images { get; set; }
    }

    [HttpPost("{id}/vote")]
    public async Task<IActionResult> VotePlace(long id, [FromBody] VoteRequest request)
    {
        if (string.IsNullOrEmpty(request.DeviceId)) return BadRequest("DeviceId is required");
        if (string.IsNullOrEmpty(request.VoteType)) return BadRequest("VoteType is required");
        if (string.IsNullOrEmpty(request.Value)) return BadRequest("Value is required");

        var place = await EnsurePlaceExistsAsync(
            id,
            request.Name,
            request.Category,
            request.Address,
            request.Latitude,
            request.Longitude,
            request.Description,
            request.Images
        );

        var existingVote = _context.PlaceVotes.FirstOrDefault(v => v.PlaceId == id && v.DeviceId == request.DeviceId && v.VoteType == request.VoteType);
        if (existingVote != null)
        {
            if (request.VoteType == "location")
            {
                // For wrong location votes, once submitted it is permanent and cannot be modified or toggled off
                var existingPlace = await _repository.GetByIdAsync(id);
                return Ok(existingPlace);
            }

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

    public class LikeRequest
    {
        public string DeviceId { get; set; } = string.Empty;
        public string? Name { get; set; }
        public string? Category { get; set; }
        public string? Address { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string? Description { get; set; }
        public List<string>? Images { get; set; }
    }

    [HttpPost("{id}/like")]
    [Authorize]
    public async Task<IActionResult> ToggleLike(long id, [FromBody] LikeRequest request)
    {
        var phone = User.Claims.FirstOrDefault(c => c.Type == "phone")?.Value;
        if (string.IsNullOrEmpty(phone)) return Unauthorized();

        var place = await EnsurePlaceExistsAsync(
            id,
            request.Name,
            request.Category,
            request.Address,
            request.Latitude,
            request.Longitude,
            request.Description,
            request.Images
        );

        var existingLike = _context.PlaceLikes.FirstOrDefault(l => l.PlaceId == id && l.DeviceId == phone);
        bool isLiked;
        if (existingLike != null)
        {
            _context.PlaceLikes.Remove(existingLike);
            isLiked = false;
        }
        else
        {
            _context.PlaceLikes.Add(new PlaceLike
            {
                PlaceId = id,
                DeviceId = phone,
                CreatedAt = DateTime.UtcNow
            });
            isLiked = true;
        }

        await _context.SaveChangesAsync();
        return Ok(new { success = true, isLiked = isLiked });
    }

    [HttpGet("liked")]
    [Authorize]
    public async Task<IActionResult> GetLikedPlaces()
    {
        var phone = User.Claims.FirstOrDefault(c => c.Type == "phone")?.Value;
        if (string.IsNullOrEmpty(phone))
        {
            return Ok(new List<Place>());
        }

        var likedPlaceIds = _context.PlaceLikes
            .Where(l => l.DeviceId == phone)
            .Select(l => l.PlaceId)
            .ToList();

        var places = new List<Place>();
        foreach (var id in likedPlaceIds)
        {
            var p = await _repository.GetByIdAsync(id);
            if (p != null)
            {
                places.Add(p);
            }
        }

        return Ok(places);
    }

    [HttpGet("{id}/isLiked")]
    [Authorize]
    public IActionResult CheckIfLiked(long id)
    {
        var phone = User.Claims.FirstOrDefault(c => c.Type == "phone")?.Value;
        if (string.IsNullOrEmpty(phone))
        {
            return Ok(new { isLiked = false });
        }
        var exists = _context.PlaceLikes.Any(l => l.PlaceId == id && l.DeviceId == phone);
        return Ok(new { isLiked = exists });
    }

    [HttpPost("{id}/publish-osm")]
    public async Task<IActionResult> PublishToOsm(long id)
    {
        var place = await _repository.GetByIdAsync(id);
        if (place == null)
        {
            return NotFound("Joy topilmadi.");
        }

        var result = await _osmSyncService.PublishPlaceAsync(place);
        if (!result.success)
        {
            return StatusCode(500, new { message = "OpenStreetMap-ga joylashda xatolik yuz berdi.", error = result.error });
        }

        return Ok(new { success = true, osmNodeId = result.osmNodeId, message = "OpenStreetMap-ga muvaffaqiyatli joylandi!" });
    }

    public class LocationCommentRequest
    {
        public string DeviceId { get; set; } = string.Empty;
        public string CommentText { get; set; } = string.Empty;
        public string? Name { get; set; }
        public string? Category { get; set; }
        public string? Address { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string? Description { get; set; }
        public List<string>? Images { get; set; }
    }

    [HttpPost("{id}/location-comments")]
    [Authorize]
    public async Task<IActionResult> AddLocationComment(long id, [FromBody] LocationCommentRequest request)
    {
        if (string.IsNullOrEmpty(request.CommentText)) return BadRequest("Izoh matni bo'sh bo'lmasligi kerak");

        var phone = User.Claims.FirstOrDefault(c => c.Type == "phone")?.Value;
        if (string.IsNullOrEmpty(phone)) return Unauthorized();

        var user = _context.Users.FirstOrDefault(u => u.PhoneNumber == phone);
        var userName = user != null ? $"{user.FirstName} {user.LastName}".Trim() : "Foydalanuvchi";
        if (string.IsNullOrEmpty(userName)) userName = "Foydalanuvchi";

        var place = await EnsurePlaceExistsAsync(
            id,
            request.Name,
            request.Category,
            request.Address,
            request.Latitude,
            request.Longitude,
            request.Description,
            request.Images
        );

        var comment = new PlaceLocationComment
        {
            PlaceId = id,
            DeviceId = phone,
            UserName = userName,
            CommentText = request.CommentText,
            CreatedAt = DateTime.UtcNow
        };

        _context.PlaceLocationComments.Add(comment);
        await _context.SaveChangesAsync();

        return Ok(new { success = true, comment });
    }

    [HttpGet("{id}/location-comments")]
    public async Task<IActionResult> GetLocationComments(long id)
    {
        var comments = await _context.PlaceLocationComments
            .Where(c => c.PlaceId == id)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();

        return Ok(comments);
    }
}
