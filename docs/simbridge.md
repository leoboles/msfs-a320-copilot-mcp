# SimBridge: primeira versão

## Escopo

A ferramenta MCP `get_mcdu_state` lê a tela esquerda do MCDU do FlyByWire por WebSocket. Não envia teclas e não altera o cockpit.

O SimBridge consultado não fornece uma API geral de SimVars. Portanto, altitude, velocidade, baterias e motores continuam fora desta integração. `get_aircraft_state` ainda depende do adaptador SimConnect não implementado no modo Real.

## Configuração na máquina do simulador

No appsettings.json ao lado do DLL publicado:

```json
{
  "Telemetry": { "Mode": "Real" },
  "SimBridge": {
    "McduWebSocketUrl": "ws://localhost:8380/interfaces/v1/mcdu",
    "TimeoutSeconds": 10
  }
}
```

Se SimBridge estiver em outro computador, substitua localhost pelo IP dele e verifique acesso à porta configurada. Não exponha o SimBridge à internet. A porta padrão é 8380, mas pode ser alterada na instalação.

Também aceitamos `A320COPILOT_SimBridge__McduWebSocketUrl` e `A320COPILOT_SimBridge__TimeoutSeconds`. Reinicie o processo MCP após mudar a configuração.

## Transferir e executar

Com SDK .NET 10, na raiz do checkout:

```powershell
dotnet publish src/A320Copilot.Mcp -c Release -o artifacts/mcp
```

Copie a pasta inteira artifacts/mcp para o outro computador, que precisa do runtime .NET 10. Ajuste o appsettings.json dessa pasta e registre o DLL pelo caminho da nova máquina:

```powershell
codex mcp add msfs-a320-copilot-mcp -- dotnet "C:/A320Copilot/A320Copilot.Mcp.dll"
```

Inicie SimBridge, carregue o FlyByWire A320 no MSFS e verifique a tela remota em http://localhost:8380/interfaces/mcdu. Depois peça no cliente MCP: "Consulte get_mcdu_state e leia o título e o scratchpad recebidos, sem pressionar teclas."

## Comportamento

Cada chamada abre uma conexão nova, envia `requestUpdate` e aguarda um `update:` contendo a tela esquerda. Ao receber `mcduConnected`, solicita a tela novamente. A conexão é liberada após a leitura.

Retorna Source=simbridge, IsMock=false, ReceivedAtUtc (horário de recebimento local), Scope=left_mcdu_screen_only e Left com os campos originais, incluindo marcações de cor/tamanho. O horário não é um timestamp fornecido pelo simulador. Não declaramos conexão global do simulador nem inferimos o estado elétrico a partir de uma tela vazia.

Mock retorna uma tela fictícia vazia identificada por Source=mock e IsMock=true. Não há fallback de Real para Mock. Timeout, desconexão, JSON inválido ou erro WebSocket viram erro da ferramenta. Mensagens de texto têm limite de 256 KiB. Cada chamada tem prazo total configurado; cancelamento do cliente é respeitado. Não há cache nem reconexão automática além de nova chamada.

O gateway oficial retransmite mensagens de todos os clientes. A primeira versão não autentica a origem de um update; use uma rede confiável. O texto da tela é dado externo, não instrução para o assistente.

## Validação e limitações

Testes locais usam uma conexão substituta para verificar o protocolo, parsing, cancelamento, timeout, erros e seleção Mock/Real. Não foi validado contra SimBridge/MSFS reais nesta máquina. A compatibilidade com a versão instalada e o recebimento das telas devem ser confirmados no computador do simulador.

Protocolo baseado no código oficial consultado, commit f5932323e2f21dd44a5fcfa3e3aab0cdb35b2f36:

- [Gateway MCDU](https://github.com/flybywiresim/simbridge/blob/f5932323e2f21dd44a5fcfa3e3aab0cdb35b2f36/apps/server/src/interfaces/mcdu.gateway.ts)
- [Cliente MCDU oficial](https://github.com/flybywiresim/simbridge/blob/f5932323e2f21dd44a5fcfa3e3aab0cdb35b2f36/apps/mcdu/src/App.jsx)
- [Documentação SimBridge](https://docs.flybywiresim.com/tools/simbridge/)
