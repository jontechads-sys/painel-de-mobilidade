using Mobilidade.IncidentesConsole.Importacao;
using Mobilidade.IncidentesConsole.Modelos;

namespace Mobilidade.Tests;

public class CsvIncidenteImportadorTests
{
    private static async Task<ResultadoImportacao> ImportarConteudoAsync(string conteudoCsv)
    {
        var caminho = Path.Combine(Path.GetTempPath(), $"incidentes-teste-{Guid.NewGuid():N}.csv");
        await File.WriteAllTextAsync(caminho, conteudoCsv);
        try
        {
            return await CsvIncidenteImportador.ImportarAsync(caminho);
        }
        finally
        {
            File.Delete(caminho);
        }
    }

    [Fact]
    public async Task ImportarAsync_DescartaLinhaComIdInvalido_EGeraAviso()
    {
        var csv = "id,data_hora,tipo,severidade,bairro,descricao\n" +
                  "abc,2026-07-13T07:20:00,Colisao,Alta,Centro,teste\n" +
                  "1,2026-07-13T07:20:00,Colisao,Alta,Centro,teste";

        var resultado = await ImportarConteudoAsync(csv);

        Assert.Single(resultado.Incidentes);
        Assert.Single(resultado.Avisos);
        Assert.Contains("id", resultado.Avisos[0]);
    }

    [Fact]
    public async Task ImportarAsync_SeveridadeAusente_MantemRegistroComSeveridadeNula()
    {
        var csv = "id,data_hora,tipo,severidade,bairro,descricao\n" +
                  "1,2026-07-13T07:20:00,BuracoNaVia,,Centro,sem triagem ainda";

        var resultado = await ImportarConteudoAsync(csv);

        var incidente = Assert.Single(resultado.Incidentes);
        Assert.Null(incidente.Severidade);
        Assert.Empty(resultado.Avisos);
    }

    [Fact]
    public async Task ImportarAsync_BairroAusente_VaiParaMarcadorNaoInformado()
    {
        var csv = "id,data_hora,tipo,severidade,bairro,descricao\n" +
                  "1,2026-07-13T07:20:00,Obra,Baixa,,sem bairro no csv";

        var resultado = await ImportarConteudoAsync(csv);

        var incidente = Assert.Single(resultado.Incidentes);
        Assert.Equal("(nao informado)", incidente.Bairro);
    }

    [Fact]
    public async Task ImportarAsync_DescricaoAusente_VaiParaNull()
    {
        var csv = "id,data_hora,tipo,severidade,bairro,descricao\n" +
                  "1,2026-07-13T07:20:00,Obra,Baixa,Centro,";

        var resultado = await ImportarConteudoAsync(csv);

        var incidente = Assert.Single(resultado.Incidentes);
        Assert.Null(incidente.Descricao);
    }

    [Fact]
    public async Task ImportarAsync_RespeitaCampoEntreAspasComVirgula()
    {
        var csv = "id,data_hora,tipo,severidade,bairro,descricao\n" +
                  "1,2026-07-13T07:20:00,Obra,Baixa,Centro,\"Via interditada, com desvio sinalizado\"";

        var resultado = await ImportarConteudoAsync(csv);

        var incidente = Assert.Single(resultado.Incidentes);
        Assert.Equal("Via interditada, com desvio sinalizado", incidente.Descricao);
    }

    [Fact]
    public async Task ImportarAsync_DataInvalida_DescartaLinhaEGeraAviso()
    {
        var csv = "id,data_hora,tipo,severidade,bairro,descricao\n" +
                  "1,31/13/2026,Obra,Baixa,Centro,data quebrada";

        var resultado = await ImportarConteudoAsync(csv);

        Assert.Empty(resultado.Incidentes);
        var aviso = Assert.Single(resultado.Avisos);
        Assert.Contains("data_hora", aviso);
    }

    [Fact]
    public async Task ImportarAsync_TipoDesconhecido_DescartaLinhaEGeraAviso()
    {
        var csv = "id,data_hora,tipo,severidade,bairro,descricao\n" +
                  "1,2026-07-13T07:20:00,Terremoto,Alta,Centro,tipo que nao existe no enum";

        var resultado = await ImportarConteudoAsync(csv);

        Assert.Empty(resultado.Incidentes);
        var aviso = Assert.Single(resultado.Avisos);
        Assert.Contains("tipo", aviso);
    }
}
