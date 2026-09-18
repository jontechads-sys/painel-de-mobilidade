using Mobilidade.Application.Eventos;
using Mobilidade.Application.Repositorio;
using Mobilidade.Domain.Entity;
using Mobilidade.Domain.ValueObjects;

namespace Mobilidade.Application.CasosDeUso;

public class EncerrarIncidente
{
    private readonly IIncidenteRepository _repositorio;
    private readonly IDespachadorDeEventos _despachador;

    public EncerrarIncidente(
        IIncidenteRepository repositorio,
        IDespachadorDeEventos despachador)
    {
        _repositorio = repositorio;
        _despachador = despachador;
    }

    public int Executar(int id)
    {
        var incidente = _repositorio.BuscarPorId(id);
        if (incidente == null)
        {
            throw new KeyNotFoundException("Id inexistente no banco");
        }

        incidente.Encerrar();
        _repositorio.Salvar(incidente);

        foreach (var evento in incidente.Eventos)
            _despachador.Publicar(evento);

        incidente.LimparEventos();

        return incidente.Id;
    }
}