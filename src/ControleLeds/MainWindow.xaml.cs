using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;
using ControleLeds.Hardware;

namespace ControleLeds;

public partial class MainWindow : Window
{
    private static readonly (string Nome, Color Cor)[] Paleta =
    [
        ("Branco", Color.FromRgb(0xFF, 0xFF, 0xFF)),
        ("Vermelho", Color.FromRgb(0xFF, 0x00, 0x00)),
        ("Laranja", Color.FromRgb(0xFF, 0x60, 0x00)),
        ("Amarelo", Color.FromRgb(0xFF, 0xD0, 0x00)),
        ("Verde", Color.FromRgb(0x00, 0xFF, 0x40)),
        ("Ciano", Color.FromRgb(0x00, 0xFF, 0xFF)),
        ("Azul", Color.FromRgb(0x00, 0x40, 0xFF)),
        ("Roxo", Color.FromRgb(0x90, 0x00, 0xFF)),
        ("Rosa", Color.FromRgb(0xFF, 0x00, 0xA0))
    ];

    private readonly Configuracao _config = Configuracao.Carregar();
    private readonly ControladorWaterCooler _controlador;
    private readonly IReadOnlyList<ModoLuz> _modos;
    private readonly Dictionary<string, Button> _botoesModo = new(StringComparer.OrdinalIgnoreCase);
    private readonly DispatcherTimer _animacao = new() { Interval = TimeSpan.FromMilliseconds(40) };
    private readonly Stopwatch _relogio = Stopwatch.StartNew();
    private EstadoLeds _estado = EstadoLeds.Desconhecido;
    private Color _cor = Colors.White;

    public MainWindow()
    {
        InitializeComponent();
        _controlador = new ControladorWaterCooler(_config);
        _modos = CatalogoModos.Carregar(_config.Dispositivo);
        _animacao.Tick += (_, _) => Animar();
        MontarPaleta();
        MontarModos();
    }

    /// <summary>Na ordem do desenho: R1, R2, bomba. Todos recebem o mesmo sinal.</summary>
    private Shape[] LedsWaterCooler => [LedR1, LedR2, LedBomba];

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        _estado = _controlador.LerEstado();
        _cor = Color.FromRgb(_estado.R, _estado.G, _estado.B);

