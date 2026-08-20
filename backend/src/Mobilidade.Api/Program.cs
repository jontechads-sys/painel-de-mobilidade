using Mobilidade.Application.Repositorio;
using Mobilidade.Infrastructure.Repositorio;
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddSingleton<IIncidenteRepository, RepositorioIncidentesEmMemoria>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
app.UseHttpsRedirection();

app.Run();