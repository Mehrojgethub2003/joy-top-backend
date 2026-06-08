using System;

namespace JoyTopBackend.Domain.Entities;

public class PlaceLocationComment : BaseEntity
{
    public long PlaceId { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string CommentText { get; set; } = string.Empty;
}