        Desenhar();
        TextoConexao.Text = _controlador.Impedimento()
                            ?? $"Pronto · {_config.Dispositivo} · {_modos.Count} modos · estado = último comando enviado por este app";
    }

    private async void Ligar_Click(object sender, RoutedEventArgs e) =>
        await Aplicar(Modo(_estado.UltimoModoAceso) ?? Modo(ModoLuz.Estatico) ?? _modos[0]);

    private async void Desligar_Click(object sender, RoutedEventArgs e) =>
        await Executar(_controlador.Desligar, "Desligado");

    private void LedWaterCooler_Click(object sender, MouseButtonEventArgs e)
    {
        if (!BotaoLigar.IsEnabled)
            return;

        if (_estado.Ligado)
            Desligar_Click(sender, e);
        else
            Ligar_Click(sender, e);
    }

    private async void AplicarHex_Click(object sender, RoutedEventArgs e) => await AplicarHex();

    private async void CampoHex_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            await AplicarHex();
    }

    private async Task AplicarHex()
    {
        var texto = CampoHex.Text.Trim().TrimStart('#');
        if (texto.Length != 6 || !int.TryParse(texto, NumberStyles.HexNumber, null, out var valor))
        {
            TextoConexao.Text = $"Cor inválida: \"{CampoHex.Text}\". Use RRGGBB, por exemplo 00FFFF.";
            return;
        }

        await EscolherCor(Color.FromRgb((byte)(valor >> 16), (byte)(valor >> 8), (byte)valor));
    }

    /// <summary>A cor vale para o modo atual quando ele aceita cor; senão, passa para Estático.</summary>
    private async Task EscolherCor(Color cor)
    {
        _cor = cor;
        CampoHex.Text = Hex(cor);

        var modo = Modo(_estado.UltimoModoAceso);
        await Aplicar(modo is { UsaCor: true } ? modo : Modo(ModoLuz.Estatico) ?? _modos[0]);
    }

    private async Task Aplicar(ModoLuz modo)
    {
        var cor = _cor;
        var detalhe = modo.UsaCor ? $" em {Hex(cor)}" : "";
        await Executar(() => _controlador.Aplicar(modo, cor.R, cor.G, cor.B), $"{modo.Rotulo}{detalhe}");
    }

    /// <summary>Roda o comando fora da tela: cada chamada ao OpenRGB leva cerca de 1,5 s.</summary>
    private async Task Executar(Func<EstadoLeds> operacao, string mensagemSucesso)
    {
        HabilitarControles(false);
        TextoConexao.Text = "Enviando ao OpenRGB…";
        try
        {
            _estado = await Task.Run(operacao);
            TextoConexao.Text = $"{mensagemSucesso} · {DateTime.Now:HH:mm:ss}";
        }
        catch (Exception ex)
        {
            TextoConexao.Text = ex.Message;
        }
        finally
        {
            Desenhar();
            HabilitarControles(true);
        }
    }

    private void Desenhar()
    {
        var ultimo = Modo(_estado.UltimoModoAceso);
        var ativo = _estado.Ligado ? Modo(_estado.Modo!) : null;

        foreach (var (nome, botao) in _botoesModo)
        {
            var selecionado = ativo?.Nome.Equals(nome, StringComparison.OrdinalIgnoreCase) == true;
            botao.Background = selecionado ? new SolidColorBrush(Color.FromRgb(0x1E, 0x4F, 0x86)) : (Brush)FindResource("BotaoFundo");
            botao.BorderBrush = selecionado ? (Brush)FindResource("Destaque") : (Brush)FindResource("Borda");
        }

        CampoHex.Text = Hex(_cor);

        // Modo ativo escondido no menu: abre o menu para ele aparecer destacado.
        if (ativo is not null && PainelMaisModos.Children.Contains(_botoesModo[ativo.Nome]))
            AbrirMaisModos(true);

        if (!_estado.Conhecido)
        {
            TextoEstado.Text = "Estado desconhecido";
            TextoModo.Text = "Nenhum comando enviado ainda.";
        }
        else if (ativo is not null)
        {
            TextoEstado.Text = ativo.UsaCor ? $"{ativo.Rotulo} · {Hex(_cor)}" : ativo.Rotulo;
            TextoModo.Text = ativo.Descricao + (ativo.UsaCor ? "." : " (não usa a cor escolhida).");
        }
        else if (_estado.Ligado)
        {
            TextoEstado.Text = $"Modo {_estado.Modo}";
            TextoModo.Text = "Modo fora do catálogo atual.";
        }
        else
        {
            TextoEstado.Text = "Desligado";
            TextoModo.Text = ultimo is null ? "" : $"Ligar volta para: {ultimo.Rotulo}.";
        }

        if (_estado.Ligado)
            _animacao.Start();
        else
        {
            _animacao.Stop();
            PintarLeds(null, null, null);
        }
    }

    /// <summary>Pré-visualização aproximada do efeito, para saber o que está ativo.</summary>
    private void Animar()
    {
        var t = _relogio.Elapsed.TotalSeconds;
        var c = _cor;
        Color Escala(double k) => Color.FromRgb((byte)(c.R * k), (byte)(c.G * k), (byte)(c.B * k));

        Color Led(int i) => _estado.Modo switch
        {
            "Breathing" => Escala(0.12 + 0.88 * (0.5 - 0.5 * Math.Cos(2 * Math.PI * t / 3))),
            "Flashing" => t % 1 < 0.5 ? c : Escala(0.05),
            "Spectrum Cycle" => DeMatiz(t * 60 % 360),
            "Rainbow" => DeMatiz((t * 90 + i * 120) % 360),
            "Chase" => (int)(t * 3) % 3 == i ? c : Escala(0.08),
            "Chase Fade" => Escala(Math.Max(0.08, Math.Cos(2 * Math.PI * (t * 0.8 - i / 3.0)))),
            _ when Modo(_estado.Modo!) is { UsaCor: true } => c,
            _ => Colors.White
        };

        PintarLeds(Led(0), Led(1), Led(2));
    }

    private void PintarLeds(Color? r1, Color? r2, Color? bomba)
    {
        Color?[] cores = [r1, r2, bomba];
        var leds = LedsWaterCooler;
        for (var i = 0; i < leds.Length; i++)
            Pintar(leds[i], cores[i]);

        Pintar(IndicadorEstado, bomba);

    }

    /// <summary>Anéis (ventoinhas e bomba) acendem no contorno; o indicador do painel, no preenchimento.</summary>
    private void Pintar(Shape forma, Color? cor)
    {
        var anel = forma.Tag as string == "anel";
        var pincel = cor is { } c ? new SolidColorBrush(c) : (Brush)FindResource(anel ? "LedApagadoAnel" : "LedApagado");

        if (anel)
            forma.Stroke = pincel;
        else
            forma.Fill = pincel;

        forma.Effect = cor is { } g ? new DropShadowEffect { Color = g, BlurRadius = 18, ShadowDepth = 0, Opacity = 0.9 } : null;
    }

    private void MontarPaleta()
    {
        foreach (var (nome, cor) in Paleta)
        {
            var amostra = new Button
            {
                Style = (Style)FindResource("Amostra"),
                Background = new SolidColorBrush(cor),
                BorderBrush = (Brush)FindResource("Borda"),
                ToolTip = $"{nome} · {Hex(cor)}"
            };
            amostra.Click += async (_, _) => await EscolherCor(cor);
            PainelCores.Children.Add(amostra);
        }
    }

    private void MontarModos()
    {
        foreach (var modo in _modos)
        {
            var botao = new Button
            {
                Style = (Style)FindResource("Botao"),
                Content = modo.Rotulo,
                Padding = new Thickness(8, 6, 8, 6),
                Margin = new Thickness(0, 0, 6, 6),
                FontSize = 13,
                ToolTip = $"{modo.Descricao}{(modo.UsaCor ? " · usa a cor escolhida" : "")} · OpenRGB: {modo.Nome}"
            };
            botao.Click += async (_, _) => await Aplicar(modo);
            _botoesModo[modo.Nome] = botao;

            var noMenu = _config.ModosNoMenu.Contains(modo.Nome, StringComparer.OrdinalIgnoreCase);
            (noMenu ? PainelMaisModos : PainelModos).Children.Add(botao);
        }

        // O botão ocupa a próxima célula da grade principal, do mesmo tamanho dos modos.
        ((Panel)BotaoMaisModos.Parent).Children.Remove(BotaoMaisModos);
        if (PainelMaisModos.Children.Count > 0)
            PainelModos.Children.Add(BotaoMaisModos);
    }

    private void MaisModos_Click(object sender, RoutedEventArgs e) =>
        AbrirMaisModos(PainelMaisModos.Visibility != Visibility.Visible);

    private void AbrirMaisModos(bool aberto)
    {
        PainelMaisModos.Visibility = aberto ? Visibility.Visible : Visibility.Collapsed;
        TextoMaisModos.Text = aberto ? "Menos modos" : "Mais modos";
        GiroSetaMaisModos.Angle = aberto ? 180 : 0;
    }

    private ModoLuz? Modo(string nome) =>
        _modos.FirstOrDefault(m => m.Nome.Equals(nome, StringComparison.OrdinalIgnoreCase));

    private void HabilitarControles(bool habilitar)
    {
        foreach (var controle in new UIElement[] { BotaoLigar, BotaoDesligar, BotaoHex, CampoHex, PainelCores, PainelModos, PainelMaisModos })
            controle.IsEnabled = habilitar;
    }

    private static string Hex(Color c) => $"{c.R:X2}{c.G:X2}{c.B:X2}";

    private static Color DeMatiz(double matiz)
    {
        var x = 1 - Math.Abs(matiz / 60 % 2 - 1);
        var (r, g, b) = (int)(matiz / 60) switch
        {
            0 => (1d, x, 0d),
            1 => (x, 1d, 0d),
            2 => (0d, 1d, x),
            3 => (0d, x, 1d),
            4 => (x, 0d, 1d),
            _ => (1d, 0d, x)
        };
        return Color.FromRgb((byte)(r * 255), (byte)(g * 255), (byte)(b * 255));
    }
}
