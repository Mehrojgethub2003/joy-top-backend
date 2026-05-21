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
        var places = await _context.Places.ToListAsync();
        await PopulateVoteCountsAsync(places);
        return places;
    }

    public async Task<Place?> GetByIdAsync(long id)
    {
        var place = await _context.Places.FindAsync(id);
        if (place != null)
        {
            await PopulateVoteCountsAsync(place);
        }
        return place;
    }

    public async Task<IEnumerable<Place>> GetByCategoryAsync(string category)
    {
        var places = await _context.Places
            .Where(p => p.Category.ToLower() == category.ToLower())
            .ToListAsync();
        await PopulateVoteCountsAsync(places);
        return places;
    }

    private async Task PopulateVoteCountsAsync(Place place)
    {
        if (place == null) return;
        var votes = await _context.PlaceVotes
            .Where(v => v.PlaceId == place.Id)
            .ToListAsync();
        place.ArzonVotesCount = votes.Count(v => (v.VoteType == "price" || v.VoteType == "service") && v.Value == "1");
        place.OrtachaVotesCount = votes.Count(v => (v.VoteType == "price" || v.VoteType == "service") && v.Value == "2");
        place.QimmatVotesCount = votes.Count(v => (v.VoteType == "price" || v.VoteType == "service") && v.Value == "3");
        place.WrongLocationVotesCount = votes.Count(v => v.VoteType == "location" && v.Value == "wrong");
    }

    private async Task PopulateVoteCountsAsync(IEnumerable<Place> places)
    {
        if (places == null || !places.Any()) return;
        var placeIds = places.Select(p => p.Id).ToList();
        var allVotes = await _context.PlaceVotes
            .Where(v => placeIds.Contains(v.PlaceId))
            .ToListAsync();
        foreach (var place in places)
        {
            var votes = allVotes.Where(v => v.PlaceId == place.Id).ToList();
            place.ArzonVotesCount = votes.Count(v => (v.VoteType == "price" || v.VoteType == "service") && v.Value == "1");
            place.OrtachaVotesCount = votes.Count(v => (v.VoteType == "price" || v.VoteType == "service") && v.Value == "2");
            place.QimmatVotesCount = votes.Count(v => (v.VoteType == "price" || v.VoteType == "service") && v.Value == "3");
            place.WrongLocationVotesCount = votes.Count(v => v.VoteType == "location" && v.Value == "wrong");
        }
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

    public async Task DeleteAsync(long id)
    {
        var place = await _context.Places.FindAsync(id);
        if (place != null)
        {
            _context.Places.Remove(place);
            await _context.SaveChangesAsync();
        }
    }
}
