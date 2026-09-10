namespace Mobilidade.Api.Dtos;

public sealed record RegistrarAtendimentoRequest(
    DateTime DataHora,
    string OrgaoResponsavel,
    string Situacao
);