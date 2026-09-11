using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mobilidade.Domain.Entity;
using Mobilidade.Domain.ValueObjects;


namespace Mobilidade.Infrastructure.Data;

public class AtendimentoConfiguration : IEntityTypeConfiguration<Atendimento>
{
    public void Configure(EntityTypeBuilder<Atendimento> builder)
    {
        builder.ToTable("Atendimento");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.OrgaoResponsavel)
        .IsRequired()
        .HasMaxLength(100);

        builder.Property(a => a.DataHora)
        .IsRequired();

        builder.Property(a => a.Situacao)
        .IsRequired()
        .HasMaxLength(100);

        builder.HasOne(a => a.Incidente)
        .WithMany(i => i.Atendimentos)
        .HasForeignKey(a => a.IncidenteId)
        .OnDelete(DeleteBehavior.Cascade);
    }
}