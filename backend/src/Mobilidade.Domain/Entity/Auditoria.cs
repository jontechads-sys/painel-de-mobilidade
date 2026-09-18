using Mobilidade.Domain.ValueObjects;

namespace Mobilidade.Domain.Entity;

public class Auditoria
{
	private Auditoria() { }
	public int Id { get; private set; }
	public int IncidenteId { get; private set; }
	public Severidade Severidade { get; private set; }
	public DateTime Data { get; private set; }

	private Auditoria(
		int incidenteId,
		Severidade severidade,
		DateTime data)
	{
		IncidenteId = incidenteId;
		Severidade = severidade;
		Data = data;
	}
	public static Auditoria RegistrarAuditoria(int incidenteId,Severidade severidade,DateTime data) 
	{
		var registro = new Auditoria(incidenteId, severidade, data);
		return registro;
	}
}
