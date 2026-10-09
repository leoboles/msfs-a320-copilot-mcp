# Revisão geral — 9 de outubro de 2026

## Escopo e conclusão

Revisão do código existente, testes, documentação, skill e sessões de voz do copiloto para MSFS 2024 + FlyByWire A320. O agendamento foi cancelado; esta revisão foi realizada agora. O modelo do agendamento não executou a revisão. Não foram implementadas correções dos achados abaixo: este documento orienta o próximo incremento.

A base já permite leitura real de SimConnect e SimBridge, conexões persistentes dentro do processo MCP, validade dos dados, eventos de mudança e armazenamento de checklists. Isso ainda não constitui um copiloto que acompanha automaticamente cada confirmação da conversa. O principal próximo passo é integrar o cliente de voz ao MCP persistente e ao progresso estruturado, mantendo observações atuais separadas de confirmações históricas.

## Evidências e validação

- Build Release e 56 testes automatizados passaram na revisão. A verificação de espaços e conflitos do diff também passou.
- As leituras reais realizadas nesta sessão estão registradas em `local-validation.md`. Elas comprovam os cenários e horários descritos ali; não representam o estado atual do avião.
- Foram validados anteriormente leitura de parâmetros dos motores e overhead, MCDU, monitoramento numa mesma conexão, atualização e retomada de checklist e rejeição de revisão concorrente. Não houve envio de comandos à aeronave.
- A tabela atual contém 58 parâmetros: 18 de overhead, 10 de motores, 12 de combustível e 18 de controles. Campo exposto não significa que todas as posições, falhas e variantes foram conferidas visualmente.
- No caminho padrão de armazenamento de checklists não havia sessões salvas durante a revisão. As confirmações da sessão de voz não foram acompanhadas de chamadas de criação e atualização do checklist. Outros caminhos de armazenamento não foram auditados.
- A latência medida do MCP exclui reconhecimento de voz, comunicação com o modelo e geração da resposta. Não há medição completa que permita atribuir a demora percebida a uma dessas etapas.

## Achados prioritários

### P1 — Integrar voz, conexão persistente e checklist

**Evidência:** `scripts/Test-Mcp.ps1` inicia um processo e o encerra ao finalizar. As conexões persistentes de `NativeSimConnectSession` e `SimBridgeMcduReader` duram enquanto esse processo estiver vivo. `ChecklistTools.cs` já oferece armazenamento, mas o fluxo de voz não o utilizou.

**Impacto:** chamadas independentes repetem inicialização, perdem o monitor anterior e deixam o progresso apenas na conversa. Dizer “registrado” não comprova armazenamento no MCP.

**Proposta:** cliente local persistente com ferramentas registradas; iniciar uma sessão por voo e fase, registrar confirmações explicitamente e consultar o próximo item do MCP. A skill deve orientar a linguagem, a interpretação e o ensino; o MCP deve guardar ordem, revisão, evidência e progresso. Uma confirmação visual deve continuar identificada como fornecida pelo usuário.

**Aceite:** completar uma fase por voz, reiniciar o cliente, retomar o item correto e consultar as evidências. Medir separadamente inicialização, ferramenta e resposta de voz. Consultar apenas os sistemas necessários a cada item.

### P1 — Tornar a preparação condicional à fonte de energia

**Evidência:** a preparação em `ChecklistStore.cs` exige inicialização do FWS e testes de fogo antes de APU disponível, sem ramificação explícita para ausência de energia externa. A inicialização do FWS requer alimentação AC; em bateria somente, parte das indicações do teste não aparece no ECAM.

**Impacto:** sem energia externa, o usuário pode ficar bloqueado ou confirmar uma indicação indisponível. A preferência de testar os motores antes da APU precisa ser condicionada à alimentação necessária.

