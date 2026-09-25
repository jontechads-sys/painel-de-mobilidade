namespace Mobilidade.Domain.Interfaces;

public interface IHasherDeSenha
{
    string Gerar(string senhaEmTexto);
    bool Verificar(string senhaEmTexto, string senhaHash);
}