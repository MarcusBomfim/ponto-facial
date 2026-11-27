# Os problemas da API

Quando um pedido não dá certo, a API responde com um ProblemDetails (RFC 9457), em `application/problem+json`. O campo `type` aponta para uma das seções abaixo, e o `detail` traz a mensagem do domínio, escrita para quem usa o sistema: o aplicativo a mostra como veio.

## regra-do-ponto

**422.** Uma regra do ponto, da jornada ou da biometria recusou o pedido. Exemplos:

- marcação de quem não está incluído no registro;
- inclusão de marcação que ainda não aconteceu;
- horário de mais de 10 horas no dia;
- CPF que não confere;
- termo de consentimento que mudou desde que o trabalhador o leu.

Nada foi gravado.

## nao-encontrado

**404.** O que o pedido aponta não está no banco, ou é de outra empresa, o que dá no mesmo. Exemplos:

- o estabelecimento de outra empresa;
- o registro que ainda não foi aberto;
- o trabalhador que não foi cadastrado;
- a marcação de um NSR que não existe.

## ja-existe

**409.** O que o pedido cria já existe. Exemplos:

- o registro do estabelecimento já aberto;
- a segunda autorização biométrica vigente do mesmo trabalhador;
- o feriado local já cadastrado.

## so-o-proprio

**403.** O trabalhador pediu o que é de outro trabalhador: o espelho, o cadastro, o comprovante. O trabalhador só vê o que é dele. O RH e o gestor veem o de todos.

## pedido-invalido

**400.** O corpo do pedido não está no formato da API. Exemplos:

- JSON malformado;
- um campo que o contrato não tem, como a hora da marcação, que é sempre a do servidor;
- um campo obrigatório que falta;
- um valor que não é dos aceitos.

## conteudo-adulterado

**500.** O que está no banco não é o que o sistema gravou:

- a marcação que não confere com o hash dela;
- a numeração com buraco;
- o modelo do rosto que não decifra.

A API recusa o pedido em vez de devolver o dado, e o registro do log tem o nível crítico. É caso de auditoria, e não de repetir o pedido.

## Sem corpo de problema

- **401:** o pedido veio sem token, ou com um token que não confere: outra chave, outro emissor, vencido.
- **403:** o token é válido, mas o papel não tem acesso à rota, como o trabalhador pedindo o AFD; ou o token não traz uma empresa e um CPF que conferem.
