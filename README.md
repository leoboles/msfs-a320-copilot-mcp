# MSFS A320 Copilot MCP

Base .NET 10 para um copiloto virtual do FlyByWire A320 no Microsoft Flight Simulator 2024.

## Estado atual

A solução compila sem MSFS ou SDK SimConnect. Inclui servidor MCP stdio com a ferramenta de leitura `get_aircraft_state`, cenário fictício de um A320 desligado no hangar e limite para o futuro adaptador SimConnect.
**Ainda não há conexão ao simulador, checklists, IA ou integração Winwing/WinControl.**
O modo MCP exige `--mock` e sempre identifica os dados como fictícios.

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

A demonstração imprime JSON identificado como `mock`, com dados sintéticos.
Para iniciar o servidor MCP stdio, execute o DLL compilado com `--mock`. Sem argumento válido, o programa termina com código 2.
O cenário é fixo: conversar sobre ligar uma bateria não altera os dados.

## Conectar ao Codex local

Após compilar, substitua o caminho abaixo pelo caminho absoluto do seu checkout:

```powershell
codex mcp add msfs-a320-copilot-mcp -- dotnet "C:/caminho/msfs-a320-copilot-mcp/src/A320Copilot.Mcp/bin/Release/net10.0/A320Copilot.Mcp.dll" --mock
```

Abra uma nova conversa após registrar o servidor; se ele não aparecer, reinicie o aplicativo. Peça: "Use o MCP msfs-a320-copilot-mcp e consulte get_aircraft_state. Vamos praticar com o A320 fictício desligado no hangar."
O servidor usa stdout exclusivamente para MCP e stderr para logs.

Teste de ponta a ponta após a compilação:

```powershell
pwsh -File scripts/Test-Mcp.ps1
```

## Organização

| Projeto | Responsabilidade |
| --- | --- |
| A320Copilot.Domain | Modelo e contrato de leitura, sem dependências externas |
| A320Copilot.Bridge | Fontes de telemetria; futuro adaptador SimConnect |
| A320Copilot.Mcp | Servidor MCP stdio e demonstração JSON |
| A320Copilot.Tests | Testes dos contratos e comportamento das fontes |

Veja [a arquitetura](docs/architecture.md) e [o plano SimConnect](docs/simconnect.md).

Uso exclusivo em simulação; não destinado à operação de aeronaves reais.
