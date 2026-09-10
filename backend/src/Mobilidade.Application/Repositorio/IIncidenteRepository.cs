using Mobilidade.Domain.Modelos;

namespace Mobilidade.Application.Repositorio
{

    public interface IIncidenteRepository
    {
        Incidente Criar(Incidente incidente);
        IReadOnlyList<Incidente> Listar();
        Incidente? BuscarPorId(int id);
        Incidente? Atualizar(int id, Incidente incidente);
        Incidente Salvar(Incidente incidente);
        bool Remover(int id);
    }
}