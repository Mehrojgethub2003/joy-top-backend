namespace JoyTopBackend.Domain.Entities;

public class PlaceRating : BaseEntity
{
    public long PlaceId { get; set; }
    public string UserPhone { get; set; } = string.Empty;
    public int Score { get; set; } // 1 to 5
}
