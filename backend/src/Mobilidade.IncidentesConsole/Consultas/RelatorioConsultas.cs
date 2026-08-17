using Mobilidade.IncidentesConsole.Modelos;

namespace Mobilidade.IncidentesConsole.Consultas;

/// <summary>
/// Contagem agrupada por uma chave qualquer.
/// </summary>
public sealed record Contagem<TChave>(TChave Chave, int Quantidade) where TChave : notnull;

/// <summary>
/// Projeção de GroupBy: resumo por bairro combinando total, quantidade de
/// severidade alta e o tipo de incidente predominante ali.
/// </summary>
public sealed record ResumoBairro(string Bairro, int Total, int SeveridadeAlta, TipoIncidente TipoPredominante);

public static class RelatorioConsultas
{
    /// <summary>
    /// Agrupador genérico reutilizável: conta incidentes por qualquer
    /// chave (tipo, bairro, ...) e devolve já ordenado do maior pro menor.
    /// Evita repetir GroupBy + Select + OrderByDescending em toda consulta
    /// que só precisa de "quantidade por categoria" — é usado por
    /// <see cref="TotalPorTipo"/> e <see cref="Top3BairrosSeveridadeAlta"/>.
    /// </summary>
    public static IReadOnlyList<Contagem<TChave>> ContarPor<TChave>(
        IEnumerable<Incidente> incidentes,
        Func<Incidente, TChave> seletorDeChave)
        where TChave : notnull
        => incidentes
            .GroupBy(seletorDeChave)
            .Select(g => new Contagem<TChave>(g.Key, g.Count()))
            .OrderByDescending(c => c.Quantidade)
            .ToList();

    /// <summary>Total de incidentes por tipo.</summary>
    public static IReadOnlyList<Contagem<TipoIncidente>> TotalPorTipo(IEnumerable<Incidente> incidentes)
        => ContarPor(incidentes, i => i.Tipo);

    /// <summary>
    /// Top 3 bairros com mais incidentes de severidade alta. Incidentes
    /// sem severidade classificada (null) ficam de fora naturalmente — a
    /// comparação <c>== Severidade.Alta</c> com um <c>Severidade?</c> nulo
    /// já dá <c>false</c>, sem precisar de tratamento especial.
    /// </summary>
    public static IReadOnlyList<Contagem<string>> Top3BairrosSeveridadeAlta(IEnumerable<Incidente> incidentes)
        => ContarPor(incidentes.Where(i => i.Severidade == Severidade.Alta), i => i.Bairro)
            .Take(3)
            .ToList();

    /// <summary>
    /// Média de incidentes por dia da semana. Agrupa primeiro por dia
    /// calendário (quantos incidentes cada dia teve), depois tira a média
    /// desses totais agrupando por <see cref="DayOfWeek"/> — assim "média
    /// das segundas" reflete quantas segundas existem na base e quanto
    /// cada uma teve, não simplesmente total-de-segundas / 7.
    /// Ordenado começando na segunda (rotaciona o enum, que começa no
    /// domingo = 0).
    ///
    /// Nuance consciente: só entram no cálculo dias que aparecem na base
    /// (GroupBy não materializa grupo vazio). Um sábado sem nenhum
    /// incidente não conta como "0" puxando a média de sábado para baixo —
    /// ele simplesmente não existe como dia de referência. A métrica é
    /// "média de incidentes nos dias em que houve incidente", não "média
    /// por dia do calendário".
    /// </summary>
    public static IReadOnlyList<(DayOfWeek DiaDaSemana, double Media)> MediaPorDiaDaSemana(
        IEnumerable<Incidente> incidentes)
        => incidentes
            .GroupBy(i => i.DataHora.Date)
            .Select(g => new { g.Key.DayOfWeek, Quantidade = g.Count() })
            .GroupBy(x => x.DayOfWeek)
            .Select(g => (DiaDaSemana: g.Key, Media: g.Average(x => x.Quantidade)))
            .OrderBy(x => ((int)x.DiaDaSemana + 6) % 7) // segunda primeiro, domingo por ultimo
            .ToList();

    /// <summary>
    /// Consulta livre: GroupBy + projeção em record. Para cada bairro,
    /// total de incidentes, quantos são severidade alta e qual tipo é o
    /// mais comum ali — sinaliza onde priorizar manutenção/fiscalização.
    /// </summary>
    public static IReadOnlyList<ResumoBairro> ResumoPorBairro(IEnumerable<Incidente> incidentes)
        => incidentes
            .GroupBy(i => i.Bairro)
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
