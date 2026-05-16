using Microsoft.EntityFrameworkCore;
using JoyTopBackend.Domain.Entities;
using JoyTopBackend.Domain.Interfaces;
using JoyTopBackend.Infrastructure.Persistence;

namespace JoyTopBackend.Infrastructure.Repositories;

public class EfPlaceRepository : IPlaceRepository
{
    private readonly AppDbContext _context;

    public EfPlaceRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Place>> GetAllAsync()
    {
        return await _context.Places.ToListAsync();
    }

    public async Task<Place?> GetByIdAsync(int id)
    {
        return await _context.Places.FindAsync(id);
    }

    public async Task<IEnumerable<Place>> GetByCategoryAsync(string category)
    {
        return await _context.Places
            .Where(p => p.Category.ToLower() == category.ToLower())
            .ToListAsync();
    }

    public async Task<Place> AddAsync(Place place)
    {
        _context.Places.Add(place);
        await _context.SaveChangesAsync();
        return place;
    }

    public async Task UpdateAsync(Place place)
    {
        place.UpdatedAt = DateTime.UtcNow;
        _context.Entry(place).State = EntityState.Modified;
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var place = await _context.Places.FindAsync(id);
        if (place != null)
        {
            _context.Places.Remove(place);
            await _context.SaveChangesAsync();
        }
    }
}
