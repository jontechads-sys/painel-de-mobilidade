using Mobilidade.Application.Eventos;
using Mobilidade.Application.Repositorio;
using Mobilidade.Domain.Entity;
using Mobilidade.Domain.ValueObjects;

namespace Mobilidade.Application.CasosDeUso;

public sealed record RegistrarAtendimentoCommand(
    int IncidenteId,
    DateTime DataHora,
    string OrgaoResponsavel,
    string Situacao);

public class RegistrarAtendimento
{
    private readonly IIncidenteRepository _repositorio;
    private readonly IDespachadorDeEventos _despachador;

    public RegistrarAtendimento(
        IIncidenteRepository repositorio,
        IDespachadorDeEventos despachador)
    {
        _repositorio = repositorio;
        _despachador = despachador;
    }

    public int Executar(RegistrarAtendimentoCommand comando)
    {
        var incidente = _repositorio.BuscarPorId(comando.IncidenteId);
        if (incidente == null)
        {
            throw new KeyNotFoundException("Id inexistente no banco");
        }

        incidente.RegistrarAtendimento(comando.DataHora, comando.OrgaoResponsavel, comando.Situacao);
        _repositorio.Salvar(incidente);

        foreach (var evento in incidente.Eventos)
            _despachador.Publicar(evento);

        incidente.LimparEventos();

        return incidente.Id;
    }
}