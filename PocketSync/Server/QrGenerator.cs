using QRCoder;

namespace PocketSync.Server;

/// <summary>QR-ul de pairing: conține URL-ul cu token, scanabil cu camera telefonului.</summary>
public static class QrGenerator
{
    public static byte[] GeneratePng(string content, int pixelsPerModule = 10)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q);
        using var png = new PngByteQRCode(data);
        return png.GetGraphic(pixelsPerModule);
    }
}
