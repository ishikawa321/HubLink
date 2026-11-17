using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HubLink.Models;
using HubLink.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Tmds.DBus.Protocol;

namespace HubLink.ViewModels;

public partial class HubLinkViewModel : ViewModelBase
{
    private readonly ShipmentApiService _api;
    private readonly CsvExportService _exporter;
    private List<Shipment> _allShipments = new();

    public ObservableCollection<Shipment> DisplayedShipments { get; } = new();

    public string[] StatusOptions { get; } =
    {
        "All", "Pending", "InTransit", "Delivered", "Exception"
    };

    public string[] RequestModes { get; } =
    {
        "normal", "empty", "bad-request",
        "server-error", "invalid-json", "delay"
    };

    [ObservableProperty]
    private string baseAddress = "http://127.0.0.1:5080";

    [ObservableProperty]
    private string branchCode = "BR-001";

    [ObservableProperty]
    private string requestMode = "normal";

    [ObservableProperty]
    private string timeoutSeconds = "5";

    [ObservableProperty]
    private string selectedStatus = "All";

    [ObservableProperty]
    private string message = "Click Refresh to retrieve shipments.";

    [ObservableProperty]
    private string summaryText = "No shipment data loaded.";

    [ObservableProperty]
    private string exportPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        $"HubLink-{DateTime.Now:yyyyMMdd-HHmmss}.csv");

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    [NotifyCanExecuteChangedFor(nameof(RefreshCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExportCommand))]
    private bool isBusy;

    public bool IsNotBusy => !IsBusy;

    public HubLinkViewModel(
        ShipmentApiService api,
        CsvExportService exporter)
    {
        _api = api;
        _exporter = exporter;
    }

    private bool CanRun() => !IsBusy;

    partial void OnSelectedStatusChanged(string value)
    {
        ApplyFilter();
    }

    partial void OnBaseAddressChanged(string value)
    {
        ClearForSettingsChange();
    }

    partial void OnBranchCodeChanged(string value)
    {
        ClearForSettingsChange();
    }

    partial void OnRequestModeChanged(string value)
    {
        ClearForSettingsChange();
    }

    private void ClearForSettingsChange()
    {
        _allShipments.Clear();
        DisplayedShipments.Clear();
        SummaryText = "No shipment data loaded.";
        Message = "Settings changed. Click Refresh.";
    }

    private void ApplyFilter()
    {
        DisplayedShipments.Clear();

        foreach (var shipment in _allShipments)
        {
            if (SelectedStatus == "All"
                || shipment.Status == SelectedStatus)
            {
                DisplayedShipments.Add(shipment);
            }
        }
    }

    private void UpdateSummary()
    {
        int pending = _allShipments.Count(s => s.Status == "Pending");
        int transit = _allShipments.Count(s => s.Status == "InTransit");
        int delivered = _allShipments.Count(s => s.Status == "Delivered");
        int exceptions = _allShipments.Count(s => s.Status == "Exception");

        SummaryText =
            $"All retrieved branch shipments: {_allShipments.Count}\n" +
            $"Pending: {pending} | InTransit: {transit} | " +
            $"Delivered: {delivered} | Exception: {exceptions}";
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task RefreshAsync()
    {
        if (!int.TryParse(TimeoutSeconds, out int seconds)
            || seconds < 1 || seconds > 120)
        {
            Message = "Enter a timeout between 1 and 120 seconds.";
            return;
        }

        IsBusy = true;

        // Clear old data so a failed refresh cannot look successful.
        _allShipments.Clear();
        DisplayedShipments.Clear();
        SummaryText = "Loading...";
        Message = "Retrieving shipments...";

        try
        {
            _allShipments = await _api.GetShipmentsAsync(
                BaseAddress, BranchCode, RequestMode, seconds);

            UpdateSummary();
            ApplyFilter();

            Message = _allShipments.Count == 0
                ? "Request succeeded. No shipments were returned."
                : $"Successfully loaded {_allShipments.Count} shipments " +
                  $"for {BranchCode.Trim()}.";
        }
        catch (OperationCanceledException)
        {
            RetrievalFailed(
                "The request timed out. Increase the timeout or retry.");
        }
        catch (JsonException)
        {
            RetrievalFailed(
                "The server returned invalid JSON or incompatible field values.");
        }
        catch (InvalidDataException exception)
        {
            RetrievalFailed(exception.Message);
        }
        catch (HttpRequestException exception)
        {
            Debug.WriteLine(exception);

            RetrievalFailed(
                "HTTP request failed. Check that the server is running " +
                "and the address is correct. " + exception.Message);
        }
        catch (ArgumentException exception)
        {
            RetrievalFailed(exception.Message);
        }
        catch (Exception exception)
        {
            Debug.WriteLine(exception);
            RetrievalFailed("Could not retrieve shipments. Check settings and retry.");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void RetrievalFailed(string explanation)
    {
        _allShipments.Clear();
        DisplayedShipments.Clear();
        SummaryText = "No current data: retrieval failed.";
        Message = explanation + " Click Refresh to retry.";
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task ExportAsync()
    {
        if (DisplayedShipments.Count == 0)
        {
            Message = "There are no displayed shipments to export.";
            return;
        }

        // Snapshot exactly the records visible under the active filter.
        var records = DisplayedShipments.ToList();
        string path = ExportPath;

        IsBusy = true;
        Message = "Exporting CSV...";

        try
        {
            await Task.Run(() => _exporter.ExportAsync(path, records));

            Message =
                $"Exported {records.Count} shipment(s) to " +
                Path.GetFullPath(path.Trim());
        }
        catch (UnauthorizedAccessException)
        {
            Message = "Export failed: you do not have permission to write there.";
        }
        catch (IOException exception)
        {
            Debug.WriteLine(exception);

            Message =
                "Export failed. Check that the folder exists and choose " +
                "a filename that does not already exist.";
        }
        catch (ArgumentException exception)
        {
            Message = "Export failed: " + exception.Message;
        }
        catch (Exception exception)
        {
            Debug.WriteLine(exception);
            Message = "Export failed: " + exception.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}