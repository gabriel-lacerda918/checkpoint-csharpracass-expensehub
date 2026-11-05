# Checkpoint 3 — ExpenseHub

# GABRIEL MACHADO LACERDA   RM: 556714   3ESPW

O prazo de entrega é **13 de outubro de 2026**. O grupo deverá implementar autenticação, autorização, fluxo de aprovação e reprovação, pagamento simulado, histórico e testes unitários.

## Criar seu repositório

1. Clique em **Use this template**.
2. Selecione **Create a new repository**.
3. Crie um repositório **público** em uma das contas do grupo.
4. Adicione os demais integrantes como colaboradores.
5. Clone o repositório.

Não use fork. As issues permanecem neste repositório original como especificação comum da turma.

Os commits serão utilizados para avaliar a participação. Todos os membros do grupo
devem possuir mais de um commit no repositório.

## Fluxo de trabalho

Para cada issue:

1. leia os critérios no repositório original;
2. crie uma branch com o identificador, por exemplo `i06-ownership`;
3. implemente e valide a feature;
4. abra uma pull request no seu próprio repositório;
5. use um título como `I06 — Ownership e matriz de acesso`;
6. adicione na descrição uma referência completa, como `Racass/checkpoint-csharpracass-expensehub#6`;
7. não use `Closes`, `Fixes` ou `Resolves`, pois a issue original deve permanecer aberta;
8. conclua a auto-revisão e faça o merge.

## Estrutura inicial

```text
sources/
├── ExpenseHub.slnx
├── ExpenseHub.Api/
└── ExpenseHub.UnitTests/
```

A solução começa sem Identity, banco, domínio ou testes funcionais. Toda implementação avaliada deve ser criada por você.

## Comandos

```shell
dotnet restore ./sources/ExpenseHub.slnx
dotnet build ./sources/ExpenseHub.slnx
dotnet test ./sources/ExpenseHub.slnx
dotnet run --project ./sources/ExpenseHub.Api/ExpenseHub.Api.csproj
```

O endpoint inicial `GET /health` existe apenas para confirmar que a aplicação inicia.

## Documentação

- [Enunciado](docs/ENUNCIADO.md)
- [Requisitos e contratos](docs/REQUISITOS.md)
- [Rubrica](docs/RUBRICA.md)
- [Matriz de autorização](docs/MATRIZ-AUTORIZACAO.md)
- [Processo no GitHub](docs/PROCESSO-GITHUB.md)
- [Uso de Inteligência Artificial](docs/USO-DE-IA.md)
- [Regras do pipeline de qualidade](docs/code-quality-rules.md)

## Implementação

API REST em ASP.NET Core (.NET 10) com ASP.NET Core Identity, Entity Framework Core, autenticação JWT bearer, autorização por roles e regras de ownership/estado na camada de serviço.

```text
sources/ExpenseHub.Api/
├── Domain/           entidades (Expense, ExpenseHistory, PaymentRecord, ExpenseCategory), estados e regras de transição
├── Application/      serviços, validação, visibilidade por perfil e portas (repositório, identidade, token)
├── Infrastructure/   EF Core + SQLite, Identity, JWT e seed
├── Controllers/      endpoints (somente role no atributo; o restante é decidido no serviço)
├── Dtos/             requests com validação declarativa
└── Contracts/        responses
```

### Banco de dados

- Provider: **SQLite** — pacote `Microsoft.EntityFrameworkCore.Sqlite`.
- Connection string: `ConnectionStrings:ExpenseHub` (padrão `Data Source=expensehub.db`, arquivo local ignorado pelo Git).
- Criação: o banco e as tabelas (Identity + domínio) são criados automaticamente na inicialização com `EnsureCreated`. Para recriar, apague `expensehub.db`.
- Migrations: não são versionadas; o procedimento equivalente é o `EnsureCreated` acima. Para usar migrations, instale `dotnet-ef`, execute `dotnet ef migrations add Initial --project sources/ExpenseHub.Api` e troque `EnsureCreatedAsync` por `MigrateAsync` em `DatabaseInitializer`.

### Configuração (sem segredos no repositório)

A chave de assinatura do JWT e a senha do Admin inicial **não** ficam em arquivo versionado. Use User Secrets:

