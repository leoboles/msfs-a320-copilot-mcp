# MSFS A320 Copilot MCP

Base .NET 10 para um copiloto virtual do FlyByWire A320 no Microsoft Flight Simulator 2024.

## Estado atual

A solução compila sem MSFS ou SDK SimConnect. Inclui contrato de telemetria, fonte de demonstração, limite para o futuro adaptador SimConnect e testes.
**Ainda não há servidor MCP, conexão ao simulador, checklists, IA ou integração Winwing/WinControl.**
O executável é apenas um bootstrap; não o configure como servidor MCP ainda.

## Requisitos

- SDK .NET 10 estável (o runtime sozinho não basta).
- Para a futura integração real: Windows, MSFS 2024 e SDK SimConnect oficial.
- A primeira restauração dos testes requer acesso ao NuGet.

## Compilar e testar

Na raiz do repositório:

```powershell
dotnet restore A320Copilot.slnx
dotnet build A320Copilot.slnx --configuration Release --no-restore
dotnet test A320Copilot.slnx --configuration Release --no-build
dotnet run --project src/A320Copilot.Mcp --configuration Release -- --demo
```

A demonstração imprime JSON identificado como `demo`, com dados sintéticos.
Sem `--demo`, o programa explica o estado da implementação e termina com código 2.

## Organização

| Projeto | Responsabilidade |
| --- | --- |
| A320Copilot.Domain | Modelo e contrato de leitura, sem dependências externas |
| A320Copilot.Bridge | Fontes de telemetria; futuro adaptador SimConnect |
| A320Copilot.Mcp | Executável de demonstração; futuro host MCP |
| A320Copilot.Tests | Testes dos contratos e comportamento das fontes |

Veja [a arquitetura](docs/architecture.md) e [o plano SimConnect](docs/simconnect.md).

Uso exclusivo em simulação; não destinado à operação de aeronaves reais.
