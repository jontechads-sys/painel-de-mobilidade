using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mobilidade.Domain.Modelos;

namespace Mobilidade.Infrastructure.Data.Configuracoes;

public class BairroConfiguration : IEntityTypeConfiguration<Bairro>
{
    public void Configure(EntityTypeBuilder<Bairro> builder)
    {
        builder.HasKey(b => b.Id);

        builder.Property(b => b.Nome)
            .IsRequired()
            .HasMaxLength(100);
    }
}