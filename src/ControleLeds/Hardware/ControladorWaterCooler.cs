using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace ControleLeds.Hardware;

/// <summary>
/// Liga e desliga os LEDs do water cooler pela linha de comando do OpenRGB (o mesmo caminho do leds.sh).
/// A bomba e as 2 ventoinhas do radiador ficam todas no conector ARGB da placa, em paralelo:
/// recebem o mesmo sinal e não podem ser controladas separadamente.
/// </summary>
/// <remarks>
/// Por que não o servidor SDK: com o OpenRGB 1.0 e o OpenRGB.NET 3.1.1 (protocolo 4), o servidor ignora
/// a troca de modo e o modo Direct apaga sozinho no hardware (testado em 2026-10-13).
/// A CLI do OpenRGB no Windows não escreve nada no console, então o estado mostrado é o do último
/// comando que este app enviou com sucesso, guardado em %LOCALAPPDATA%\ControleLeds\estado.json.
/// </remarks>
public sealed class ControladorWaterCooler
{
    private static readonly TimeSpan LimiteComando = TimeSpan.FromSeconds(30);
    private static readonly string ArquivoEstado = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ControleLeds", "estado.json");

    private readonly Configuracao _config;
    private readonly object _trava = new();

    public ControladorWaterCooler(Configuracao config) => _config = config;

    /// <summary>Confere se dá para mandar comandos agora; devolve o motivo quando não dá.</summary>
    public string? Impedimento()
    {
        if (!File.Exists(_config.CaminhoOpenRgb))
            return $"OpenRGB não encontrado em {_config.CaminhoOpenRgb}. Ajuste o {Configuracao.NomeArquivo}.";

        if (Process.GetProcessesByName("OpenRGB").Length > 0)
            return "A interface do OpenRGB está aberta e segura o USB. Feche-a para usar este app.";

        return null;
    }

    public EstadoLeds LerEstado()
    {
        try
        {
            return File.Exists(ArquivoEstado)
                ? JsonSerializer.Deserialize<EstadoLeds>(File.ReadAllText(ArquivoEstado)) ?? EstadoLeds.Desconhecido
                : EstadoLeds.Desconhecido;
        }
        catch (Exception)
        {
            return EstadoLeds.Desconhecido;
        }
    }

    /// <summary>Apaga mantendo o modo e a cor anteriores, para o Ligar voltar a eles.</summary>
    public EstadoLeds Desligar()
    {
        var atual = LerEstado();
        return Executar(atual with { Modo = ModoLuz.Desligado }, "-m", "off");
    }

    /// <summary>Aplica um modo; a cor só vai junto quando o modo aceita cor.</summary>
    public EstadoLeds Aplicar(ModoLuz modo, byte r, byte g, byte b)
    {
        var novo = new EstadoLeds(modo.Nome, r, g, b, UltimoModoAceso: modo.Nome);
        var argumentos = new List<string> { "-m", modo.Nome.ToLowerInvariant() };
        if (modo.UsaCor)
            argumentos.AddRange(["-c", $"{r:X2}{g:X2}{b:X2}"]);

        return Executar(novo, [.. argumentos]);
    }

    private EstadoLeds Executar(EstadoLeds esperado, params string[] argumentos)
    {
        lock (_trava)
        {
            if (Impedimento() is { } motivo)
                throw new InvalidOperationException(motivo);

            var inicio = new ProcessStartInfo(_config.CaminhoOpenRgb)
            {
                UseShellExecute = false,
                CreateNoWindow = true
            };
            foreach (var arg in new[] { "--noautoconnect", "-d", _config.Dispositivo }.Concat(argumentos))
                inicio.ArgumentList.Add(arg);

            using var processo = Process.Start(inicio)
                                 ?? throw new InvalidOperationException("Não foi possível iniciar o OpenRGB.");

            if (!processo.WaitForExit(LimiteComando))
            {
                processo.Kill();
                throw new TimeoutException("O OpenRGB não terminou o comando a tempo.");
            }

            if (processo.ExitCode != 0)
                throw new InvalidOperationException(
                    $"O OpenRGB saiu com código {processo.ExitCode}. Veja o log em %APPDATA%\\OpenRGB\\logs.");

            Directory.CreateDirectory(Path.GetDirectoryName(ArquivoEstado)!);
            File.WriteAllText(ArquivoEstado, JsonSerializer.Serialize(esperado));
            return esperado;
        }
    }
}
