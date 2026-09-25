using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mobilidade.Domain.Entity;
using Mobilidade.Domain.ValueObjects;

namespace Mobilidade.Infrastructure.Data.Configuracoes;

public class UsuarioConfiguration : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> builder)
    {
        builder.ToTable("Usuarios");
        builder.HasKey(u => u.Id);

        builder.HasIndex(u => u.Email).IsUnique();

        builder.Property(u => u.Email)
     .HasConversion(
         email => email.Valor,
         valor => Email.Criar(valor))
     .HasMaxLength(254)
     .IsRequired();

        builder.Property(u => u.Papel)
    .HasConversion(
        hash => hash.Nome,
        deNome => Papel.DeNome(deNome))
    .HasMaxLength(20)
    .IsRequired();

       builder.Property(u => u.SenhaHash)
            .HasMaxLength(255)
            .IsRequired();
    }
}