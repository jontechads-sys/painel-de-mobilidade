namespace Mobilidade.Domain.Modelos;

public sealed class Atendimento
{
    public int Id { get; set; }
    public int IncidenteId { get; set; }
    public string OrgaoResponsavel { get; set; }
    public DateTime DataHora { get; set; }
    public string Situacao { get; set; }

    public Incidente Incidente {get; set;}
}