**Proposta:** ramificar entre energia externa disponível e preparação em bateria. Separar resultado do teste, indicação observável e alimentação necessária. A sequência deve respeitar a variante FlyByWire e a fonte de energia. [Guia oficial de preparação](https://docs.flybywiresim.com/pilots-corner/a32nx/a32nx-beginner-guide/starting-the-aircraft/).

**Aceite:** percorrer ambas as sequências sem falsa confirmação, omissão silenciosa ou bloqueio circular.

### P1 — Individualizar itens e dependências entre fases

**Evidência:** os itens `initial_controls`, `cockpit_preparation`, `configuration` e algumas autorizações agrupam várias verificações. As seis fases são templates separados; sua conclusão prévia não é imposta automaticamente.

**Impacto:** o MCP não consegue representar com precisão qual controle falta num grupo. É possível começar uma fase posterior sem evidência das condições anteriores.

**Proposta:** itens menores com localização, posição esperada, fonte de confirmação e pré-condições. Incluir verificações ausentes, como limpadores desligados na preparação. Separar fluxo de preparação, checklist de conferência e autorização de movimento. Manter variantes e desvios explícitos.

**Aceite:** um item por vez, avanço determinístico e bloqueios explicados. A partida deve apresentar bombas, manetes, fonte de partida, beacon e coordenação antes do ENG MASTER. Nenhuma autorização IFR deve valer como autorização de movimento no solo.

### P1 — Representar disponibilidade e confiabilidade por parâmetro

**Evidência:** `FlyByWireParameters.cs` define parâmetros e `SimConnectSampleParser.cs` interpreta números. Um zero num LVAR não prova que a variável existe ou é válida na aeronave carregada. A identificação por título também precisa distinguir outras aeronaves FlyByWire.

**Impacto:** uma variável ausente pode parecer um botão desligado. Isso é especialmente relevante para conclusões de cold and dark, bombas e testes de fogo.

**Proposta:** qualidade por campo: conhecido, indisponível, não validado ou obsoleto; identificação precisa da aeronave; documentação de versão compatível e validação visual das posições. Diferenciar comando selecionado, posição efetiva, alimentação, disponibilidade e falha.

**Aceite:** aeronave incompatível não produz uma confirmação falsa. Cada posição validada tem fonte e unidade; campos indisponíveis aparecem como desconhecidos. Validar também bombas/transferência centrais conforme arquitetura A320neo, sem chamar todo elemento de bomba convencional. [API oficial FlyByWire](https://docs.flybywiresim.com/aircraft/a32nx/a32nx-api/a32nx-flightdeck-api/).

### P1 — Corrigir a verificação Real na CI

**Evidência:** `.github/workflows/build.yml` executa `Test-Mcp.ps1 -Mode Real` num runner hospedado. Esse ambiente não possui o simulador, SimBridge e SDK local exigidos pela integração real.

**Impacto:** a verificação geral pode falhar embora build e testes unitários passem. O workflow de release é independente e não exige o teste Real.

**Proposta:** testes determinísticos na CI hospedada; teste Real em execução local explícita ou runner próprio preparado. Não aceitar mock como substituto silencioso de leitura real.

**Aceite:** CI hospedada verde sem simulador; execução real documentada continua rejeitando ausência de conexão.

## Achados adicionais

### P2 — Invalidar progresso dependente e preservar histórico

Em `ChecklistStore.cs`, reabrir um item como pendente invalida itens seguintes, mas alterar um item confirmado para pulado não aplica a mesma regra. Itens pulados também não impedem avanço; isso precisa ser uma política explícita por item. A atualização substitui a evidência anterior, sem trilha imutável de auditoria.

Implementar dependências e política de desvio, preservar evidências anteriores e explicar por que o avanço foi permitido. Testar confirmado → pulado, reabertura, revisão concorrente e retomada após interrupção. O indicador de todos confirmados já evita equiparar uma fase com itens pulados a uma fase inteiramente confirmada.

### P2 — Identidade do voo e validade de confirmação histórica

A evidência tem horário, campo, valor e identificadores do monitor e sessão de telemetria, mas não registra título/variante e identidade inequívoca do voo. Uma recarga da mesma aeronave pode manter o mesmo título. Confirmações antigas não devem certificar prontidão atual.

Adicionar identificação e política de reinício do voo; distinguir conclusão histórica de condição atual e detectar divergências. Exigir nova sessão ou confirmação explícita após reinício, sem apagar o histórico.

### P2 — Validar o conteúdo da MCDU

`SimBridgeMcduReader.cs` valida objeto esquerdo, título textual e array de linhas, mas não verifica completamente quantidade e estrutura das linhas, scratchpad e brilho. Conteúdo parcial pode ser aceito como leitura fresca.

Adicionar validação estrutural tolerante às variantes documentadas, mantendo a última leitura válida com sua idade original quando a mensagem for inválida. Testar mensagens truncadas, tipos errados e linhas ausentes.

O horário de recebimento comprova atividade do transporte; não comprova sozinho a idade do dado na origem. A possibilidade de cache antigo do produtor é uma limitação a investigar, não uma falha reproduzida nesta revisão.

### P2 — Robustez de parâmetros opcionais e armazenamento

O parser rejeita a amostra completa se qualquer parâmetro numérico for não finito. Um parâmetro opcional inválido pode comprometer informações ainda utilizáveis. Avaliar degradação por campo e status explícito, preservando rejeição de dados essenciais inválidos.

O carregamento de checklist desserializa JSON sem validação completa de consistência; arquivo corrompido pode impedir uma listagem. Validar estrutura e relações, isolar o arquivo inválido e oferecer recuperação sem apagar dados. Testar interrupção durante gravação e arquivos inconsistentes.

### P2 — Documentação de distribuição desatualizada

`docs/binary-quickstart.md`, copiado para `START-HERE.md` no pacote, ainda afirma que SimConnect não foi implementado. Isso contradiz o código atual. Corrigir na próxima alteração de documentação e incluir requisitos reais, SDK não distribuído, configuração local, limitações de campos e instruções de diagnóstico. Nesta publicação o problema fica explicitamente registrado.

A skill instalada também contém referências a caminhos deste computador. Sua cópia no repositório serve para versionamento; instalação em outra máquina precisa adaptar esses caminhos. Centralizar capacidade atual em documentação consultável evita que texto antigo da skill prevaleça sobre o contrato do MCP.

### P2 — Testes operacionais e experiência de voz

Ainda faltam ensaios longos, desconexão real de cada serviço, pausa/recarga do simulador, reconexão e variantes de aeronave. Os testes automatizados cobrem vários mecanismos, mas não substituem validação de cada variável no cockpit.

A voz repetiu mensagens de espera e anunciou registro sem persistência. Avaliar respostas diretas, consulta seletiva e confirmação apenas quando concluída. O orçamento de latência deve incluir voz e modelo, além da ferramenta.

## Correções de orientação identificadas na conversa

- A abertura de combustível na partida do A320 é comandada pelo ENG MASTER e automatismos; não deve existir instrução manual inventada baseada num limiar de N2.
- N2 isolado não certifica conclusão da partida. Usar parâmetros, indicações disponíveis e avisos; não exigir indefinidamente uma indicação transitória de AVAIL que já desapareceu.
- Parâmetros dos motores ficam no ECAM superior; botões ENGINE/BLEED/ELEC selecionam páginas inferiores. Informar tela e localização antes de pedir confirmação.
- Conferir bombas antes da partida. A seleção ligada não comprova ausência de falhas ou pressão suficiente; essas leituras continuam pendentes.
- Distinguir EXT PWR selecionado de equipamento fisicamente removido; porta fechada de solo pronto; freio liberado de autorização para liberar. No pushback, liberar e reaplicar o freio conforme coordenação do solo.
- Configuração de flaps após partida depende do plano de decolagem. No guia atual FlyByWire, a ordem de motores pode diferir da preferência do usuário; registrar a variante em vez de apresentar uma única ordem como universal. [Guia oficial de partida e táxi](https://docs.flybywiresim.com/pilots-corner/a32nx/a32nx-beginner-guide/engine-start-taxi/).
- O APU APS3200 do FlyByWire não exige a mesma espera de três segundos aplicável a outros modelos. Explicar a variante em vez de impor espera genérica.
- O plano da MCDU não equivale automaticamente ao plano enviado ao ATC nativo; instruções devem identificar o EFB do simulador e suas opções observadas.

## Ordem proposta para implementação posterior

1. Corrigir CI e instruções do pacote; registrar capacidades e qualidade por campo.
2. Integrar cliente persistente e ferramentas de checklist ao chat de voz.
3. Individualizar itens, ramificações de energia e dependências entre fases.
4. Acrescentar histórico de evidência, identidade de voo e invalidação de confirmações.
5. Reforçar schema da MCDU e recuperação de armazenamento/telemetria parcial.
6. Ampliar leitura para indicações ECAM, falhas/pressões de combustível, alimentação efetiva, portas e equipamentos de solo, identificando campos sem suporte.
7. Executar matriz operacional e medir latência completa em sessão de voz.

O MCP deve fornecer fatos, qualidade, eventos e progresso; a skill deve conduzir a cooperação e explicar cada ação. Nenhum estado do checklist autoriza automaticamente comandos à aeronave. A release publica a implementação existente e este diagnóstico, não declara resolvidos os achados.
