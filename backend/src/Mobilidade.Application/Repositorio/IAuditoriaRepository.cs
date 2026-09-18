using Mobilidade.Domain.Entity;

namespace Mobilidade.Application.Repositorio
{
	public interface IAuditoriaRepository
	{
		Auditoria Criar(Auditoria auditoria);
	}
}