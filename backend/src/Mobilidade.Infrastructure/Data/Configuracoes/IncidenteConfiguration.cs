using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mobilidade.Domain.Entity;
using Mobilidade.Domain.ValueObjects;

namespace Mobilidade.Infrastructure.Data.Configuracoes;

public class IncidenteConfiguration : IEntityTypeConfiguration<Incidente>
{
    public void Configure(EntityTypeBuilder<Incidente> builder)
    {
        builder.HasKey(i => i.Id);

        builder.OwnsOne(i => i.Periodo, periodoBuilder =>
        {            
                periodoBuilder.Property(p => p.Inicio).IsRequired();
            periodoBuilder.Property(p => p.Fim);
            });

        builder.Property(i => i.Tipo)
            .IsRequired();

        builder.Property(i => i.Severidade)
            .HasConversion(
                severidade => severidade.Valor,
                valor => Severidade.DeInt(valor))
            .IsRequired();

        builder.Property(i => i.Descricao)
            .IsRequired(false);

        builder.HasOne(i => i.Bairro)
            .WithMany()
            .HasForeignKey(i => i.BairroId);
    }
}