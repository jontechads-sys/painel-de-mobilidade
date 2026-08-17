namespace Mobilidade.Domain.Modelos;

public enum TipoIncidente
{
    Colisao,
    Alagamento,
    Obra,
    BuracoNaVia,
    SemaforoApagado,
    Engarrafamento
}

public enum Severidade
{
    Baixa,
    Media,
    Alta
}
/* sealed — proíbe herança: nenhuma outra classe pode fazer class Coisa : Incidente. 
Faz sentido aqui porque Incidente é um registro de dados fechado, 
não uma hierarquia de tipos — não existe "um tipo de Incidente que se comporta diferente".
*/
public sealed record Incidente(
    int Id,
    DateTime DataHora,
    TipoIncidente Tipo,
    Severidade? Severidade,
    string Bairro,
    string? Descricao
);
