namespace MdmBilling.Services.Udil;

public class UdilOptions
{
    public bool UseFake { get; set; } = true;

    public int ConnectTimeoutSeconds { get; set; } = 15;

    public int CommandTimeoutSeconds { get; set; } = 60;

    public bool Encrypt { get; set; } = true;

    public bool TrustServerCertificate { get; set; } = false;
}