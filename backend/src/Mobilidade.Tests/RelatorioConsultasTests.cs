using Mobilidade.IncidentesConsole.Consultas;
using Mobilidade.IncidentesConsole.Modelos;

namespace Mobilidade.Tests;

public class RelatorioConsultasTests
{
    private static Incidente Criar(
        int id,
        DateTime dataHora,
        TipoIncidente tipo = TipoIncidente.Colisao,
        Severidade? severidade = null,
        string bairro = "Centro",
        string? descricao = null)
        => new(id, dataHora, tipo, severidade, bairro, descricao);

    [Fact]
    public void ContarPor_AgrupaEOrdenaPorQuantidadeDecrescente()
    {
        var incidentes = new[]
        {
            Criar(1, DateTime.Today, TipoIncidente.Colisao),
            Criar(2, DateTime.Today, TipoIncidente.Colisao),
            Criar(3, DateTime.Today, TipoIncidente.Obra),
        };

        var resultado = RelatorioConsultas.ContarPor(incidentes, i => i.Tipo);

        Assert.Equal(2, resultado.Count);
        Assert.Equal(TipoIncidente.Colisao, resultado[0].Chave);
        Assert.Equal(2, resultado[0].Quantidade);
        Assert.Equal(TipoIncidente.Obra, resultado[1].Chave);
        Assert.Equal(1, resultado[1].Quantidade);
    }

    [Fact]
    public void Top3BairrosSeveridadeAlta_IgnoraIncidentesSemSeveridadeOuComOutraSeveridade()
    {
        var incidentes = new[]
        {
            Criar(1, DateTime.Today, bairro: "Centro", severidade: Severidade.Alta),
            Criar(2, DateTime.Today, bairro: "Centro", severidade: Severidade.Alta),
            Criar(3, DateTime.Today, bairro: "Centro", severidade: Severidade.Baixa),
            Criar(4, DateTime.Today, bairro: "Vila Nova", severidade: Severidade.Alta),
            Criar(5, DateTime.Today, bairro: "Boa Vista", severidade: null),
        };

        var resultado = RelatorioConsultas.Top3BairrosSeveridadeAlta(incidentes);

        Assert.Equal(2, resultado.Count);
        Assert.Equal(("Centro", 2), (resultado[0].Chave, resultado[0].Quantidade));
        Assert.Equal(("Vila Nova", 1), (resultado[1].Chave, resultado[1].Quantidade));
    }

    [Fact]
    public void MediaPorDiaDaSemana_CalculaMediaSobreDiasComIncidente()
    {
        // Duas segundas com 2 e 4 incidentes -> media 3. Uma terca com 1 incidente -> media 1.
        var segunda1 = new DateTime(2026, 7, 13); // segunda-feira de verdade
        var segunda2 = new DateTime(2026, 7, 20);
        var terca1 = new DateTime(2026, 7, 14);

        var incidentes = new[]
        {
            Criar(1, segunda1.AddHours(8)),
            Criar(2, segunda1.AddHours(9)),
            Criar(3, segunda2.AddHours(8)),
            Criar(4, segunda2.AddHours(9)),
            Criar(5, segunda2.AddHours(10)),
            Criar(6, segunda2.AddHours(11)),
            Criar(7, terca1.AddHours(8)),
        };

        var resultado = RelatorioConsultas.MediaPorDiaDaSemana(incidentes);

        var segunda = resultado.Single(x => x.DiaDaSemana == DayOfWeek.Monday);
        var terca = resultado.Single(x => x.DiaDaSemana == DayOfWeek.Tuesday);

        Assert.Equal(3.0, segunda.Media);
        Assert.Equal(1.0, terca.Media);
    }

    [Fact]
    public void ResumoPorBairro_ProjetaTotalAltaETipoPredominante()
    {
        var incidentes = new[]
        {
            Criar(1, DateTime.Today, TipoIncidente.Colisao, Severidade.Alta, "Centro"),
            Criar(2, DateTime.Today, TipoIncidente.Colisao, Severidade.Baixa, "Centro"),
            Criar(3, DateTime.Today, TipoIncidente.Obra, Severidade.Media, "Centro"),
        };

        var resumo = Assert.Single(RelatorioConsultas.ResumoPorBairro(incidentes));

        Assert.Equal("Centro", resumo.Bairro);
        Assert.Equal(3, resumo.Total);
        Assert.Equal(1, resumo.SeveridadeAlta);
        Assert.Equal(TipoIncidente.Colisao, resumo.TipoPredominante);
    }
}
