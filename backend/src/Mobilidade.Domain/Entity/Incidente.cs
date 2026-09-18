using Mobilidade.Domain.ValueObjects;
using Mobilidade.Domain.Eventos;
using Mobilidade.Domain.Excecoes;

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
    public void LimparEventos() => _eventos.Clear();
    private readonly List<IEventoDeDominio> _eventos = new();
    public IReadOnlyCollection<IEventoDeDominio> Eventos => _eventos;
    private Incidente() { }
    public Periodo Periodo { get; private set; }
    public int Id { get; set; }
    private Incidente(Periodo periodo, Severidade severidade)
    {
        Periodo = periodo;

        if (periodo.Inicio > DateTime.UtcNow)
            throw new DomainException("Incidente não pode ser iniciado com data e hora futuras");

        if (severidade == Severidade.NaoTriada)
            throw new DomainException("Incidente não pode ser aberto sem severidade definida");
        Severidade = severidade;
    }
    public TipoIncidente Tipo { get; set; }
    public Severidade Severidade { get; private set; }
    public int BairroId { get; private set; }
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
        var periodo = new Periodo(dataHora);
        var incidente = new Incidente(periodo, severidade)
        {
            BairroId = bairroId,
            Descricao = descricao
        };

        var atendimento = new Atendimento(incidente.Id, orgaoResponsavel, periodo.Inicio, situacao);
        atendimento.Incidente = incidente;
        incidente._atendimentos.Add(atendimento);


        return incidente;
    }
    public void RegistrarIncidenteAberto()
    {
        var incidenteAberto = new IncidenteAberto(Id, Severidade, Periodo.Inicio);
        _eventos.Add(incidenteAberto);
    }

    public void Encerrar()
    {
        if (!Periodo.EstaEmAberto)
        {
            throw new DomainException("Incidente já foi encerrado");
        }

        Periodo = new Periodo(Periodo.Inicio, DateTime.UtcNow);
        _eventos.Add(new IncidenteEncerrado(Id, DateTime.UtcNow));
    }

    public void RegistrarAtendimento(DateTime dataHora, string orgaoResponsavel, string situacao)
    {
        if (!Periodo.EstaEmAberto)
        {
            throw new DomainException("Incidente encerrado não aceita novo atendimento.");
        }
        var atendimento = new Atendimento(Id, orgaoResponsavel, dataHora, situacao);
        {
            atendimento.Incidente = this;
            _atendimentos.Add(atendimento);
            _eventos.Add(new AtendimentoRegistrado(Id, orgaoResponsavel, dataHora));
        }
    }
    public void MudarBairro(int bairroId)
    {
        if (!Periodo.EstaEmAberto)
        {
            throw new DomainException("Incidente encerrado não pode ter o bairro alterado.");
        }

        BairroId = bairroId;
    }
}