```shell
dotnet user-secrets set "Jwt:SigningKey" "<chave-aleatoria-com-32-ou-mais-caracteres>" --project ./sources/ExpenseHub.Api
dotnet user-secrets set "Seed:AdminPassword" "<senha-forte-do-admin>" --project ./sources/ExpenseHub.Api
```

Ou variáveis de ambiente `Jwt__SigningKey` e `Seed__AdminPassword`. O e-mail do Admin é `Seed:AdminEmail` (padrão `admin@expensehub.local`). Sem `Jwt:SigningKey` a aplicação não inicia; sem `Seed:AdminPassword` ela inicia, registra um aviso e não cria o Admin.

### Como iniciar

```shell
dotnet restore ./sources/ExpenseHub.slnx
dotnet run --project ./sources/ExpenseHub.Api/ExpenseHub.Api.csproj
```

A API escuta em `http://localhost:5245`. Exemplos de requisições em `sources/ExpenseHub.Api/ExpenseHub.Api.http`.

### Seed

Idempotente. Cria as roles `Admin`, `Employee`, `Approver`, `Finance` e `Auditor`, as categorias `1 Travel`, `2 Meals`, `3 Lodging`, `4 Transport` e `5 Other` (campo opcional `categoryId`) e **somente** a conta Admin inicial. Os demais usuários nascem em `POST /register`.

### Decisões de contrato

- **Registro sem role**: `POST /register` ignora qualquer campo de role e cria o usuário sem roles. Um usuário sem role autenticado recebe `403` nas rotas protegidas por role. O Admin atribui roles com `PUT /api/admin/users/{id}/roles` (conjunto completo de roles; apenas roles conhecidas).
- **Reautenticação após mudar roles**: o token carrega o *security stamp* do usuário. Ao alterar roles o stamp muda e os tokens anteriores passam a retornar `401`; é preciso fazer `POST /login` de novo.
- **Admin não remove a própria role Admin**: `400`.
- **Ownership e visibilidade**: Employee vê os próprios, Approver os `Submitted`, Finance os `Approved`/`Paid`, Auditor todos, Admin nenhum (roles acumulam por união). O filtro vai para a consulta SQL antes de materializar. Recurso fora do escopo de leitura, inclusive editar/enviar reembolso alheio, retorna `404`.
- **Aprovar, reprovar e pagar** alcançam qualquer reembolso que não seja rascunho de outra pessoa, para que repetir uma decisão ou agir em estado errado retorne `409` (e não `404`). Rascunhos de terceiros retornam `404`. Aprovar, reprovar ou pagar o próprio reembolso retorna `403`, mesmo com roles acumuladas.
- **Ordem das verificações**: `401` (token) → `403` (role/proprietário) → `404` (escopo) → `400` (validação) → `409` (estado).
- **Validações**: descrição e justificativa de 10 a 500 caracteres (após `Trim`); valor de 0,01 a 2.147.483.647 com no máximo 2 casas decimais; data não futura, comparada com a data UTC do servidor; categoria, se informada, deve existir.
- **Histórico e atomicidade**: alteração, histórico e pagamento são gravados em um único `SaveChanges` (uma transação). Um *concurrency token* (`Version`) transforma disputas simultâneas em `409` sem duplicar histórico. Editar sem alterar nenhum campo não gera histórico.
- **Erros**: `ProblemDetails`/`ValidationProblemDetails` para `400`, `401`, `403`, `404` e `409`.

## Banco de dados

Você pode utilizar Microsoft SQL Server LocalDB, Oracle Database, SQLite ou outro provider relacional compatível com Entity Framework Core.

A escolha não gera pontos. Documente no README do seu repositório:

- provider e pacote utilizado;
- configuração necessária;
- criação ou atualização do banco;
- como iniciar a aplicação.

Não versione senhas, tokens ou connection strings sensíveis.

## Testes

Somente testes unitários escritos por você entram na nota. Testes de integração, end-to-end ou de interface são permitidos, mas opcionais e sem pontuação.

Os testes unitários devem executar sem banco, rede ou serviço externo.

