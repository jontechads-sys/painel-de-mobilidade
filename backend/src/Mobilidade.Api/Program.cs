using Mobilidade.Application.Importacao;
using Mobilidade.Application.Repositorio;
using Mobilidade.Infrastructure.Importacao;
using Mobilidade.Infrastructure.Repositorio;
using Microsoft.Data.SqlClient;
using Mobilidade.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Mobilidade.Application.CasosDeUso;
using Mobilidade.Application.Eventos;
using Mobilidade.Application.Eventos.Manipuladores;
using Mobilidade.Domain.Eventos;
using Mobilidade.Api.Filtros;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers(options =>
{
    options.Filters.Add<DomainExceptionFilter>();
})
.AddJsonOptions(options => options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles);
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

builder.Services.AddScoped<IIncidenteRepository, RepositorioIncidentesEfCore>();
builder.Services.AddScoped<IAuditoriaRepository, RepositorioAuditoriaEfCore>();
builder.Services.AddTransient<IIncidenteImportador, ImportadorIncidentesCsv>();
builder.Services.AddScoped<AbrirIncidente>();
builder.Services.AddScoped<EncerrarIncidente>();
builder.Services.AddScoped<RegistrarAtendimento>();
builder.Services.AddScoped<IDespachadorDeEventos, Despachador>();
builder.Services.AddScoped<IManipuladorDeEvento<IncidenteAberto>, RegistrarAuditoriaAoAbrirIncidenteGrave>();

builder.Services.AddDbContext<IncidentesDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

//app.UseExceptionHandler();
app.UseHttpsRedirection();
app.MapControllers();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

app.MapGet("/health", async () =>
{
    try
    {
        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        return Results.Ok("OK");
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Falha ao tentar se conectar no banco de dados");
        return Results.StatusCode(503);
    }
});

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