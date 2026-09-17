using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mobilidade.Domain.Entity;
using Mobilidade.Domain.ValueObjects;


namespace Mobilidade.Infrastructure.Data;

public class AuditoriaConfiguration : IEntityTypeConfiguration<Auditoria>
{
    public void Configure(EntityTypeBuilder<Auditoria> builder)
    {
        builder.ToTable("Auditoria");
        builder.HasKey(aud => aud.Id);

        builder.Property(aud => aud.Severidade)
           .HasConversion(
               severidade => severidade.Valor,
               valor => Severidade.DeInt(valor))
           .IsRequired();

        builder.Property(aud => aud.Data)
            .IsRequired(); 

        builder.HasOne<Incidente>()
            .WithMany()
            .HasForeignKey(aud => aud.IncidenteId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}