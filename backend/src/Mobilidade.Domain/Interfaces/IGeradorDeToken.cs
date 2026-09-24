using Mobilidade.Domain.Entity;

namespace Mobilidade.Domain.Interfaces;

public interface IGeradorDeToken
{
    string Gerar(Usuario usuario);
}