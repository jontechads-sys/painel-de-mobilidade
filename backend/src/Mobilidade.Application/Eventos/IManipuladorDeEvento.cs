namespace Mobilidade.Application.Eventos;

using Mobilidade.Domain.Eventos;

public interface IManipuladorDeEvento<TEvento> where TEvento : IEventoDeDominio
{
	void Manipular(TEvento evento);
}