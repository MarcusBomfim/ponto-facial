# ADR-0017 — A API leva a empresa e o papel no token, e a hora de cada marcação é a do servidor, lida na fila

**Status:** aceita

## Contexto

Até a Parte 5, o sistema era uma biblioteca: o domínio e o banco. A Parte 6 abre a porta para o aplicativo, para o RH e para o fiscal. Três perguntas precisam de resposta.

- **Quem pode o quê.** A Portaria fala do RH, que trata o ponto, e do trabalhador, que marca e recebe o comprovante. A empresa tem também o gestor, que acompanha a equipe.
- **De quem é a hora.** A hora de uma marcação é a do relógio do registro (Portaria 671, anexo IX). A ADR-0013 deixou anotado que, com a hora tirada da identificação, duas marcações juntas na virada do minuto podiam fazer o relógio voltar.
- **O que a API recusa.** A entrada tem de ser estrita: um campo a mais, como a hora da marcação escolhida pelo cliente, não pode passar em silêncio.

## Decisão

1. **ASP.NET Core com rotas mínimas**, uma classe de rotas por assunto: cadastro, registro, marcação, tratamento, espelho, arquivos e biometria. O OpenAPI 3.1 sai da própria aplicação e fica publicado em `docs/api/openapi.json`. Um teste falha se a API mudar e o arquivo não.
2. **O token JWT traz a empresa, o CPF e o papel.** Ele é emitido por quem autentica, e a API só o confere: o emissor, o público e a assinatura HS256, com a chave da configuração. Os papéis são três:
   - **o RH** cadastra, abre o registro, inclui e exclui trabalhadores, trata o ponto e gera o AFD e o AEJ;
   - **o gestor** lê o cadastro e o espelho de todos, sem gravar nada;
   - **o trabalhador** marca o ponto, extrai os comprovantes, vê o próprio espelho e cuida da própria biometria.

   O CPF do token é o responsável em cada operação do RH: quem incluiu, quem tratou. Para o trabalhador, é quem marca, e ele só vê o que é dele. O pedido sobre outro trabalhador volta 403, sem mostrar nada.
3. **A marcação é pelo código pessoal.** O trabalhador autenticado entrou com a senha que só ele sabe: é o código pessoal do domínio, e a marcação vale o mesmo que a pelo rosto (ADR-0002). A marcação pelo rosto, com a prova de vida diante da câmera, entra na Parte 7, com o aplicativo.
4. **A hora é a do servidor, lida dentro da fila do estabelecimento.** A rota passa ao repositório uma função que só lê o relógio depois de entrar na fila. A marcação que chega depois sempre tem a hora de depois, e o relógio do registro nunca volta. O corpo da marcação só tem o coletor. Um campo de hora é recusado, como qualquer campo que o contrato não tem.
5. **Os erros são ProblemDetails**, com um tipo para cada caso, catalogado em `docs/api/problemas.md`:
   - a regra do domínio é 422, com a mensagem dele;
   - o que não existe, ou é de outra empresa, é 404;
   - o que já existe é 409;
   - o corpo que não se lê é 400;
   - o banco adulterado é 500, com o registro crítico no log.
6. **O comprovante sai assinado em PDF**, a cada pedido. O trabalhador extrai os das últimas 48 horas, como o art. 80 manda, numa lista com o endereço de cada um.
7. **A biometria é do trabalhador.** Ele lê o termo e autoriza dizendo o hash do termo que leu: se o termo mudou desde então, a autorização é recusada. Ele revoga sozinho, sem pedir a ninguém (LGPD, art. 8º, § 5º). No desligamento, quem encerra é o RH. Nos dois casos, o modelo do rosto sai na mesma transação (ADR-0014).
8. **Os segredos ficam na configuração**: a chave dos tokens, a chave mestra e o certificado ICP-Brasil. Em desenvolvimento, a API sobe sem eles, com chaves derivadas de uma semente pública, que não valem fora dali. Fora de desenvolvimento, ela não sobe sem eles. O `--migrar` aplica as migrações com o dono do esquema e sai.

## Consequências

- Uma consulta errada na API não vê outra empresa: a empresa do token vai para a transação, e a segurança por linha filtra (ADR-0012).
- Os testes sobem a API de verdade sobre o PostgreSQL do contêiner, com um relógio que cada teste acerta. A hora de um teste chega à API pelo contexto de execução e não se mistura com a dos outros testes em paralelo.
- A chave HS256 é compartilhada com quem emite os tokens. Num provedor de identidade de verdade, o certo é a chave pública dele (RS256, JWKS). A troca é de configuração, e entra na Parte 8.
- O cadastro do rosto e a marcação pelo rosto ainda não têm rota: precisam da câmera e da prova de vida no aparelho, e entram na Parte 7.
- O tratamento ainda não tem aprovação em duas etapas. O RH trata, e o gestor só lê. Se a empresa quiser que o gestor aprove, isso é um passo a mais no domínio, e não na API.
