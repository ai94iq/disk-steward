namespace MoleWin.Core.Monitor;

public static class LoadLevels
{
    // CPU spikes are normal; only sustained high load deserves color.
    public static LoadLevel Cpu(double percent) => From(percent, 70, 90);

    // Memory and disks: full is the problem, not busy.
    public static LoadLevel Fill(double percent) => From(percent, 80, 95);

    public static LoadLevel Health(int score) => score >= 75 ? LoadLevel.Normal : score >= 50 ? LoadLevel.High : LoadLevel.Critical;

    private static LoadLevel From(double percent, double high, double critical) =>
        percent >= critical ? LoadLevel.Critical : percent >= high ? LoadLevel.High : LoadLevel.Normal;
}
