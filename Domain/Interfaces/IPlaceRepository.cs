using JoyTopBackend.Domain.Entities;

namespace JoyTopBackend.Domain.Interfaces;

public interface IPlaceRepository
{
    Task<IEnumerable<Place>> GetAllAsync();
    Task<Place?> GetByIdAsync(int id);
    Task<IEnumerable<Place>> GetByCategoryAsync(string category);
    Task<Place> AddAsync(Place place);
    Task UpdateAsync(Place place);
    Task DeleteAsync(int id);
}
