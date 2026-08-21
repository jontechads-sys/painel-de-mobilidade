using Mobilidade.Application.Importacao;
using Mobilidade.Application.Repositorio;
using Mobilidade.Infrastructure.Importacao;
using Mobilidade.Infrastructure.Repositorio;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddControllers();
builder.Services.AddProblemDetails();

builder.Services.AddSingleton<IIncidenteRepository, RepositorioIncidentesEmMemoria>();
builder.Services.AddTransient<IIncidenteImportador, ImportadorIncidentesCsv>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseExceptionHandler();
app.UseHttpsRedirection();
app.MapControllers();

app.MapPost("/api/incidentes/importar", async (
    IIncidenteImportador importador,
    IIncidenteRepository repositorio,
    CancellationToken ct) =>
{
    var resultado = await importador.ImportarAsync(ct);

    foreach (var incidente in resultado.Incidentes)
    {
        repositorio.Criar(incidente);
    }

    return Results.Ok(new { importados = resultado.Incidentes.Count, avisos = resultado.Avisos });
})
.WithName("ImportarIncidentesCsv");

app.Run();