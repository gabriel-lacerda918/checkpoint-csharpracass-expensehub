# CP3 — Dossiê de experimentos (E01 a E05)

> **Modelo para preencher com as evidências reais da sua execução.** Nada abaixo foi executado em seu nome: substitua cada
> `(preencher)` pelo identificador, comando, resultado e trecho de log obtidos de verdade. Não inclua tokens, senhas,
> cabeçalhos de autenticação nem strings de conexão. Cole logs já sanitizados.

## Identificação da versão entregue

| Item | Valor |
|---|---|
| Repositório | (preencher) |
| Commit do CP2 | (preencher: SHA do último commit do CP2) |
| Commit final do CP3 | (preencher) |
| Imagem web | `ghcr.io/<usuario>/expensehub-web:<sha>` (preencher) |
| Imagem worker | `ghcr.io/<usuario>/expensehub-worker:<sha>` (preencher) |
| Como acessar as imagens | pacotes públicos no GHCR; `docker pull <referência>` sem credenciais |

## Preparação (uma vez)

1. `docker compose pull && docker compose up -d && docker compose ps` — todos `running`/`healthy`.
2. Login como Admin e criação das contas de teste (`POST /register`) e das roles (`PUT /api/admin/users/{id}/roles`):

| Conta | Roles |
|---|---|
| `emp1@expensehub.local` | Employee |
| `emp2@expensehub.local` | Employee |
| `approver@expensehub.local` | Approver |
| `finance@expensehub.local` | Finance |
| `auditor@expensehub.local` | Auditor |

3. **Dados conhecidos** (reproduz o exemplo do enunciado). Com cada conta autenticada de novo após receber a role:

| Quem cria | Valor | `expenseDate` | Estado final | Como chegar |
|---|---|---|---|---|
| emp1 | 100.00 | 2026-09-10 | Approved | criar → enviar → Approver aprova |
| emp1 | 50.00 | 2026-09-20 | Approved | criar → enviar → Approver aprova |
| emp1 | 25.00 | 2026-09-30 | Paid | criar → enviar → aprovar → Finance paga |
| emp2 | 200.00 | 2026-09-15 | Approved | criar → enviar → Approver aprova |
| emp1 | 500.00 | 2026-10-01 | Paid | criar → enviar → aprovar → Finance paga (fora do período) |

**Esperado** para o período 2026-09-01 a 2026-09-30: `Mine` (emp1) = 3 reembolsos, **175.00** (Approved 2/150.00, Paid
1/25.00); `Global` (auditor) = 4 reembolsos, **375.00**.

> Se a categoria for informada, use um `categoryId` existente (1 a 5) ou omita o campo.

## E01 — Fluxo normal

Procedimento: autenticar como emp1 → `POST /api/reports` (`Mine`, 2026-09-01 a 2026-09-30) → guardar o `id` → repetir
`GET /api/reports/{id}` até `Completed` → baixar `GET /api/reports/{id}/excel` → abrir a planilha e comparar.

```shell
curl -s -o report.xlsx -D - -H "Authorization: Bearer <TOKEN>" http://localhost:5245/api/reports/<ID>/excel
```

| Evidência | Resultado |
|---|---|
| `reportId` | (preencher) |
| Resposta do `POST` (`202`, `Location`, status `Pending`) | (preencher) |
| Detalhe `Completed` (`totalCount` 3, `totalAmount` 175.00, `byStatus`) | (preencher) |
| Cabeçalhos do download (`200`, content-type `...spreadsheetml.sheet`, `attachment; filename=report-<id>.xlsx`) | (preencher) |
| Planilha: valores iguais ao resultado salvo (anexar captura ou transcrever a tabela) | (preencher) |
| Logs do worker com o `reportId` (registered/published/started/completed) | (preencher) |

## E02 — Worker parado

Procedimento: `docker compose stop worker` → emp1 faz `POST /api/reports` → observar no painel do RabbitMQ
(`http://localhost:15672` → Queues → `expensehub.reports.requested`) **Ready = 1** e o relatório `Pending` → `docker compose up -d worker`
→ acompanhar o **mesmo** `id` até `Completed`.

| Evidência | Resultado |
|---|---|
| `reportId` | (preencher) |
| `Ready` na fila com o worker parado | (preencher) |
| `GET /api/reports/{id}` enquanto parado (`Pending`) | (preencher) |
| Logs do worker após retomar (mesmo `reportId`) | (preencher) |
| `GET /api/reports/{id}` final (`Completed`, mesmo `id`) | (preencher) |

## E03 — Duplicata

Procedimento: com o relatório do E01/E02 `Completed`, anotar `result` e `completedAt` → `docker compose restart worker` →
no painel do RabbitMQ: Queues → `expensehub.reports.requested` → *Publish message* com o payload
`{"reportId":"<ID>"}` (Properties: `delivery_mode = 2`) → verificar os logs e comparar.

| Evidência | Resultado |
|---|---|
| Resultado e `completedAt` antes | (preencher) |
| Log `duplicate delivery ignored` com o `reportId` | (preencher) |
| Resultado e `completedAt` depois (idênticos) | (preencher) |
| `GET /api/reports` do usuário (mesma quantidade de registros, sem novo id) | (preencher) |

## E04 — Isolamento

| Chamada | Esperado | Obtido |
|---|---|---|
| emp1 `POST /api/reports` com `Global` | `403` | (preencher) |
| emp1 `GET /api/reports/{id de emp2}` | `404` | (preencher) |
| emp1 `GET /api/reports/{id de emp2}/excel` | `404` | (preencher) |
| auditor `POST /api/reports` com `Global` | `202` | (preencher) |
| auditor `GET /api/reports/{id de emp1}` | `404` | (preencher) |
| auditor `GET /api/reports/{id de emp1}/excel` | `404` | (preencher) |
| auditor `POST /api/reports` com `Mine` (sem Employee) | `403` | (preencher) |
| sem token `GET /api/reports` | `401` | (preencher) |
| `GET /api/reports/{id}/excel` de relatório próprio `Pending` | `409` | (preencher) |

## E05 — Persistência e versão

Procedimento: anotar o `id` de um relatório `Completed` e as referências das imagens em execução
(`docker compose images`, `docker inspect --format '{{.Config.Image}}' <contêiner>`, `GET /health` → `version`) →
`docker compose up -d --force-recreate web worker` (**sem** remover volumes) → autenticar de novo, se necessário →
`GET /api/reports/{id}` e o download Excel devolvem os mesmos dados.

| Evidência | Resultado |
|---|---|
| Imagens e `version` antes | (preencher) |
| Comando executado | `docker compose up -d --force-recreate web worker` |
| Imagens e `version` depois (iguais ao commit final) | (preencher) |
| Resultado do relatório anterior preservado | (preencher) |
| Log de inicialização do worker com a versão | (preencher) |

## Diagnóstico

Preencha com suas próprias palavras (a redação humana não é comparada literalmente):

- **Aceitação x conclusão:** o `202` significa registro gravado e mensagem confirmada pelo broker, não relatório pronto.
- **Worker parado x broker indisponível:** (preencher)
- **Reentrega x novo `POST`:** (preencher)
- **Janela sem outbox e o `503`:** (preencher)
