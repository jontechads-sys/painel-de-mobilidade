namespace Mobilidade.Application.Eventos.Manipuladores;

using Mobilidade.Application.Eventos;
using Mobilidade.Application.Repositorio;
using Mobilidade.Domain.Entity;
using Mobilidade.Domain.Eventos;
using Mobilidade.Domain.ValueObjects;

public sealed class RegistrarAuditoriaAoAbrirIncidenteGrave
    : IManipuladorDeEvento<IncidenteAberto>
{
    private readonly IAuditoriaRepository _auditoria;

    public RegistrarAuditoriaAoAbrirIncidenteGrave(IAuditoriaRepository auditoria)
    {
        _auditoria = auditoria;
    }

    public void Manipular(IncidenteAberto evento)
    {
        if (evento.Severidade != Severidade.Alta)
            return;

        var registro = Auditoria.RegistrarAuditoria(
            evento.IncidenteId,
            evento.Severidade,
            evento.AbertoEm);

        _auditoria.Criar(registro);
    }
}