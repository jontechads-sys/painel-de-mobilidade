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
    public Incidente(DateTime dataHora)
    {
        if (dataHora > DateTime.UtcNow)
        throw new ArgumentException("DataHora não pode ser futura.", nameof(dataHora));
        DataHora = dataHora;
    }
    public TipoIncidente Tipo { get; set; }
    public Severidade Severidade { get; set; }
    public int BairroId { get; set; }
    public string? Descricao { get; set; }

    public Bairro? Bairro { get; set; }
    public ICollection<Atendimento> Atendimentos { get; set; } = new List<Atendimento>();
}