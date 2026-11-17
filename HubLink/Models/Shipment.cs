using System;

namespace HubLink.Models;

public class Shipment
{
    public string? TrackingNumber { get; set; }

    public string? BranchCode { get; set; }

    public string? Origin { get; set; }

    public string? Destination { get; set; }

    public string? Status { get; set; }

    public DateTimeOffset? LastUpdatedUtc { get; set; }
}