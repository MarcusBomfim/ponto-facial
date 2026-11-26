# ADR-0012 — O banco é o PostgreSQL, com SQL escrito à mão, e cada empresa só vê o que é dela

**Status:** aceita

## Contexto

Até a Parte 4, tudo vivia em memória. A Parte 5 põe no banco o cadastro, o registro de ponto, o tratamento e, cifrado, o modelo do rosto (ADR-0014).

O sistema atende várias empresas no mesmo banco. O ponto de uma empresa é dado pessoal dos trabalhadores dela, e o modelo do rosto é dado sensível (LGPD, art. 5º, II). Mostrar a uma empresa o que é de outra é o pior defeito que este sistema pode ter. Esse isolamento não pode depender de cada consulta lembrar do `where`.

O que se grava aqui também é diferente do que o crm-obras grava:

- o registro e o tratamento só recebem inserções;
- boa parte das regras cabe em restrições do próprio banco: colunas geradas, chaves estrangeiras da tabela para ela mesma, o SHA-256 conferido num `check` (ADR-0013);
- as consultas da ponta do registro usam `distinct on` e `lateral`.

O Entity Framework, que o crm-obras usa, acompanha mudanças em objetos e gera o SQL. Aqui não há o que acompanhar, e o SQL é o próprio desenho.

## Decisão

1. **PostgreSQL 17, pelo Npgsql, sem ORM.** O texto de cada comando é constante, e os valores vão por parâmetro, por posição (`$1`, `$2`). Nada do que vem de fora entra no comando como texto.
2. **As migrações são arquivos `.sql` numerados**, embutidos no assembly.
   - Elas se aplicam em ordem, numa transação só e com um bloqueio, para duas instâncias não aplicarem ao mesmo tempo.
   - O banco guarda o SHA-256 de cada migração aplicada, e uma migração que mudou depois de aplicada é recusada. A correção vai numa migração nova.
   - O hash é calculado com as quebras de linha como LF, e o mesmo arquivo dá o mesmo hash no Windows e no Linux.
3. **Cada empregador é uma empresa**, com um id próprio (UUID versão 7). Toda tabela tem a coluna `empregador_id`, e as chaves estrangeiras compostas amarram o estabelecimento, o trabalhador e a operação à mesma empresa.
4. **Toda tabela tem segurança por linha, ligada e forçada.** A política compara `empregador_id` com `ponto.empresa_atual()`.
   - Essa função lê o parâmetro `ponto.empregador_id`, que a aplicação define no começo de cada transação, com `set_config` local.
   - O parâmetro some no fim da transação: uma conexão que volta ao pool não leva a empresa de um pedido para o seguinte.
   - Sem empresa definida, a aplicação não vê nada.
   - `force` faz a política valer também para o dono das tabelas.
5. **A aplicação entra com um usuário membro do papel `ponto_aplicacao`.** O papel não é dono de nada nem superusuário, e as migrações dão a ele só o necessário:
   - no registro e no tratamento, `select` e `insert`, sem `update`, `delete` ou `truncate`;
   - no trabalhador, `update` só do nome, da admissão e do cargo; o CPF não muda.
6. **Cada chamada ao repositório é uma transação.** A operação de negócio roda dentro dela, como uma função que recebe o objeto do domínio: `Gravar(obra, registro => registro.MarcarOnLine(...))`. Se o domínio recusa, nada fica no banco, nem o que veio antes na mesma função.
7. **A leitura passa pelas fábricas do domínio.** O CPF tem de conferir, o fuso tem de ser do Brasil, o código do evento tem de existir. O que o domínio recusaria volta como `ConteudoAdulterado`, e não como objeto.

## Consequências

- Uma consulta escrita errado, aqui ou na API da Parte 6, não vê outra empresa: o banco filtra. Os testes conferem isso com SQL escrito à mão e sem `where`.
- A segurança por linha não protege de quem administra o banco: o superusuário vê tudo. Contra ele, valem o hash do registro (ADR-0013) e a cifra do vetor do rosto (ADR-0014).
- O CNPJ é único no banco inteiro. Cadastrar uma empresa com um CNPJ que já existe é recusado, e a recusa revela que ele já está cadastrado. É dado público, e quem cadastra empresas é a operação do sistema, e não o usuário de uma empresa.
- Os testes usam um PostgreSQL de verdade, no Testcontainers. Fora do CI, sem Docker, eles são pulados e dizem por quê; no CI, a falta de Docker é falha.
- O horário contratual e os feriados locais ainda não vão para o banco. O horário precisa de vigência: o espelho de outubro usa o horário que valia em outubro, mesmo que o contrato tenha mudado em novembro. Isso entra na Parte 6, com a API que os cadastra.
