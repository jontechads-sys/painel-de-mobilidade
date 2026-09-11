using Mobilidade.Domain.ValueObjects;

namespace Mobilidade.Domain.Entity;

public enum TipoIncidente
{
    Colisao,
    Alagamento,
    Obra,
    BuracoNaVia,
    SemaforoApagado,
    Engarrafamento
}
public sealed class Incidente
{
    public int Id { get; set; }
    public DateTime DataHora { get; private set; }
    private Incidente(DateTime dataHora, Severidade severidade)
    {
        if (dataHora > DateTime.UtcNow)
            throw new ArgumentException("DataHora não pode ser futura.", nameof(dataHora));
        DataHora = dataHora;

        if (severidade == Severidade.NaoTriada)
            throw new ArgumentException("Severidade Ausente", nameof(severidade));
        Severidade = severidade;
    }
    public TipoIncidente Tipo { get; set; }
    public Severidade Severidade { get; private set; }
    public int BairroId { get; set; }
    public string? Descricao { get; set; }

    public Bairro? Bairro { get; set; }
    private readonly List<Atendimento> _atendimentos = new();
    public IReadOnlyCollection<Atendimento> Atendimentos => _atendimentos;

    public static Incidente Abrir(
        DateTime dataHora,
        Severidade severidade,
        int bairroId,
        string? descricao,
        string orgaoResponsavel,
        string situacao)
    {
        var incidente = new Incidente(dataHora, severidade)
        {
            BairroId = bairroId,
            Descricao = descricao
        };

        var atendimento = new Atendimento(incidente.Id, orgaoResponsavel, dataHora, situacao);
        atendimento.Incidente = incidente;
        incidente._atendimentos.Add(atendimento);
        return incidente;
    }
    public bool Encerrado { get; private set; }

    public void Encerrar()
    {
        if (Encerrado)
        {
            throw new InvalidOperationException("Incidente já foi encerrado");
        }
        Encerrado = true;
    }
    public void RegistrarAtendimento(DateTime dataHora, string orgaoResponsavel, string situacao)
    {
        if (Encerrado)
        {
            throw new InvalidOperationException("Incidente encerrado não aceita novo atendimento.");
        }
        var atendimento = new Atendimento(Id, orgaoResponsavel, dataHora, situacao);
        {
            atendimento.Incidente = this;
            _atendimentos.Add(atendimento);
        }
    }
}