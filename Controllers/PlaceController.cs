using Microsoft.AspNetCore.Mvc;
using JoyTopBackend.Domain.Entities;
using JoyTopBackend.Domain.Interfaces;

namespace JoyTopBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PlaceController : ControllerBase
{
    private readonly IPlaceRepository _repository;

    public PlaceController(IPlaceRepository repository)
    {
        _repository = repository;
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
    public async Task<ActionResult<Place>> GetPlace(int id)
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
    public async Task<IActionResult> UpdatePlace(int id, Place place)
    {
        if (id != place.Id) return BadRequest();
        await _repository.UpdateAsync(place);
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeletePlace(int id)
    {
        await _repository.DeleteAsync(id);
        return NoContent();
    }
}
