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
    public async Task<Incidente> Executar(Atendimento atendimento, Incidente incidente)
    {
        using var transacao = await _contexto.Database.BeginTransactionAsync();
        try
        {
            _contexto.Incidentes.Add(incidente);
            await _contexto.SaveChangesAsync();

            _contexto.Atendimentos.Add(atendimento);
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