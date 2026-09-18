using Mobilidade.Domain.Entity;
using Mobilidade.Domain.ValueObjects;

namespace Mobilidade.Application.Consultas;

public sealed record Contagem<TChave>(TChave Chave, int Quantidade) where TChave : notnull;

public sealed record ResumoBairro(string Bairro, int Total, int SeveridadeAlta, TipoIncidente TipoPredominante);

public static class RelatorioConsultas
{
    public static IReadOnlyList<Contagem<TChave>> ContarPor<TChave>(
        IEnumerable<Incidente> incidentes,
        Func<Incidente, TChave> seletorDeChave)
        where TChave : notnull
        => incidentes
            .GroupBy(seletorDeChave)
            .Select(g => new Contagem<TChave>(g.Key, g.Count()))
            .OrderByDescending(c => c.Quantidade)
            .ToList();

    public static IReadOnlyList<Contagem<TipoIncidente>> TotalPorTipo(IEnumerable<Incidente> incidentes)
        => ContarPor(incidentes, i => i.Tipo);

    public static IReadOnlyList<Contagem<string>> Top3BairrosSeveridadeAlta(IEnumerable<Incidente> incidentes)
        => ContarPor(incidentes.Where(i => i.Severidade == Severidade.Alta), i => i.Bairro.Nome)
            .Take(3)
            .ToList();

    public static IReadOnlyList<(DayOfWeek DiaDaSemana, double Media)> MediaPorDiaDaSemana(
        IEnumerable<Incidente> incidentes)
        => incidentes
        .GroupBy(i => i.Periodo.Inicio.Date)
            .Select(g => new { g.Key.DayOfWeek, Quantidade = g.Count() })
            .GroupBy(x => x.DayOfWeek)
            .Select(g => (DiaDaSemana: g.Key, Media: g.Average(x => x.Quantidade)))
            .OrderBy(x => ((int)x.DiaDaSemana + 6) % 7)
            .ToList();

    public static IReadOnlyList<ResumoBairro> ResumoPorBairro(IEnumerable<Incidente> incidentes)
        => incidentes
            .GroupBy(i => i.Bairro.Nome)
            .Select(g => new ResumoBairro(
                Bairro: g.Key,
                Total: g.Count(),
                SeveridadeAlta: g.Count(i => i.Severidade == Severidade.Alta),
                TipoPredominante: g
                    .GroupBy(i => i.Tipo)
                    .OrderByDescending(tg => tg.Count())
                    .First().Key))
            .OrderByDescending(r => r.Total)
            .ToList();
}