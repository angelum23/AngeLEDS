using System.IO;
using System.Text.Json;

namespace ControleLeds.Hardware;

/// <summary>Um modo de luz do controlador, com o nome que o OpenRGB usa na CLI (-m).</summary>
public sealed record ModoLuz(string Nome, string Rotulo, bool UsaCor, string Descricao)
{
    public const string Desligado = "Off";
    public const string Estatico = "Static";
}

/// <summary>
/// Modos que o OpenRGB expõe para o dispositivo, lidos do Configuration.json dele.
/// Se o arquivo não existir ou não trouxer o dispositivo, vale a lista medida na B550M-A em 2026-10-13.
/// </summary>
public static class CatalogoModos
{
    private static readonly Dictionary<string, (string Rotulo, string Descricao)> Traducoes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Static"] = ("Estático", "Cor fixa"),
            ["Breathing"] = ("Respirar", "A cor acende e apaga devagar"),
            ["Flashing"] = ("Piscar", "A cor pisca"),
            ["Spectrum Cycle"] = ("Ciclo de cores", "Todos os LEDs passam pelas cores juntos"),
            ["Rainbow"] = ("Arco-íris", "As cores correm pelos LEDs"),
            ["Chase Fade"] = ("Perseguição suave", "Um trecho aceso corre pelos LEDs, com rastro e pausa longa entre as passadas"),
            ["Chase"] = ("Perseguição", "Um trecho aceso corre pelos LEDs, com pausa longa entre as passadas"),
            ["Direct"] = ("Direto", "Cor LED a LED, enviada pelo OpenRGB; com uma cor só, fica igual ao Estático")
        };

    private static readonly (string Nome, int ColorMode)[] Padrao =
    [
        ("Direct", 1), ("Static", 2), ("Breathing", 2), ("Flashing", 2),
        ("Spectrum Cycle", 0), ("Rainbow", 0), ("Chase Fade", 2), ("Chase", 2)
    ];

    private static readonly string ArquivoOpenRgb = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OpenRGB", "Configuration.json");

    /// <summary>Modos que acendem (sem o Off, que é o botão Desligar), na ordem do OpenRGB.</summary>
    public static IReadOnlyList<ModoLuz> Carregar(string dispositivo)
    {
        var lidos = LerDoOpenRgb(dispositivo);
        var fonte = lidos.Count > 0 ? lidos : Padrao.ToList();

        return fonte
            .Where(m => !m.Nome.Equals(ModoLuz.Desligado, StringComparison.OrdinalIgnoreCase))
            .Select(m => Criar(m.Nome, m.ColorMode))
            .ToList();
    }

    // color_mode do OpenRGB: 0 nenhum, 1 por LED, 2 específico do modo, 3 aleatório.
    private static ModoLuz Criar(string nome, int colorMode)
    {
        var (rotulo, descricao) = Traducoes.TryGetValue(nome, out var t) ? t : (nome, "Modo do OpenRGB");
        return new ModoLuz(nome, rotulo, colorMode is 1 or 2, descricao);
    }

    private static List<(string Nome, int ColorMode)> LerDoOpenRgb(string dispositivo)
    {
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(ArquivoOpenRgb));
            var controlador = doc.RootElement.GetProperty("controllers").EnumerateArray()
                .FirstOrDefault(c => c.GetProperty("name").GetString()?.Equals(dispositivo, StringComparison.OrdinalIgnoreCase) == true);

            if (controlador.ValueKind != JsonValueKind.Object)
                return [];

            return controlador.GetProperty("modes").EnumerateArray()
                .Select(m => (m.GetProperty("name").GetString() ?? "", m.GetProperty("color_mode").GetInt32()))
                .Where(m => m.Item1.Length > 0)
                .ToList();
        }
        catch (Exception)
        {
            return [];
        }
    }
}
