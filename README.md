# MSFS A320 Copilot MCP

Base .NET 10 para um copiloto virtual do FlyByWire A320 no Microsoft Flight Simulator 2024.

## Estado atual

A solução compila sem MSFS ou SDK SimConnect. Inclui servidor MCP stdio com a ferramenta de leitura `get_aircraft_state`, cenário fictício de um A320 desligado no hangar e limite para o futuro adaptador SimConnect.
**Ainda não há telemetria geral via SimConnect, checklists, IA ou integração Winwing/WinControl.**
Há uma primeira integração SimBridge para ler a tela esquerda do MCDU por `get_mcdu_state`, ainda não validada com o simulador real. Veja [configuração e transferência para outra máquina](docs/simbridge.md).
O MCP usa `Telemetry:Mode` em `appsettings.json` para escolher a fonte. O padrão é `Mock`.

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
Para iniciar o servidor MCP stdio, execute o DLL compilado sem argumentos para respeitar o settings, ou use `--mock` / `--real` para sobrescrever o modo.
O cenário é fixo: conversar sobre ligar uma bateria não altera os dados.

## Escolher a fonte dos dados

Edite `src/A320Copilot.Mcp/appsettings.json` e compile novamente:

```json
{
  "Telemetry": {
    "Mode": "Mock"
  }
}
```

- `Mock`: retorna o cenário fictício do avião desligado no hangar.
- `Real`: chama o adaptador SimConnect. **A integração ainda não está implementada**, então a ferramenta retorna um erro explícito, sem substituir por dados simulados.
- Para `get_mcdu_state`, `Real` lê o SimBridge e `Mock` retorna uma tela fictícia vazia. A limitação SimConnect acima aplica-se a `get_aircraft_state`.

O settings é copiado para a pasta de saída e publicação. Também é possível editar diretamente o `appsettings.json` ao lado do DLL em execução. Reinicie o processo MCP após mudar o modo; não há recarga durante uma sessão.

Precedência, da maior para a menor: `--mock` ou `--real`, variável de ambiente `A320COPILOT_Telemetry__Mode`, configuração do host .NET (incluindo `appsettings.json`), padrão `Mock`. Um modo inválido encerra o servidor com código 2. `--demo` sempre imprime o cenário mock e encerra.

O arquivo é carregado a partir da pasta do aplicativo, independentemente da pasta em que o cliente MCP inicia o processo. Não registre o servidor com `--mock` se quiser controlar o modo pelo settings.

## Conectar ao Codex local

Após compilar, substitua o caminho abaixo pelo caminho absoluto do seu checkout:

```powershell
codex mcp add msfs-a320-copilot-mcp -- dotnet "C:/caminho/msfs-a320-copilot-mcp/src/A320Copilot.Mcp/bin/Release/net10.0/A320Copilot.Mcp.dll"
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
