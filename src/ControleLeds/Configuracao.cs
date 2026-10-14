using System.IO;
using System.Text.Json;

namespace ControleLeds;

/// <summary>
/// Onde está o OpenRGB e qual dispositivo controla o water cooler.
/// Os valores padrão são os do PC do Ângelo; um ControleLeds.json ao lado do .exe sobrescreve.
/// </summary>
public sealed record Configuracao
{
    public string CaminhoOpenRgb { get; init; } = @"D:\Programas\OpenRGB\OpenRGB.exe";
    public string Dispositivo { get; init; } = "ASUS PRIME B550M-A";

    /// <summary>Modos (nome do OpenRGB) que ficam no menu "Mais modos" em vez da grade principal.</summary>
    public string[] ModosNoMenu { get; init; } = ["Direct", "Flashing", "Chase"];

    public const string NomeArquivo = "ControleLeds.json";

    public static Configuracao Carregar()
    {
        var caminho = Path.Combine(AppContext.BaseDirectory, NomeArquivo);
        if (!File.Exists(caminho))
            return new Configuracao();

        return JsonSerializer.Deserialize<Configuracao>(File.ReadAllText(caminho),
                   new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
               ?? new Configuracao();
    }
}
