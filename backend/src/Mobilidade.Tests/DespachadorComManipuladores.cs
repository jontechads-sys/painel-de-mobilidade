using Mobilidade.Application.Eventos;
using Mobilidade.Domain.Eventos;

namespace Mobilidade.Tests;

public sealed class DespachadorComManipuladores : IDespachadorDeEventos
{
    private readonly IManipuladorDeEvento<IncidenteAberto> _manipuladorAberto;

    public DespachadorComManipuladores(IManipuladorDeEvento<IncidenteAberto> manipuladorAberto)
    {
        _manipuladorAberto = manipuladorAberto;
    }

    public void Publicar(IEventoDeDominio evento)
    {
        if (evento is IncidenteAberto incidenteAberto)
            _manipuladorAberto.Manipular(incidenteAberto);
    }
}