namespace Mobilidade.Domain.Modelos;

public sealed record Severidade
{
    public static readonly Severidade Baixa = new(0, nameof(Baixa));
    public static readonly Severidade Media = new(1, nameof(Media));
    public static readonly Severidade Alta = new(2, nameof(Alta));
    public static readonly Severidade NaoTriada = new(3, nameof(NaoTriada));

    public int Valor { get; }
    public string Nome { get; }

    private Severidade(int valor, string nome)
    {
        Valor = valor;
        Nome = nome;
    }

    public bool DeveriaEscalonar(TimeSpan tempoDecorrido)
    {
        return this == Alta && tempoDecorrido > TimeSpan.FromHours(2);
    }

    public static Severidade DeInt(int valor) => valor switch
    {
        0 => Baixa,
        1 => Media,
        2 => Alta,
        3 => NaoTriada,
        _ => throw new ArgumentException($"Valor de severidade inválido: {valor}", nameof(valor))
    };

    public override string ToString() => Nome;

    public static bool TryParse(string? texto, out Severidade resultado)
    {
        switch (texto?.Trim().ToLowerInvariant())
        {
            case "baixa": resultado = Baixa; return true;
            case "media": resultado = Media; return true;
            case "alta": resultado = Alta; return true;
            case "naotriada": resultado = NaoTriada; return true;
            default: resultado = Baixa; return false;
        }
    }
}