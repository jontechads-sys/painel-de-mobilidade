namespace Mobilidade.Application.Eventos;

using Mobilidade.Domain.Eventos;

public interface IDespachadorDeEventos
{
    void Publicar(IEventoDeDominio evento);
}