using DiskSteward.Core.Monitor;

namespace DiskSteward.App.Features.Monitor;

public sealed record DriveRow(string Root, double UsedPercent, LoadLevel Level, string FreeText, string Detail);