Os testes ficam em `sources/ExpenseHub.UnitTests` (MSTest) e cobrem transições de estado, ownership e casos negativos de autorização, aprovação/reprovação/pagamento, histórico, validações, visibilidade por perfil, regras de roles e contratos de entrada. Usam um repositório em memória e um relógio controlável, sem banco ou rede.

```shell
dotnet test ./sources/ExpenseHub.slnx
```

## CP3 — Relatórios em segundo plano (RabbitMQ + worker)

O usuário solicita um resumo de reembolsos por período; a API **registra** o pedido, o publica no RabbitMQ e responde
`202`. Um **worker C#** consome a mensagem, calcula, **salva** o resultado e o usuário consulta (ou baixa em Excel) o que
foi salvo. A documentação completa está em [`docs/CP3-OPERACAO.md`](docs/CP3-OPERACAO.md) e o roteiro dos experimentos
em [`docs/CP3-DOSSIE.md`](docs/CP3-DOSSIE.md).

| Rota | Descrição |
|---|---|
| `POST /api/reports` | `{"fromDate":"2026-09-01","toDate":"2026-09-30","scope":"Mine"}` → `202` + `Location` |
| `GET /api/reports` | solicitações do próprio usuário |
| `GET /api/reports/{id}` | andamento (`Pending`, `Processing`, `Completed`, `Failed`) e resultado |
| `GET /api/reports/{id}/excel` | planilha `.xlsx` do resultado salvo (`409` se ainda não concluído) |

Employee solicita `Mine`; Auditor solicita `Global`; cada um só enxerga as solicitações que criou.

```text
sources/
├── ExpenseHub.Api/       web (registra, publica, consulta)
├── ExpenseHub.Worker/    worker (consome, calcula, persiste)
└── ExpenseHub.UnitTests/ testes unitários
docker/                   Dockerfile.web e Dockerfile.worker
docker-compose.yml        web + worker + RabbitMQ (usa as imagens publicadas)
```

### Executar com as imagens publicadas

Os testes rodam no workflow `publish-images`, que só publica as imagens (`expensehub-web` e `expensehub-worker`,
com o SHA do commit como tag) se todos passarem.

```shell
cp .env.example .env     # preencha WEB_IMAGE/WORKER_IMAGE e os segredos
docker compose pull
docker compose up -d
docker compose ps
docker compose logs --tail 80 worker
```

A API responde em `http://localhost:5245` e o painel do RabbitMQ em `http://localhost:15672`.

### Executar localmente (sem Docker para a aplicação)

Suba apenas o RabbitMQ (`docker compose up -d rabbitmq` com o `.env` preenchido) e configure os segredos **uma vez**; os
dois projetos compartilham o mesmo User Secrets e **precisam apontar para o mesmo arquivo de banco**:

```shell
dotnet user-secrets set "Jwt:SigningKey" "<32+ caracteres>" --project ./sources/ExpenseHub.Api
dotnet user-secrets set "Seed:AdminPassword" "<senha-forte>" --project ./sources/ExpenseHub.Api
dotnet user-secrets set "RabbitMq:Password" "<mesma senha do .env>" --project ./sources/ExpenseHub.Api
dotnet user-secrets set "RabbitMq:UserName" "<mesmo usuário do .env>" --project ./sources/ExpenseHub.Api
dotnet user-secrets set "ConnectionStrings:ExpenseHub" "Data Source=<caminho-absoluto>/expensehub.db" --project ./sources/ExpenseHub.Api

dotnet run --project ./sources/ExpenseHub.Api       # terminal 1
dotnet run --project ./sources/ExpenseHub.Worker    # terminal 2
```

### Testes das novas regras

```shell
dotnet test ./sources/ExpenseHub.slnx
```

Cobrem cálculo (limites inclusivos, conjunto vazio, decimais), período e escopo, autorização e isolamento, idempotência e
falhas do worker, resultado persistido sem recálculo e o contrato do Excel. Não usam banco, rede nem RabbitMQ.

## Entrega

Entregue:

- URL do repositório público;
- commit Secure Hash Algorithm (SHA) final;
- integração contínua executada;
- documentação atualizada.

O projeto deve compilar sem erros e ser entregue sem warnings para receber a pontuação integral de Qualidade de Código.
