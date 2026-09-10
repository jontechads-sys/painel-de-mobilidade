namespace Mobilidade.Domain.Modelos;

public sealed class Atendimento
{
    public int Id { get; set; }
    public int IncidenteId { get; private set; }
    public string OrgaoResponsavel { get; private set; }
    public DateTime DataHora { get; private set; }
    public string Situacao { get; private set; }
    public Incidente Incidente {get; internal set;}

    public Atendimento(int incidenteId, string  orgaoResponsavel, DateTime dataHora, string situacao)
    {
        IncidenteId = incidenteId;
        OrgaoResponsavel = orgaoResponsavel;
        DataHora = dataHora;
        Situacao = situacao;
    }
}