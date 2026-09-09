namespace Mobilidade.Domain.Modelos;

public enum TipoIncidente
{
    Colisao,
    Alagamento,
    Obra,
    BuracoNaVia,
    SemaforoApagado,
    Engarrafamento
}

public enum Severidade
{
    Baixa,
    Media,
    Alta,
    NaoTriada
}

public sealed class Incidente
{
    public int Id { get; set; }
    public DateTime DataHora { get; private set; }
    public Incidente(DateTime dataHora, Severidade severidade)
    {
        if (dataHora > DateTime.UtcNow)
        throw new ArgumentException("DataHora não pode ser futura.", nameof(dataHora));
        DataHora = dataHora;

        if (!Enum.IsDefined(severidade))
            throw new ArgumentException("Esse valor não corresponde a lista de Severidades.", nameof(severidade));
        if (severidade == Severidade.NaoTriada)
            throw new ArgumentException("Severidade Ausente", nameof(severidade));
        Severidade = severidade;
    }
    public TipoIncidente Tipo { get; set; }
    public Severidade Severidade { get; private set; }
    public int BairroId { get; set; }
    public string? Descricao { get; set; }

    public Bairro? Bairro { get; set; }
    public ICollection<Atendimento> Atendimentos { get; set; } = new List<Atendimento>();
}