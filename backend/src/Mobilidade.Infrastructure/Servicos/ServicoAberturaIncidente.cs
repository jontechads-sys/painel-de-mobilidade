using Mobilidade.Domain.Modelos;
using Mobilidade.Infrastructure.Data;

namespace Mobilidade.Infrastructure.Servicos;

public class ServicoAberturaIncidente
{
    private readonly IncidentesDbContext _contexto;

    public ServicoAberturaIncidente(IncidentesDbContext contexto)
    {
        _contexto = contexto;
    }
    public async Task<Incidente> Executar(
    DateTime dataHora,
    Severidade severidade,
    string orgaoResponsavel,
    string situacao)
    {
        var incidente = Incidente.Abrir(dataHora, severidade, orgaoResponsavel, situacao);

        using var transacao = await _contexto.Database.BeginTransactionAsync();
        try
        {
            _contexto.Incidentes.Add(incidente);
            await _contexto.SaveChangesAsync();

            await transacao.CommitAsync();
            return incidente;
        }
        catch
        {
            await transacao.RollbackAsync();
            throw;
        }
    }
}