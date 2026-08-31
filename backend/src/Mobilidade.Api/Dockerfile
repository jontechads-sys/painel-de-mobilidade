# syntax=docker/dockerfile:1

# Build com este Dockerfile a partir da RAIZ do repositório (o contexto precisa
# enxergar backend/src/* inteiro, por causa das referências entre projetos):
#   docker build -f backend/src/Mobilidade.Api/Dockerfile -t mobilidade-api .

# ---------- Estágio 1: build ----------
# Imagem do SDK: tem o compilador, o NuGet, tudo que só existe em tempo de build.
# Não vai sobrar rastro dela na imagem final.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copia só os .csproj (e a .sln) primeiro. Enquanto ninguém mexer nas
# dependências dos projetos, esta camada e a do restore ficam em cache —
# o Docker só reexecuta o restore quando um .csproj muda, não a cada
# alteração de código em Program.cs ou num Controller.
COPY backend/Mobilidade.sln backend/
COPY backend/src/Mobilidade.Api/Mobilidade.Api.csproj backend/src/Mobilidade.Api/
COPY backend/src/Mobilidade.Application/Mobilidade.Application.csproj backend/src/Mobilidade.Application/
COPY backend/src/Mobilidade.Domain/Mobilidade.Domain.csproj backend/src/Mobilidade.Domain/
COPY backend/src/Mobilidade.Infrastructure/Mobilidade.Infrastructure.csproj backend/src/Mobilidade.Infrastructure/
RUN dotnet restore backend/src/Mobilidade.Api/Mobilidade.Api.csproj

# Só agora o código-fonte entra na imagem de build.
COPY backend/src/Mobilidade.Api/ backend/src/Mobilidade.Api/
COPY backend/src/Mobilidade.Application/ backend/src/Mobilidade.Application/
COPY backend/src/Mobilidade.Domain/ backend/src/Mobilidade.Domain/
COPY backend/src/Mobilidade.Infrastructure/ backend/src/Mobilidade.Infrastructure/

RUN dotnet publish backend/src/Mobilidade.Api/Mobilidade.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# ---------- Estágio 2: runtime ----------
# Imagem aspnet: só o runtime do ASP.NET Core, sem SDK e sem compilador.
# É a imagem que efetivamente vira o container em produção.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Nada de código-fonte aqui — só o resultado do publish do estágio anterior.
COPY --from=build /app/publish .

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

# As imagens oficiais do .NET (8+) já vêm com um usuário não-root chamado
# "app" (UID 64198) pronto pra uso — não precisa criar usuário na mão.
# Sem este USER, o processo do container rodaria como root por padrão.
USER app

ENTRYPOINT ["dotnet", "Mobilidade.Api.dll"]
