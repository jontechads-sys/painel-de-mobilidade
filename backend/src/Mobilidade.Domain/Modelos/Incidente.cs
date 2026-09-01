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

public sealed class Incidente
{
    public int Id { get; set; }
    public DateTime DataHora { get; set; }
    public TipoIncidente Tipo { get; set; }
    public Severidade? Severidade { get; set; }
    public int BairroId { get; set; }
    public string? Descricao { get; set; }
    
    public Bairro Bairro { get; set; }
}