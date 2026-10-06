# Arquitetura inicial

## Dependências

```text
A320Copilot.Mcp -> A320Copilot.Bridge -> A320Copilot.Domain
              -> A320Copilot.Domain
A320Copilot.Tests -> A320Copilot.Bridge
```

Domain contém AircraftState e IAircraftStateSource. O modelo declara unidades e instante UTC para evitar interpretações ambíguas. Bridge implementa a leitura e manterá detalhes do SDK fora do domínio. Mcp será a fronteira com clientes MCP; hoje apenas executa uma leitura de demonstração.

## Comportamento atual

DemoAircraftStateSource produz dados estáticos sintéticos com timestamp atual e identificação DEMO. SimConnectAircraftStateSource rejeita a leitura explicitamente. Não existe fallback automático para demonstração, nem estado fictício apresentado como conexão real.

O contrato é uma leitura assíncrona cancelável. A futura implementação pode manter internamente uma assinatura de eventos e devolver a última amostra válida; deverá definir validade e expiração antes disso. Falhas e ausência de conexão não devem virar zeros.

## Próximas etapas

1. Implementar e validar conexão, recebimento e desconexão SimConnect no Windows.
2. Mapear telemetria básica com unidades explícitas e controlar idade das amostras.
3. Verificar variáveis específicas do FlyByWire separadamente das SimVars padrão.
4. Adicionar SDK MCP oficial e transporte stdio, com ferramenta de leitura de estado.
5. Acrescentar checklists e depois integração de controles, conforme requisitos.

No futuro host stdio, stdout será reservado ao protocolo e logs irão para stderr. O JSON atual é somente uma demonstração de console. Nenhum comando altera o simulador nesta base.
