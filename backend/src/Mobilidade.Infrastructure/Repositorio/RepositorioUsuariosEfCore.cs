using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Mobilidade.Application.Repositorio;
using Mobilidade.Domain.Entity;
using Mobilidade.Domain.Excecoes;
using Mobilidade.Domain.ValueObjects;
using Mobilidade.Infrastructure.Data;

namespace Mobilidade.Infrastructure.Repositorio;

public class RepositorioUsuariosEfCore : IUsuarioRepository
{
    // 2627 = violacao de constraint unique; 2601 = chave duplicada em indice unique.
    private const int ViolacaoDeConstraintUnique = 2627;
    private const int ChaveDuplicadaEmIndiceUnique = 2601;

    private readonly IncidentesDbContext _contexto;

    public RepositorioUsuariosEfCore(IncidentesDbContext contexto)
    {
        _contexto = contexto;
    }

    public Usuario Criar(Usuario usuario)
    {
        _contexto.Usuarios.Add(usuario);

        try
        {
            _contexto.SaveChanges();
        }
        catch (DbUpdateException ex) when (EhEmailDuplicado(ex))
        {
            // A entidade fica no ChangeTracker como Added depois da falha; sem
            // desanexar, o proximo SaveChanges deste contexto tentaria gravar de novo.
            _contexto.Entry(usuario).State = EntityState.Detached;
            throw new DomainException("Já existe um usuário com este email.");
        }

        return usuario;
    }

    public Usuario? BuscarPorEmail(Email email)
    {
        return _contexto.Usuarios.FirstOrDefault(u => u.Email == email);
    }

    private static bool EhEmailDuplicado(DbUpdateException ex)
        => ex.InnerException is SqlException sql
           && (sql.Number == ViolacaoDeConstraintUnique || sql.Number == ChaveDuplicadaEmIndiceUnique)
           && sql.Message.Contains("IX_Usuarios_Email", StringComparison.OrdinalIgnoreCase);
}