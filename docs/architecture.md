# Arquitetura inicial

## Dependências

```text
A320Copilot.Mcp -> A320Copilot.Bridge -> A320Copilot.Domain
              -> A320Copilot.Domain
A320Copilot.Tests -> A320Copilot.Bridge
```

Domain contém AircraftState e IAircraftStateSource. O modelo declara unidades e instante UTC para evitar interpretações ambíguas. Bridge implementa a leitura e manterá detalhes do SDK fora do domínio. Mcp expõe get_aircraft_state usando o SDK oficial MCP e transporte stdio. HangarScenario é um cenário fictício estático independente da fonte de telemetria futura.

## Comportamento atual

DemoAircraftStateSource produz dados estáticos sintéticos com timestamp atual e identificação DEMO. SimConnectAircraftStateSource rejeita a leitura explicitamente. Não existe fallback automático para demonstração, nem estado fictício apresentado como conexão real.

O contrato é uma leitura assíncrona cancelável. A futura implementação pode manter internamente uma assinatura de eventos e devolver a última amostra válida; deverá definir validade e expiração antes disso. Falhas e ausência de conexão não devem virar zeros.

## Próximas etapas

1. Implementar e validar conexão, recebimento e desconexão SimConnect no Windows.
2. Mapear telemetria básica com unidades explícitas e controlar idade das amostras.
3. Verificar variáveis específicas do FlyByWire separadamente das SimVars padrão.
4. Conectar a ferramenta MCP existente à fonte real, mantendo seleção explícita de modo.
5. Acrescentar checklists e depois integração de controles, conforme requisitos.

No host stdio (--mock), stdout é reservado ao protocolo e logs vão para stderr. --demo imprime JSON e encerra. Nenhum comando altera o simulador nesta base. O cenário de hangar informa Source=mock e SimulatorConnected=false; baterias, motores, APU e energia externa estão desligados, com freio de estacionamento e calços aplicados. Não há mutações do cenário.
