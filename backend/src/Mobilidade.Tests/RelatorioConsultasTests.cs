using Mobilidade.Domain.Modelos;
using Mobilidade.Application.Consultas;

namespace Mobilidade.Tests;

public class RelatorioConsultasTests
{
    private static Incidente Criar(
    int id,
    DateTime dataHora,
    TipoIncidente tipo = TipoIncidente.Colisao,
    Severidade? severidade = null,
    int bairroId = 1,
    string? descricao = null)
    {
        var severidadeReal = severidade ?? Severidade.Baixa;

        var bairroNome = bairroId switch
        {
            1 => "Centro",
            2 => "Vila Nova",
            3 => "Vila Mariana",
            4 => "Jardins",
            5 => "Boa Vista",
            _ => $"Bairro {bairroId}"
        };

        var incidente = Incidente.Abrir(dataHora, severidadeReal, bairroId, descricao, "Defesa Civil", "Em analise");
        incidente.Id = id;
        incidente.Tipo = tipo;
        incidente.Bairro = new Bairro { Id = bairroId, Nome = bairroNome };

        return incidente;
    }

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
    public void Top3BairrosSeveridadeAlta_IgnoraIncidentesComOutraSeveridade()
    {
        var incidentes = new[]
        {
            Criar(1, DateTime.Today, bairroId: 1, severidade: Severidade.Alta),
            Criar(2, DateTime.Today, bairroId: 2, severidade: Severidade.Alta),
            Criar(3, DateTime.Today, bairroId: 3, severidade: Severidade.Baixa),
            Criar(4, DateTime.Today, bairroId: 4, severidade: Severidade.Alta),
        };

        var resultado = RelatorioConsultas.Top3BairrosSeveridadeAlta(incidentes);

        Assert.Equal(3, resultado.Count);
        Assert.Equal(("Centro", 1), (resultado[0].Chave, resultado[0].Quantidade));
        Assert.Equal(("Vila Nova", 1), (resultado[1].Chave, resultado[1].Quantidade));
        Assert.Equal(("Jardins", 1), (resultado[2].Chave, resultado[2].Quantidade));
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
            Criar(1, DateTime.Today, TipoIncidente.Colisao, Severidade.Alta, bairroId: 1),
            Criar(2, DateTime.Today, TipoIncidente.Colisao, Severidade.Baixa, bairroId: 1),
            Criar(3, DateTime.Today, TipoIncidente.Obra, Severidade.Media, bairroId: 1),
        };

        var resumo = Assert.Single(RelatorioConsultas.ResumoPorBairro(incidentes));

        Assert.Equal("Centro", resumo.Bairro);
        Assert.Equal(3, resumo.Total);
        Assert.Equal(1, resumo.SeveridadeAlta);
        Assert.Equal(TipoIncidente.Colisao, resumo.TipoPredominante);
    }
}