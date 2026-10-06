# Plano de integração SimConnect

## Ponto de extensão

Implemente IAircraftStateSource em SimConnectAircraftStateSource. A classe atual lança NotSupportedException e não carrega bibliotecas nativas.

A integração real deve executar no Windows e ser validada com o SDK do MSFS 2024. Não incluímos DLLs proprietárias nem um pacote NuGet não oficial. A compatibilidade do wrapper gerenciado com .NET 10 ainda precisa ser comprovada; caso necessário, isole o adaptador em processo Windows separado.

## Primeiro incremento

- Abrir sessão e tratar indisponibilidade do simulador.
- Implementar o recebimento de mensagens/eventos exigido pelo wrapper escolhido.
- Definir e registrar estrutura de dados e unidades; solicitar amostras da aeronave do usuário.
- Mapear título, altitude em pés, velocidade indicada em nós, proa verdadeira em graus e condição no solo.
- Tratar encerramento, desconexão, cancelamento, liberação de recursos e amostras expiradas.
- Validar cada campo no cockpit e adicionar testes de mapeamento sem dependência do simulador.

O mecanismo de leitura das variáveis específicas do FlyByWire será investigado depois da telemetria padrão. SimBridge e SimConnect são integrações distintas; esta base não depende de SimBridge.

## Referências oficiais

- [SDK SimConnect do MSFS 2024](https://docs.flightsimulator.com/msfs2024/retail/programming-apis/simconnect/simconnect-sdk/)
- [SimConnect no MSFS 2024](https://docs.flightsimulator.com/msfs2024/retail/programming-apis/simconnect/)

A implementação deverá seguir a documentação e os exemplos da versão do SDK instalada.
