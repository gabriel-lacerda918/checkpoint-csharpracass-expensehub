# CP3 — Qualidade objetiva das alterações (CP3-I07)

> Preencha as colunas "Resultado" com o que você observou de fato. O objetivo é mostrar que a evolução do CP3 não
> introduziu diagnósticos atribuíveis ao trabalho, não enfraqueceu verificações e mantém os fontes limpos.

## 1. Comparação entre o commit do CP2 e a entrega final

```shell
git diff --stat <sha-cp2>..<sha-final>
git diff --name-status <sha-cp2>..<sha-final>
```

| Verificação | Como conferir | Resultado |
|---|---|---|
| Nenhum arquivo de configuração de qualidade foi alterado ou enfraquecido | `git diff <sha-cp2>..<sha-final> -- .editorconfig Directory.Build.props scripts tests .github/workflows/build.yml` deve ser vazio | (preencher) |
| Apenas arquivos novos de workflow foram adicionados | `git diff --name-status <sha-cp2>..<sha-final> -- .github` mostra só `publish-images.yml` | (preencher) |
| Nenhum analisador foi desligado, nenhuma severidade reduzida, nenhum arquivo excluído para esconder achados | revisão do diff acima | (preencher) |

## 2. Build sem diagnósticos novos

```shell
dotnet build ./sources/ExpenseHub.slnx --no-incremental
dotnet test ./sources/ExpenseHub.slnx
```

| Verificação | Resultado |
|---|---|
| `dotnet build`: avisos e erros | (preencher: esperado 0 avisos, 0 erros) |
| `dotnet test`: total / aprovados / falhas | (preencher: esperado 204 / 204 / 0) |

## 3. Relatório do workflow `code-quality`

Na aba **Actions**, abra a execução do commit final, baixe o artefato do relatório de qualidade e compare com a
execução do commit do CP2 (apenas os diagnósticos atribuíveis aos arquivos alterados contam).

| Verificação | Resultado |
|---|---|
| Diagnósticos novos nos arquivos do CP3 (`FIAPxxxx`, `IDExxxx`, `CAxxxx`) | (preencher: esperado nenhum) |
| Segredos (Gitleaks / `FIAP1002`) | (preencher: esperado nenhum) |
| Correções feitas a partir do relatório | (preencher) |

## 4. Higiene dos fontes

```shell
git ls-files | Select-String -Pattern '(^|/)(bin|obj|\.vs)(/|$)|\.(dll|exe|pdb)$|\.db(-shm|-wal)?$|(^|/)\.env$'
```

| Verificação | Resultado |
|---|---|
| Comando acima não lista nenhum arquivo | (preencher) |
| `.env` e `*.db` estão no `.gitignore` | (preencher) |
| `.env.example` e `docker-compose.yml` contêm apenas placeholders (`${VARIAVEL}` ou `<valor>`) | (preencher) |
| `.http` não contém tokens persistidos | (preencher) |

## 5. Convenções do projeto

Os arquivos novos seguem o `.editorconfig` existente: arquivos com *file-scoped namespace*, campos privados `_camelCase`,
membros `PascalCase`, sem `using` sobrando, `string.Empty` no lugar de `""` e formatação validada pelo próprio build
(`EnforceCodeStyleInBuild`). Logs usam `[LoggerMessage]`, que atende à regra CA1873.
