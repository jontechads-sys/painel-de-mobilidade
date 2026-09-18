using Microsoft.EntityFrameworkCore;
using Mobilidade.Application.Consultas;
using Mobilidade.Infrastructure.Data;

namespace Mobilidade.Infrastructure.Consultas;

public sealed record ListarIncidentesQuery();

public sealed class ListarIncidentesHandler
{
    private readonly IncidentesDbContext _contexto;

    public ListarIncidentesHandler(IncidentesDbContext contexto)
    {
        _contexto = contexto;
    }

    public List<IncidenteResumoDto> Handle(ListarIncidentesQuery query)
        => _contexto.Incidentes
            .AsNoTracking()
            .Select(i => new IncidenteResumoDto(
                i.Id,
                i.Periodo.Inicio,
                i.Severidade.Nome,
                i.Bairro!.Nome,
                i.Periodo.Fim == null))
            .ToList();
}