using Mobilidade.Application.Eventos;
using Mobilidade.Application.Repositorio;
using Mobilidade.Domain.Entity;
using Mobilidade.Domain.ValueObjects;

namespace Mobilidade.Application.CasosDeUso;

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

    public int Executar(
        int id,
        DateTime dataHora,
        string orgaoReponsavel,
        string situacao)
    {
        var incidente = _repositorio.BuscarPorId(id);
        if (incidente == null)
        {
            throw new KeyNotFoundException("Id inexistente no banco");
        }

        incidente.RegistrarAtendimento(dataHora, orgaoReponsavel, situacao);
        _repositorio.Salvar(incidente);

        foreach (var evento in incidente.Eventos)
            _despachador.Publicar(evento);

        incidente.LimparEventos();

        return incidente.Id;
    }
}