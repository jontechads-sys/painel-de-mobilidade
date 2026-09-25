using Microsoft.EntityFrameworkCore;
using Mobilidade.Domain.Entity;
using Mobilidade.Domain.ValueObjects;
using Mobilidade.Infrastructure.Data.Configuracoes;


namespace Mobilidade.Infrastructure.Data;

public class IncidentesDbContext : DbContext
{
    public IncidentesDbContext(DbContextOptions<IncidentesDbContext> options) : base(options)
    {
    }
    public DbSet<Incidente> Incidentes { get; set; }
    public DbSet<Bairro> Bairros { get; set; }
    public DbSet<Atendimento> Atendimentos {get; set;}
    public DbSet<Auditoria> Auditorias { get; set; }
    public DbSet<Usuario> Usuarios { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new IncidenteConfiguration());
        modelBuilder.ApplyConfiguration(new BairroConfiguration());
        modelBuilder.ApplyConfiguration(new AtendimentoConfiguration());
        modelBuilder.ApplyConfiguration(new AuditoriaConfiguration());
        modelBuilder.ApplyConfiguration(new UsuarioConfiguration());
    }
}
