using Mobilidade.Domain.Entity;
using Mobilidade.Domain.ValueObjects;

namespace Mobilidade.Application.Repositorio;

public interface IUsuarioRepository
{
    Usuario Criar(Usuario usuario);
    Usuario? BuscarPorEmail(Email email);
}   