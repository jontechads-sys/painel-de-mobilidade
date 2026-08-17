using System.Globalization;
using System.Text;
using Mobilidade.IncidentesConsole.Modelos;

namespace Mobilidade.IncidentesConsole.Importacao;

/// <summary>
/// Resultado da importação: os incidentes que puderam ser lidos, mais os
/// avisos sobre linhas descartadas — nunca lançamos exceção por causa de
/// uma linha ruim no meio de um CSV de 50 linhas, só registramos o motivo.
/// </summary>
public sealed record ResultadoImportacao(
    IReadOnlyList<Incidente> Incidentes,
    IReadOnlyList<string> Avisos);

public static class CsvIncidenteImportador
{
    /// <summary>
    /// Lê o CSV de forma assíncrona e em streaming: <see cref="File.ReadLinesAsync(string, CancellationToken)"/>
    /// devolve as linhas uma a uma via <c>IAsyncEnumerable&lt;string&gt;</c>,
    /// então o arquivo nunca precisa estar inteiro em memória para começar
    /// a processar — importa mesmo com um CSV bem maior que 50 linhas.
    /// </summary>
    public static async Task<ResultadoImportacao> ImportarAsync(string caminho, CancellationToken ct = default)
    {
        var incidentes = new List<Incidente>();
        var avisos = new List<string>();
        var numeroLinha = 0;

        await foreach (var linha in File.ReadLinesAsync(caminho, ct))
        {
            numeroLinha++;

            if (numeroLinha == 1 || string.IsNullOrWhiteSpace(linha))
            {
                continue; // cabecalho ou linha em branco
            }

            var campos = DividirLinhaCsv(linha);
            if (campos.Length < 6)
            {
                avisos.Add($"Linha {numeroLinha}: {campos.Length} campo(s), esperado 6 — descartada.");
                continue;
            }

            // Id, DataHora e Tipo sao obrigatorios: sem eles o registro nao
            // tem chave, eixo temporal ou classificacao minima para entrar
            // nas consultas. Decisao consciente: descartar a linha inteira
            // em vez de inventar um valor default que distorceria o relatorio.
            if (!int.TryParse(campos[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
            {
                avisos.Add($"Linha {numeroLinha}: id '{campos[0]}' invalido — descartada.");
                continue;
            }

            if (!DateTime.TryParse(campos[1].Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var dataHora))
            {
                avisos.Add($"Linha {numeroLinha}: data_hora '{campos[1]}' invalida — descartada.");
                continue;
            }

            if (!TryParseEnum<TipoIncidente>(campos[2], out var tipo))
            {
                avisos.Add($"Linha {numeroLinha}: tipo '{campos[2]}' desconhecido — descartada.");
                continue;
            }

            // Severidade pode faltar de verdade (incidente ainda nao
            // triado). Decisao consciente: manter o registro com
            // Severidade nula, em vez de descartar a linha ou chutar um
            // valor — consultas que dependem de severidade (ex.: "top
            // bairros com severidade alta") simplesmente nao contam essas
            // linhas, sem tratamento especial nenhum.
            Severidade? severidade = TryParseEnum<Severidade>(campos[3], out var severidadeValor)
                ? severidadeValor
                : null;

            // Bairro ausente vira um marcador explicito, nao null: e um
            // problema de qualidade do dado, mas o registro continua util
            // (ex.: ainda conta no total por tipo) e o marcador aparece
            // como categoria propria nos agrupamentos por bairro, em vez
            // de exigir tratamento de chave nula em todo GroupBy.
            var bairro = string.IsNullOrWhiteSpace(campos[4]) ? "(nao informado)" : campos[4].Trim();

            // Descricao e texto livre genuinamente opcional -- null
            // sinaliza "ninguem preencheu", diferente de string vazia.
            var descricao = string.IsNullOrWhiteSpace(campos[5]) ? null : campos[5].Trim();

            incidentes.Add(new Incidente(id, dataHora, tipo, severidade, bairro, descricao));
        }

        return new ResultadoImportacao(incidentes, avisos);
    }

    /// <summary>
    /// Parser genérico reutilizável: tenta converter um campo textual para
    /// qualquer enum do domínio (<see cref="TipoIncidente"/>,
    /// <see cref="Severidade"/>, ...) sem repetir a mesma lógica de
    /// trim + TryParse para cada um. É chamado duas vezes acima — o motivo
    /// de ser genérico, não só uma conveniência.
    /// </summary>
    private static bool TryParseEnum<TEnum>(string valor, out TEnum resultado) where TEnum : struct, Enum
        => Enum.TryParse(valor.Trim(), ignoreCase: true, out resultado);

    /// <summary>
    /// Split de linha CSV respeitando campos entre aspas (RFC 4180
    /// simplificado): trata vírgula dentro de aspas e aspas escapadas
    /// (<c>""</c>) para permitir descrições com vírgula sem quebrar as colunas.
    /// </summary>
    private static string[] DividirLinhaCsv(string linha)
    {
        var campos = new List<string>();
        var atual = new StringBuilder();
        var dentroDeAspas = false;

        for (var i = 0; i < linha.Length; i++)
        {
            var c = linha[i];
            if (c == '"')
            {
                if (dentroDeAspas && i + 1 < linha.Length && linha[i + 1] == '"')
                {
                    atual.Append('"');
                    i++;
                }
                else
                {
                    dentroDeAspas = !dentroDeAspas;
                }
            }
            else if (c == ',' && !dentroDeAspas)
            {
                campos.Add(atual.ToString());
                atual.Clear();
            }
            else
            {
                atual.Append(c);
            }
        }

        campos.Add(atual.ToString());
        return campos.ToArray();
    }
}
