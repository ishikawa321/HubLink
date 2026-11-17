using System.Collections.Generic;

namespace HubLink.Models;

public class ShipmentResponse
{
    public string? BranchCode { get; set; }

    public List<Shipment?>? Shipments { get; set; }
}