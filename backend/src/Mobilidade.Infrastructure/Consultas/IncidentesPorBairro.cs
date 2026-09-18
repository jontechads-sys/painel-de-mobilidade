using Microsoft.EntityFrameworkCore;
using Mobilidade.Application.Consultas;
using Mobilidade.Domain.ValueObjects;
using Mobilidade.Infrastructure.Data;

namespace Mobilidade.Infrastructure.Consultas;

public sealed record IncidentesPorBairroQuery(int BairroId, Periodo Intervalo);

public sealed class IncidentesPorBairroHandler
{
    private readonly IncidentesDbContext _contexto;

    public IncidentesPorBairroHandler(IncidentesDbContext contexto)
    {
        _contexto = contexto;
    }

    public List<IncidenteResumoDto> Handle(IncidentesPorBairroQuery query)
    {
        var inicio = query.Intervalo.Inicio;
        var fim = query.Intervalo.Fim ?? DateTime.UtcNow;

        return _contexto.Incidentes
            .AsNoTracking()
            .Where(i => i.BairroId == query.BairroId
                     && i.Periodo.Inicio >= inicio
                     && i.Periodo.Inicio <= fim)
            .Select(i => new IncidenteResumoDto(
                i.Id,
                i.Periodo.Inicio,
                i.Severidade.Nome,
                i.Bairro!.Nome,
                i.Periodo.Fim == null))
            .ToList();
    }
}