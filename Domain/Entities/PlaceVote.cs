namespace JoyTopBackend.Domain.Entities;

public class PlaceVote : BaseEntity
{
    public long PlaceId { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    // "price" | "service" | "location"
    public string VoteType { get; set; } = string.Empty;
    // price/service: "1"=Arzon, "2"=O'rtacha, "3"=Qimmat | location: "wrong"
    public string Value { get; set; } = string.Empty;
}
