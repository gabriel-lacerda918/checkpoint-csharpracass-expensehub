# CP3 — Relatórios em segundo plano: operação e confiabilidade

Este documento descreve como o ExpenseHub processa relatórios com RabbitMQ e um worker C#, como executá-lo a partir
das imagens publicadas e como o sistema se comporta diante de falhas.

## Fluxo

```text
Usuário ──POST /api/reports──▶ Web ──(1) grava ExpenseReports (Pending)──▶ Banco
                                │
                                └─(2) publica {"reportId": "<guid>"} ──▶ RabbitMQ (fila durável)
                                          ◀── confirmação do broker (somente então: 202 + Location)

Worker ◀── consome ── RabbitMQ
   │ (3) Pending → Processing   (4) lê período/escopo/solicitante do BANCO e calcula
   │ (5) Completed + resultado gravados juntos (uma transação)
   └─(6) ACK da mensagem  ← somente depois do passo 5
Usuário ──GET /api/reports/{id}[/excel]──▶ Web ──▶ lê o resultado salvo (nunca recalcula)
```

A mensagem contém **apenas** `reportId`. Período, escopo e solicitante vêm do registro gravado pelo servidor; nada que
chegue na mensagem pode mudar o dono do relatório.

## Contrato

| Operação | Resposta |
|---|---|
| `POST /api/reports` | `202` + `Location` + envelope `Pending`; `400` datas/escopo inválidos; `401`; `403` perfil sem permissão para o escopo; `503` publicação não confirmada (com `reportId`) |
| `GET /api/reports` | `200` com as solicitações **do próprio usuário** |
| `GET /api/reports/{id}` | `200`; `404` inexistente ou de outro usuário (inclusive para Auditor) |
| `GET /api/reports/{id}/excel` | `200` `.xlsx`; `404` inexistente/alheio; `409` próprio mas não `Completed` |

| Perfil | `Mine` | `Global` | Listar/consultar/baixar |
|---|---|---|---|
| Employee | sim | não (`403`) | só as próprias |
| Auditor | não (`403`) | sim | só as próprias |
| Employee + Auditor | sim | sim | só as próprias |
| Admin / Approver / Finance sem os perfis acima | não | não | `403` |

Autenticação ausente retorna `401`. O solicitante vem sempre do token, nunca do corpo da requisição.

O download usa `Content-Type: application/vnd.openxmlformats-officedocument.spreadsheetml.sheet` e
`Content-Disposition: attachment; filename=report-{id}.xlsx`. O arquivo é montado no momento do download **a partir do
resultado salvo**, sem consultar os reembolsos e sem nova mensagem na fila. Ele é gerado com a biblioteca base do .NET
(`System.IO.Compression` + XML, formato Office Open XML), sem dependência de terceiros.

## RabbitMQ

| Item | Valor |
|---|---|
| Fila | `expensehub.reports.requested` (configurável em `RabbitMq:QueueName`) |
| Exchange / routing key | exchange padrão (`""`), routing key = nome da fila |
| Durabilidade | fila `durable: true`; mensagens `Persistent = true` (delivery mode 2) |
| Confirmação de publicação | *publisher confirms* (`ConfirmSelect` + `WaitForConfirmsOrDie`, 5 s) |
| Consumo | confirmação manual (`autoAck: false`), `prefetch = 1` |
| Formato da mensagem | `{"reportId": "<guid>"}` (`application/json`) |
| Armazenamento | volume Docker `rabbitmq-data` em `/var/lib/rabbitmq`; `hostname: expensehub-rabbitmq` fixo para que o nó reencontre seus dados ao ser recriado |
| Painel | `http://localhost:15672` (usuário/senha do `.env`) |

## Garantias e decisões de confiabilidade

- **202 só com confirmação do broker.** O registro é salvo antes; depois a mensagem é publicada e só então a API responde
  `202`. Sem confirmação, a API responde `503` com o `reportId` já persistido. Se o banco falhar antes de registrar, o erro
  é explícito (`500`), sem `202` e sem identificador inventado.
- **ACK somente depois de salvar.** O worker confirma a mensagem após o resultado (ou a falha definitiva) estar gravado.
  Se não conseguir gravar, a mensagem **não** é confirmada: ela volta à fila (`nack` com *requeue*) após uma pausa de
  `RabbitMq:RedeliveryDelaySeconds` (padrão 5 s) para não formar um laço apertado. Se o worker morrer antes do ACK, o
  broker reentrega.
- **Idempotência persistente.** A proteção é o próprio estado no banco: uma solicitação `Completed` ou `Failed` nunca é
  reprocessada, mesmo após reiniciar o worker ou receber a mesma mensagem de novo. A reentrega é reconhecida (`Duplicate`),
  confirmada e **não altera nada** (nem resultado, nem instantes, nem consulta ao banco de reembolsos).
- **`Processing` é retomável.** Uma solicitação deixada em `Processing` (worker parado no meio) é retomada pela próxima
  entrega da mesma mensagem: o cálculo pode rodar de novo, mas o efeito persistido é gravado uma única vez (a conclusão
  usa um *concurrency token* e só vale a partir de `Processing`). O `startedAt` original é preservado.
