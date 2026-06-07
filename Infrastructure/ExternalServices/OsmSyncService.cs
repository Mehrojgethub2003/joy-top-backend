using System.Text;
using System.Xml.Linq;
using JoyTopBackend.Domain.Entities;

namespace JoyTopBackend.Infrastructure.ExternalServices;

public class OsmSyncService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OsmSyncService> _logger;

    public OsmSyncService(HttpClient httpClient, IConfiguration configuration, ILogger<OsmSyncService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<(bool success, string? osmNodeId, string? error)> PublishPlaceAsync(Place place)
    {
        var username = _configuration["Osm:Username"];
        var password = _configuration["Osm:Password"];

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            _logger.LogWarning("⚠️ OpenStreetMap credentials are not configured in appsettings.json. Skipping actual API submission.");
            // Dev mode: return a simulated OSM Node ID for testing purposes
            var simulatedNodeId = $"sim_{DateTime.UtcNow.Ticks}";
            return (true, simulatedNodeId, "Mock mode: OSM credentials not configured, simulated upload.");
        }

        try
        {
            _logger.LogInformation("🚀 [OSM SYNC] Publishing place '{Name}' ({Category}) to OpenStreetMap...", place.Name, place.Category);

            // Set up Basic Authentication header
            var authBytes = Encoding.ASCII.GetBytes($"{username}:{password}");
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(authBytes));
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("JoyTopApp/1.0 (mehrojgethub2003@gmail.com)");

            // 1. Create a changeset
            var changesetXml = new XDocument(
                new XElement("osm",
                    new XElement("changeset",
                        new XElement("tag", new XAttribute("k", "created_by"), new XAttribute("v", "JoyTopApp 1.0")),
                        new XElement("tag", new XAttribute("k", "comment"), new XAttribute("v", $"Add new {place.Category} named '{place.Name}' via Joy Top App"))
                    )
                )
            );

            var changesetContent = new StringContent(changesetXml.ToString(), Encoding.UTF8, "application/xml");
            var changesetResponse = await _httpClient.PutAsync("https://api.openstreetmap.org/api/0.6/changeset/create", changesetContent);

            if (!changesetResponse.IsSuccessStatusCode)
            {
                var errContent = await changesetResponse.Content.ReadAsStringAsync();
                _logger.LogError("❌ [OSM SYNC] Failed to create changeset: {Error}", errContent);
                return (false, null, $"Changeset error: {errContent}");
            }

            var changesetId = (await changesetResponse.Content.ReadAsStringAsync()).Trim();
            _logger.LogInformation("✅ [OSM SYNC] Created changeset: {ChangesetId}", changesetId);

            // 2. Map Place category & details to OSM tags
            var tags = MapPlaceToOsmTags(place);

            // 3. Create the Node XML
            var nodeElement = new XElement("node",
                new XAttribute("lat", place.Latitude.ToString("F7", System.Globalization.CultureInfo.InvariantCulture)),
                new XAttribute("lon", place.Longitude.ToString("F7", System.Globalization.CultureInfo.InvariantCulture)),
                new XAttribute("changeset", changesetId)
            );

            // Add tags to Node
            nodeElement.Add(new XElement("tag", new XAttribute("k", "name"), new XAttribute("v", place.Name)));
            
            if (!string.IsNullOrEmpty(place.PhoneNumber))
            {
                nodeElement.Add(new XElement("tag", new XAttribute("k", "phone"), new XAttribute("v", place.PhoneNumber)));
            }

            if (!string.IsNullOrEmpty(place.WorkingHours))
            {
                nodeElement.Add(new XElement("tag", new XAttribute("k", "opening_hours"), new XAttribute("v", place.WorkingHours)));
            }

            foreach (var tag in tags)
            {
                nodeElement.Add(new XElement("tag", new XAttribute("k", tag.Key), new XAttribute("v", tag.Value)));
            }

            var nodeXml = new XDocument(new XElement("osm", nodeElement));
            
            // Send request to create node
            var nodeContent = new StringContent(nodeXml.ToString(), Encoding.UTF8, "application/xml");
            var nodeResponse = await _httpClient.PutAsync("https://api.openstreetmap.org/api/0.6/node/create", nodeContent);

            if (!nodeResponse.IsSuccessStatusCode)
            {
                var errContent = await nodeResponse.Content.ReadAsStringAsync();
                _logger.LogError("❌ [OSM SYNC] Failed to create node: {Error}", errContent);
                
                // Attempt to close changeset even on failure
                await _httpClient.PutAsync($"https://api.openstreetmap.org/api/0.6/changeset/{changesetId}/close", null);
                
                return (false, null, $"Node error: {errContent}");
            }

            var osmNodeId = (await nodeResponse.Content.ReadAsStringAsync()).Trim();
            _logger.LogInformation("✅ [OSM SYNC] Successfully created Node ID: {NodeId}", osmNodeId);

            // 4. Close changeset
            var closeResponse = await _httpClient.PutAsync($"https://api.openstreetmap.org/api/0.6/changeset/{changesetId}/close", null);
            if (!closeResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning("⚠️ [OSM SYNC] Failed to close changeset {ChangesetId} cleanly, but node was created.", changesetId);
            }

            return (true, osmNodeId, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ [OSM SYNC] Unhandled exception occurred while publishing place to OpenStreetMap.");
            return (false, null, ex.Message);
        }
    }

    private Dictionary<string, string> MapPlaceToOsmTags(Place place)
    {
        var tags = new Dictionary<string, string>();

        switch (place.Category.ToLower())
        {
            case "masjid":
                tags["amenity"] = "place_of_worship";
                tags["religion"] = "muslim";
                break;

            case "zapravka":
                tags["amenity"] = "fuel";
                if (!string.IsNullOrEmpty(place.FuelType))
                {
                    if (place.FuelType == "cng") tags["fuel:cng"] = "yes";
                    else if (place.FuelType == "lpg") tags["fuel:lpg"] = "yes";
                    else if (place.FuelType == "petrol") tags["fuel:octane_91"] = "yes";
                }
                break;

            case "oshxona":
                tags["amenity"] = "restaurant";
                if (!string.IsNullOrEmpty(place.OshxonaType))
                {
                    if (place.OshxonaType == "cafe") tags["amenity"] = "cafe";
                    else if (place.OshxonaType == "fast_food") tags["amenity"] = "fast_food";
                    else if (place.OshxonaType == "national") tags["cuisine"] = "uzbek";
                }
                break;

            case "do'kon":
            case "dokon":
                tags["shop"] = "yes";
                if (!string.IsNullOrEmpty(place.ShopType))
                {
                    tags["shop"] = place.ShopType; // e.g. supermarket, convenience, bakery, butcher, clothes
                }
                break;

            case "ustaxona":
                tags["shop"] = "car_repair"; // Default
                if (!string.IsNullOrEmpty(place.UstaxonaType))
                {
                    if (place.UstaxonaType == "car") tags["shop"] = "car_repair";
                    else if (place.UstaxonaType == "bicycle") tags["shop"] = "bicycle";
                    else if (place.UstaxonaType == "phone") tags["shop"] = "mobile_phone";
                    else if (place.UstaxonaType == "tailor") tags["craft"] = "tailor";
                }
                break;
        }

        return tags;
    }
}
