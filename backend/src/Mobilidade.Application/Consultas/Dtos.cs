using Mobilidade.Domain.Entity;

namespace Mobilidade.Application.Consultas;

public sealed record IncidenteResumoDto(
	int Id,
	DateTime Inicio,
	string Severidade,
	string Bairro,
	bool EstaEmAberto);

public sealed record IncidenteDetalheDto(
	int Id,
	DateTime Inicio,
	DateTime? Fim,
	string Severidade,
	string Bairro,
	TipoIncidente Tipo,
	string? Descricao,
	int QuantidadeAtendimentos);