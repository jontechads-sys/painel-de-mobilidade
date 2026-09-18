using Mobilidade.Application.Eventos;
using Mobilidade.Domain.Eventos;

namespace Mobilidade.Tests;

public sealed class DespachadorEmMemoria : IDespachadorDeEventos
{
    public List<IEventoDeDominio> Publicados { get; } = new();

    public void Publicar(IEventoDeDominio evento) => Publicados.Add(evento);
}