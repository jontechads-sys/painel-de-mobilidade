namespace Mobilidade.Application.Eventos;

using Microsoft.Extensions.DependencyInjection;
using Mobilidade.Domain.Eventos;

public sealed class Despachador : IDespachadorDeEventos
{
    private readonly IServiceProvider _provedor;

    public Despachador(IServiceProvider provedor)
    {
        _provedor = provedor;
    }

    public void Publicar(IEventoDeDominio evento)
    {
        switch (evento)
        {
            case IncidenteAberto e:
                Entregar(e);
                break;

            case AtendimentoRegistrado e:
                Entregar(e);
                break;

            case IncidenteEncerrado e:
                Entregar(e);
                break;

            default:
                throw new InvalidOperationException(
                    $"Evento sem despacho configurado: {evento.GetType().Name}");
        }
    }

    private void Entregar<TEvento>(TEvento evento) where TEvento : IEventoDeDominio
    {
        foreach (var manipulador in _provedor.GetServices<IManipuladorDeEvento<TEvento>>())
            manipulador.Manipular(evento);
    }
}