- **Falha definitiva sem laço infinito.** Cada início de processamento incrementa `Attempts`. Passadas
  `MaxAttempts = 5` tentativas, a solicitação vira `Failed` (`error.code = MAX_ATTEMPTS_EXCEEDED`, mensagem sem detalhes
  internos), a mensagem é confirmada e deixa de circular. Mensagens ilegíveis ou de relatório inexistente são descartadas.
- **Falha transitória x definitiva.** Indisponibilidade do banco ou erro no cálculo são tratados como transitórios
  (reentrega, limitada pelas 5 tentativas). Falha ao gravar o próprio estado de erro **não** confirma a mensagem.

### Limite sem Transactional Outbox

Gravar no banco e publicar na fila são duas operações distintas. Uma queda do processo web **entre** as duas deixa a
solicitação `Pending` **sem mensagem**; uma confirmação perdida também deixa a publicação incerta. Nesses casos:

- a API devolve `503` com o `reportId` (o registro foi preservado) quando detecta a falta de confirmação;
- reiniciar o worker **não** recupera uma mensagem que nunca foi publicada;
- um novo `POST` cria **outro** relatório com outro id; não há republicação automática.

Outbox, retentativas completas e fila de mensagens mortas (DLQ) são opcionais neste checkpoint e não foram implementados.

### Ready/Unacked x Pending/Processing

`Ready` (mensagem disponível) e `Unacked` (entregue, sem ACK) são métricas do **broker**. `Pending` e `Processing` são
estados da **aplicação**. Correlacione-os pelo `reportId` nos logs do worker. Worker parado e broker indisponível são
falhas diferentes: com o worker parado a mensagem fica `Ready` e o relatório `Pending`; com o broker indisponível a
publicação não é confirmada e a API responde `503`.

## Configuração

Segredos **nunca** são versionados. Em execução local use User Secrets; no Compose use o arquivo `.env`.

| Chave | Origem | Observação |
|---|---|---|
| `ConnectionStrings:ExpenseHub` | `appsettings.json` / env | web e worker **precisam** apontar para o mesmo banco |
| `Jwt:SigningKey` | User Secrets / `JWT_SIGNING_KEY` | 32+ caracteres; mantenha a mesma entre execuções |
| `Seed:AdminPassword` | User Secrets / `SEED_ADMIN_PASSWORD` | senha da conta Admin inicial |
| `RabbitMq:Host/Port/VirtualHost/UserName/QueueName` | `appsettings.json` / env | sem segredos |
| `RabbitMq:Password` | User Secrets / `RABBITMQ_PASSWORD` | |
| `APP_VERSION` | `build-arg` da imagem | aparece em `/health` e no log de inicialização |

### Banco de dados

SQLite, em um arquivo compartilhado: no Compose, o volume `expensehub-data` montado em `/data` na web e no worker
(`Data Source=/data/expensehub.db`). O modo WAL é ativado na inicialização para que os dois processos acessem o arquivo
sem bloquear um ao outro. A **web** é quem cria o banco; o worker apenas o usa.

Não há migração geral: a inicialização cria o banco e as tabelas quando faltam e, para bancos criados antes deste
checkpoint, cria a tabela `ExpenseReports` com `CREATE TABLE IF NOT EXISTS` (idempotente). Nenhum dado ou volume é apagado.

A autenticação usa JWT assinado por chave externa (`Jwt:SigningKey`) e o ASP.NET Core Identity sem cookies nem tokens de
proteção de dados; portanto não há chaves de *Data Protection* para persistir. Mantendo a mesma `JWT_SIGNING_KEY`, os tokens
e os usuários continuam válidos após recriar os contêineres.

## Imagens e execução reproduzível

O workflow `publish-images` (`.github/workflows/publish-images.yml`) roda `dotnet test` e, **somente se passar**, publica:

```text
ghcr.io/<usuario>/expensehub-web:<sha-do-commit>
ghcr.io/<usuario>/expensehub-worker:<sha-do-commit>
```

A tag é o SHA completo do commit (não se usa `latest`). Cada imagem leva o rótulo OCI `org.opencontainers.image.revision`
e a variável `APP_VERSION`. Para a correção acessar as imagens sem credenciais, torne os dois pacotes **públicos**
(GitHub → perfil → *Packages* → pacote → *Package settings* → *Change visibility* → *Public*).

```shell
cp .env.example .env            # preencha os valores (WEB_IMAGE/WORKER_IMAGE com o SHA publicado)
docker compose pull
docker compose up -d
docker compose ps
docker compose logs --tail 80 worker
docker compose stop worker      # E02: relatório fica Pending; mensagem fica Ready
docker compose up -d worker
docker compose up -d --force-recreate web worker   # E05: recria só web e worker; NÃO remove volumes
```

A web responde em `http://localhost:5245` (`GET /health` mostra `status` e `version`).

## Logs correlacionados

Todos os eventos trazem o `reportId`: `registered`, `published and confirmed`, `processing started (attempt N)`,
`completed`, `duplicate delivery ignored`, `delivered with outcome ...` e as falhas. Tokens, senhas, cabeçalhos de
autenticação e strings de conexão não são registrados.

## Testes

```shell
dotnet test ./sources/ExpenseHub.slnx
```

Os testes unitários não usam banco, rede nem RabbitMQ: usam repositório em memória (que simula salvar e *rollback*),
fonte de dados e publisher falsos e um relógio controlável.
