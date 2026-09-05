using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mobilidade.Domain.Modelos;

namespace Mobilidade.Infrastructure.Data.Configuracoes;

public class IncidenteConfiguration : IEntityTypeConfiguration<Incidente>
{
    public void Configure(EntityTypeBuilder<Incidente> builder)
    {
        builder.HasKey(i => i.Id);

        builder.Property(i => i.DataHora)
            .IsRequired();

        builder.Property(i => i.Tipo)
            .IsRequired();

        builder.Property(i => i.Descricao)
            .IsRequired(false);

        builder.HasOne(i => i.Bairro)
            .WithMany()
            .HasForeignKey(i => i.BairroId);
    }
}