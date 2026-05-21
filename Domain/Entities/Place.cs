namespace JoyTopBackend.Domain.Entities;

public class Place : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty; // Mosque, Shop, Kitchen, Gas Station
    public string Description { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public List<string> Images { get; set; } = new();
    public double Rating { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public string WorkingHours { get; set; } = string.Empty;
    public string? FuelType { get; set; }
    public string? ShopType { get; set; }
    public string? OshxonaType { get; set; }
    public string? UstaxonaType { get; set; }

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public int ArzonVotesCount { get; set; }

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public int OrtachaVotesCount { get; set; }

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public int QimmatVotesCount { get; set; }

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public int WrongLocationVotesCount { get; set; }
}
