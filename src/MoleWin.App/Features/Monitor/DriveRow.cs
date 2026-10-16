using MoleWin.Core.Monitor;

namespace MoleWin.App.Features.Monitor;

public sealed record DriveRow(string Root, double UsedPercent, LoadLevel Level, string FreeText, string Detail);
