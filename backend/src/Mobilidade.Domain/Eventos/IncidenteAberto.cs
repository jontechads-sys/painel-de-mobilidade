using Mobilidade.Domain.Entity;
using Mobilidade.Domain.ValueObjects;
using Mobilidade.Domain.Eventos;

namespace Mobilidade.Domain.Eventos;

public sealed record IncidenteAberto(
    int IncidenteId,
    Severidade Severidade,
    DateTime AbertoEm) : IEventoDeDominio;

public sealed record AtendimentoRegistrado(
    int IncidenteId,
    string OrgaoResponsavel,
    DateTime DataHora) : IEventoDeDominio;

public sealed record IncidenteEncerrado(
    int IncidenteId,
    DateTime DataHora) : IEventoDeDominio;
