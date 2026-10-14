using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;

// Gera um .ico (PNG por tamanho) com um anel de LED em arco-íris sobre fundo escuro.
var destino = args[0];
var previa = args.Length > 1 ? args[1] : null;
int[] tamanhos = [16, 24, 32, 48, 64, 128, 256];

var t = new Thread(() =>
{
    var pngs = tamanhos.Select(Renderizar).ToList();

    using var ico = new BinaryWriter(File.Create(destino));
    ico.Write((short)0); ico.Write((short)1); ico.Write((short)pngs.Count);
    var deslocamento = 6 + 16 * pngs.Count;
    for (var i = 0; i < pngs.Count; i++)
    {
        var lado = tamanhos[i] >= 256 ? 0 : tamanhos[i];
        ico.Write((byte)lado); ico.Write((byte)lado); ico.Write((byte)0); ico.Write((byte)0);
        ico.Write((short)1); ico.Write((short)32);
        ico.Write(pngs[i].Length); ico.Write(deslocamento);
        deslocamento += pngs[i].Length;
    }
    foreach (var png in pngs) ico.Write(png);

    if (previa is not null) File.WriteAllBytes(previa, pngs[^1]);
});
t.SetApartmentState(ApartmentState.STA);
t.Start();
t.Join();

static byte[] Renderizar(int lado)
{
    const double B = 256; // desenha em 256 e escala
    var visual = new DrawingVisual();
    using (var dc = visual.RenderOpen())
    {
        dc.PushTransform(new ScaleTransform(lado / B, lado / B));

        // Fundo
        dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(0x15, 0x17, 0x1C)), null,
            new Rect(8, 8, 240, 240), 52, 52);

        var centro = new Point(128, 128);
        const double raio = 78;
        var espessura = lado <= 24 ? 40 : 30;

        // Brilho difuso por trás do anel
        var halo = new RadialGradientBrush(
            [new GradientStop(Color.FromArgb(0x70, 0xB0, 0x60, 0xFF), 0.45), new GradientStop(Color.FromArgb(0, 0x15, 0x17, 0x1C), 1)]);
        dc.DrawEllipse(halo, null, centro, 118, 118);

        // Anel em arco-íris: 72 segmentos, um matiz por segmento
        const int segmentos = 72;
        for (var i = 0; i < segmentos; i++)
        {
            var a0 = (i - 0.6) * 360.0 / segmentos;
            var a1 = (i + 1.0) * 360.0 / segmentos;
            var cor = DeMatiz(i * 360.0 / segmentos);
            var caneta = new Pen(new SolidColorBrush(cor), espessura) { StartLineCap = PenLineCap.Flat, EndLineCap = PenLineCap.Flat };
            dc.DrawGeometry(null, caneta, Arco(centro, raio, a0, a1));
        }

        // Miolo da ventoinha e ponto de luz
        dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(0x1D, 0x20, 0x27)), null, centro, raio - espessura / 2.0, raio - espessura / 2.0);
        if (lado >= 32)
        {
            var luz = new RadialGradientBrush(
                [new GradientStop(Colors.White, 0), new GradientStop(Color.FromArgb(0xC0, 0xFF, 0xFF, 0xFF), 0.35), new GradientStop(Color.FromArgb(0, 0xFF, 0xFF, 0xFF), 1)]);
            dc.DrawEllipse(luz, null, centro, 26, 26);
        }
        dc.Pop();
    }

    var bmp = new RenderTargetBitmap(lado, lado, 96, 96, PixelFormats.Pbgra32);
    bmp.Render(visual);
    var enc = new PngBitmapEncoder();
    enc.Frames.Add(BitmapFrame.Create(bmp));
    using var ms = new MemoryStream();
    enc.Save(ms);
    return ms.ToArray();
}

static Geometry Arco(Point c, double r, double grausIni, double grausFim)
{
    Point P(double g) => new(c.X + r * Math.Cos((g - 90) * Math.PI / 180), c.Y + r * Math.Sin((g - 90) * Math.PI / 180));
    var fig = new PathFigure { StartPoint = P(grausIni), IsClosed = false };
    fig.Segments.Add(new ArcSegment(P(grausFim), new Size(r, r), 0, false, SweepDirection.Clockwise, true));
    return new PathGeometry([fig]);
}

static Color DeMatiz(double matiz)
{
    var x = 1 - Math.Abs(matiz / 60 % 2 - 1);
    var (r, g, b) = (int)(matiz / 60) switch
    {
        0 => (1d, x, 0d), 1 => (x, 1d, 0d), 2 => (0d, 1d, x),
        3 => (0d, x, 1d), 4 => (x, 0d, 1d), _ => (1d, 0d, x)
    };
    return Color.FromRgb((byte)(r * 255), (byte)(g * 255), (byte)(b * 255));
}
