using Mobilidade.Domain.Entity;
using Mobilidade.Domain.ValueObjects;

namespace Mobilidade.Domain.Eventos;

public sealed record IncidenteAberto(
    int IncidenteId, 
    Severidade Severidade, 
    DateTime AbertoEm);

public sealed record AtendimentoRegistrado(
    int IncidenteId,
    string OrgaoResponsavel,
    DateTime DataHora);

public sealed record IncidenteEncerrado(
    int IncidenteId,
    DateTime DataHora);
