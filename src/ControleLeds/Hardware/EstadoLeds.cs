namespace ControleLeds.Hardware;

/// <summary>
/// Último comando que o app enviou com sucesso e o último modo aceso, para o Ligar voltar a ele.
/// Modo nulo = nenhum comando ainda.
/// </summary>
public sealed record EstadoLeds(
    string? Modo = null,
    byte R = 0xFF, byte G = 0xFF, byte B = 0xFF,
    string UltimoModoAceso = ModoLuz.Estatico)
{
    public static readonly EstadoLeds Desconhecido = new();

    public bool Conhecido => Modo is not null;

    public bool Ligado => Conhecido && !Modo!.Equals(ModoLuz.Desligado, StringComparison.OrdinalIgnoreCase);
}
