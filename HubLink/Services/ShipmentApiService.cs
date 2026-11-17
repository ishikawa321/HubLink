using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using HubLink.Models;

namespace HubLink.Services;

public class ShipmentApiService
{
    private static readonly HttpClient Client = new()
    {
        Timeout = Timeout.InfiniteTimeSpan
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<List<Shipment>> GetShipmentsAsync(
        string baseAddress,
        string branchCode,
        string mode,
        int timeoutSeconds)
    {
        if (!Uri.TryCreate(baseAddress.Trim(), UriKind.Absolute, out var baseUri)
            || (baseUri.Scheme != "http" && baseUri.Scheme != "https"))
        {
            throw new ArgumentException(
                "Enter a valid HTTP or HTTPS server address.");
        }

        branchCode = branchCode.Trim();

        if (!Regex.IsMatch(branchCode, @"\ABR-[0-9]{3}\z"))
        {
            throw new ArgumentException(
                "Enter a branch code such as BR-001.");
        }

        if (timeoutSeconds < 1 || timeoutSeconds > 120)
        {
            throw new ArgumentException(
                "Timeout must be between 1 and 120 seconds.");
        }

        string[] modes =
        {
            "normal", "empty", "bad-request",
            "server-error", "invalid-json", "delay"
        };

        if (!modes.Contains(mode))
        {
            throw new ArgumentException("Select a valid request mode.");
        }

        string path = mode == "normal"
            ? "/api/shipments"
            : "/api/test/shipments";

        string query = $"?branchCode={Uri.EscapeDataString(branchCode)}";

        if (mode != "normal")
        {
            query += $"&mode={Uri.EscapeDataString(mode)}";
        }

        var requestUri = new Uri(baseUri, path + query);

        using var timeout = new CancellationTokenSource(
            TimeSpan.FromSeconds(timeoutSeconds));

        using var response = await Client.GetAsync(
            requestUri,
            HttpCompletionOption.ResponseHeadersRead,
            timeout.Token);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"The server returned HTTP {(int)response.StatusCode} " +
                $"({response.ReasonPhrase}). Check the settings and retry.");
        }

        string json = await response.Content.ReadAsStringAsync(timeout.Token);

        var data = JsonSerializer.Deserialize<ShipmentResponse>(
            json, JsonOptions);

        if (data is null
            || data.BranchCode != branchCode
            || data.Shipments is null)
        {
            throw new InvalidDataException(
                "The response does not contain valid data for this branch.");
        }

        var shipments = new List<Shipment>();
        var trackingNumbers = new HashSet<string>(StringComparer.Ordinal);

        string[] statuses =
        {
            "Pending", "InTransit", "Delivered", "Exception"
        };

        foreach (var shipment in data.Shipments)
        {
            if (shipment is null
                || string.IsNullOrWhiteSpace(shipment.TrackingNumber)
                || shipment.BranchCode != branchCode
                || string.IsNullOrWhiteSpace(shipment.Origin)
                || string.IsNullOrWhiteSpace(shipment.Destination)
                || !statuses.Contains(shipment.Status)
                || shipment.LastUpdatedUtc is null
                || shipment.LastUpdatedUtc.Value.Offset != TimeSpan.Zero)
            {
                throw new InvalidDataException(
                    "The server returned an incomplete or invalid shipment.");
            }

            if (!trackingNumbers.Add(shipment.TrackingNumber))
            {
                throw new InvalidDataException(
                    "The server returned duplicate tracking numbers.");
            }

            shipments.Add(shipment);
        }

        return shipments;
    }
}