namespace JoyTopBackend.Domain.Entities;

public class PlaceLike : BaseEntity
{
    public long PlaceId { get; set; }
    public string DeviceId { get; set; } = string.Empty;
}
