# ADR-0002 — A biometria é opcional, por consentimento, e sempre há outro meio

**Status:** aceita

## Contexto

Biometria é dado pessoal sensível na LGPD (art. 5º, II), e dado sensível só pode ser tratado nas hipóteses do art. 11. Para o ponto com reconhecimento facial, três bases são defendidas:

- **Consentimento** do titular, "de forma específica e destacada, para finalidades específicas" (art. 11, I).
- **Cumprimento de obrigação legal** (art. 11, II, *a*): o controle de jornada é obrigatório para quem tem mais de 20 empregados (CLT, art. 74, § 2º).
- **Prevenção à fraude** e segurança do titular na identificação em sistemas eletrônicos (art. 11, II, *g*).

A obrigação legal é controlar a jornada, não usar o rosto: a CLT e a Portaria 671 aceitam qualquer meio que identifique o trabalhador de forma inequívoca. A ANPD, na tomada de subsídios sobre dados biométricos, defende que a biometria não seja obrigatória quando existe outro meio razoável. E o consentimento dado a quem paga o salário só é livre se recusar não custa nada.

## Decisão

1. **A biometria depende de autorização do trabalhador**, uma `AutorizacaoBiometrica` por empregador, com uma finalidade só: identificá-lo na marcação de ponto. O mesmo modelo do rosto não serve para controle de acesso nem para outra coisa.
2. **Sempre há outro meio.** O código pessoal identifica qualquer trabalhador, com ou sem autorização, e o registro de ponto não distingue um meio do outro — a marcação pelo código vale o mesmo que a pelo rosto. Recusar a biometria não tem consequência.
3. **O termo aceito fica provado.** A autorização guarda a versão e o hash SHA-256 do texto exato que o trabalhador leu: o controlador tem o ônus de provar o consentimento (art. 8º, § 2º), e "versão 3" não prova nada se o texto da versão 3 puder mudar.
4. **Revogar é imediato, sem motivo e sem aprovação** (art. 8º, § 5º). A autorização deixa de valer no instante da revogação; o rosto não identifica mais, e o código pessoal continua.
5. **Acabou a autorização, o modelo do rosto sai.** A revogação e o desligamento emitem `ModeloFacialDeveSerEliminado` (art. 15, I e III, e art. 16). Quem guarda o modelo o apaga; o domínio decide quando.
6. **As marcações ficam.** O registro de ponto é guardado pelo prazo legal (Portaria 671, anexo IX, item 7), e a LGPD autoriza conservar o que cumpre obrigação legal (art. 16, I). Sai o modelo do rosto, não o histórico do ponto.
7. **O rosto nunca é guardado como foto.** O que se guarda é o vetor numérico que o modelo de reconhecimento extrai, cifrado por empresa. Isso é implementado nas partes do reconhecimento e da persistência; o domínio desta parte já não tem lugar para foto.

## Consequências

- O consentimento é defensável como livre: quem recusa marca o ponto pelo código, sem perder nada.
- A base legal é uma só, explícita, e o sistema não depende da interpretação mais generosa do art. 11, II.
- O sistema mantém dois meios de identificação. O código pessoal é mais fraco contra fraude — alguém pode marcar pelo colega —, e é o preço de a biometria ser opcional. A prova de vida e o reconhecimento só valem para quem autorizou.
- Uma marcação feita pelo rosto antes da revogação continua válida e continua no registro; o que muda é o futuro.
- Se o empregador mudar o texto do termo, as autorizações antigas continuam com o texto antigo: quem quiser o novo concede de novo, e o hash mostra qual valia em cada época.
