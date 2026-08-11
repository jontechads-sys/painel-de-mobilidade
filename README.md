# painel-mobilidade

Monorepo do Painel de Mobilidade. Este é o esqueleto da Semana 1: estrutura de
pastas, solution .NET e infraestrutura local do banco. Ainda não há regra de
negócio nem integração real com o banco — isso vem nas semanas seguintes.

## Estrutura

```
painel-mobilidade/
├── docker-compose.yml
├── README.md
├── .gitignore
└── backend/
    ├── Mobilidade.sln
    └── src/
        ├── Mobilidade.Api/
        ├── Mobilidade.Domain/
        ├── Mobilidade.Application/
        ├── Mobilidade.Infrastructure/
        └── Mobilidade.Tests/
```

Camadas (referências de projeto já ligadas, formalização vem nas semanas 5–6):

- **Domain**: sem dependências.
- **Application**: depende de Domain.
- **Infrastructure**: depende de Domain e Application.
- **Api**: depende de Domain, Application e Infrastructure.
- **Tests**: depende de Domain e Application.

## Pré-requisitos

- [.NET SDK 8+](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) com Docker Compose

## Banco de dados (SQL Server)

```bash
docker compose up -d
```

Sobe só o SQL Server, em `localhost:1433`, com usuário `sa` e senha definida em
`MSSQL_SA_PASSWORD` (veja `.env.example`; padrão `Mobilidade#2026` se nenhuma
`.env` for criada). Os dados ficam no volume nomeado `sqlserver_data` e
sobrevivem a `docker compose down` — só somem com `docker compose down -v`.

```bash
docker compose down     # para os containers, mantém os dados
docker compose down -v  # para os containers E apaga o volume (dados perdidos)
```

## Backend

```bash
cd backend
dotnet restore
dotnet build
dotnet test
```
