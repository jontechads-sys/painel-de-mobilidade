namespace Mobilidade.Domain.Modelos;

public sealed record Periodo
{
    public DateTime Inicio { get; }
    public DateTime? Fim { get; }

    public Periodo(DateTime inicio, DateTime? fim = null)
    {
        if (fim.HasValue && fim.Value < inicio)
            throw new ArgumentException("O fim do período não pode ser anterior ao início.", nameof(fim));

        Inicio = inicio;
        Fim = fim;
    }

    public bool EstaEmAberto => Fim is null;

    public TimeSpan? Duracao => Fim.HasValue ? Fim.Value - Inicio : null;

    public bool Contem(DateTime momento)
        => momento >= Inicio && (Fim is null || momento <= Fim);
}