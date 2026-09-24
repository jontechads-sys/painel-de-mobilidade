using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.AspNetCore.Authorization;
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
using Mobilidade.Infrastructure.Consultas;
using Mobilidade.Domain.Interfaces;
using Mobilidade.Infrastructure.Seguranca;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers(options =>
{
    options.Filters.Add<DomainExceptionFilter>();
})
.AddJsonOptions(options => options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles);
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

builder.Services.AddScoped<ListarIncidentesHandler>();
builder.Services.AddScoped<BuscarIncidentePorIdHandler>();
builder.Services.AddScoped<IncidentesPorBairroHandler>();

builder.Services.AddScoped<IIncidenteRepository, RepositorioIncidentesEfCore>();
builder.Services.AddScoped<IAuditoriaRepository, RepositorioAuditoriaEfCore>();
builder.Services.AddTransient<IIncidenteImportador, ImportadorIncidentesCsv>();
builder.Services.AddScoped<AbrirIncidente>();
builder.Services.AddScoped<EncerrarIncidente>();
builder.Services.AddScoped<RegistrarAtendimento>();
builder.Services.AddScoped<IDespachadorDeEventos, Despachador>();
builder.Services.AddScoped<IManipuladorDeEvento<IncidenteAberto>, RegistrarAuditoriaAoAbrirIncidenteGrave>();
builder.Services.AddScoped<IHasherDeSenha, HasherDeSenhaPbkdf2>();
builder.Services.AddScoped<IUsuarioRepository, RepositorioUsuariosEfCore>();
builder.Services.AddScoped<IGeradorDeToken, GeradorDeTokenJwt>();

builder.Services.AddDbContext<IncidentesDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Chave"]!)),

        ValidateIssuer = true,
        ValidIssuer = builder.Configuration["Jwt:Emissor"],

        ValidateAudience = true,
        ValidAudience = builder.Configuration["Jwt:Audiencia"],

        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero,

        RoleClaimType = "role"
    };
});

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    // Endpoint de diagnostico: imprime o SQL gerado pela query com projecao e o
    // de carregar o agregado inteiro, para comparacao. Usa ToQueryString(), que
    // nao executa nada no banco. Ver EVIDENCIAS.md, secao "Semana 6: CQRS".
    app.MapGet("/api/debug/sql", (IncidentesDbContext ctx) =>
    {
        var sqlProjecao = ctx.Incidentes
            .AsNoTracking()
            .Select(i => new
            {
                i.Id,
                i.Periodo.Inicio,
                Severidade = i.Severidade.Nome,
                Bairro = i.Bairro!.Nome,
                EstaEmAberto = i.Periodo.Fim == null
            })
            .ToQueryString();

        var sqlAgregado = ctx.Incidentes
            .Include(i => i.Bairro)
            .Include(i => i.Atendimentos)
            .ToQueryString();

        return Results.Text($"=== COM PROJECAO ===\n\n{sqlProjecao}\n\n=== AGREGADO INTEIRO ===\n\n{sqlAgregado}");
    });
}

app.UseExceptionHandler();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
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
}).AllowAnonymous();

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