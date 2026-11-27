# ADR-0016 — O AFD e o AEJ levam um .p7s com CAdES, e o comprovante é um PDF com PAdES

**Status:** aceita

## Contexto

A Portaria 671 manda assinar o que o REP e o programa de tratamento entregam (arts. 86 a 88):

- **o comprovante eletrônico** é um PDF assinado no padrão PAdES (art. 88, § 1º);
- **o AFD e o AEJ** levam a assinatura no padrão CAdES, num arquivo `.p7s` destacado (art. 88, § 2º);
- **o certificado** é ICP-Brasil, e as assinaturas são qualificadas, nos termos da Lei 14.063/2020 (art. 88, caput).

A Portaria não diz que política de assinatura da ICP-Brasil usar. Quem confere a assinatura de um AFD é o Auditor-Fiscal, com as ferramentas dele.

## Decisão

1. **O CMS sai da biblioteca da Microsoft** (`System.Security.Cryptography.Pkcs`), com SHA-256 e RSA. Os atributos que fazem do CMS um CAdES são escritos aqui, em ASN.1:
   - **o `signing-certificate-v2` (RFC 5035):** o SHA-256 do certificado de quem assina, com o emissor e o número de série. Ele impede de trocar o certificado por outro com a mesma chave;
   - **o `signature-policy-identifier` (RFC 5126):** vai quando a instalação configura a política.
2. **A política de assinatura vem da configuração**: o OID, o algoritmo e o hash do documento da política, e o endereço dele, copiados da lista que a ICP-Brasil publica. O código não traz esses valores: um hash de política escrito à mão, errado, faria toda assinatura ser recusada por quem confere a política.
3. **O AFD e o AEJ saem num .zip com o .p7s ao lado**, de mesmo nome. O `.p7s` assina exatamente os bytes do `.txt` que vai com ele, e os dois saem juntos, da mesma requisição.
4. **O comprovante em PDF é escrito aqui, sem biblioteca.**
   - É uma página em Helvetica, com as linhas do art. 79 e um campo de assinatura invisível.
   - O PDF é montado com um espaço de 24 KB reservado para a assinatura e o `/ByteRange` por preencher. Depois, o `/ByteRange` recebe as posições, e o espaço recebe o CMS dos bytes de fora dele. Nada muda de tamanho, e a tabela de referências continua certa.
   - O `/SubFilter` é `ETSI.CAdES.detached`, como o PAdES pede. A hora vai no `/M` do dicionário, e não como atributo assinado (ETSI EN 319 142-1).
   - Escrever o PDF à mão evita uma dependência para uma página de texto, e cada byte que entra na assinatura é conhecido. A mais usada das bibliotecas que assinam PDF em .NET, o iText, é AGPL.
5. **O certificado vem da configuração**: um `.pfx` ICP-Brasil, da empresa ou do desenvolvedor, com a senha. Em desenvolvimento, sem um configurado, a API usa um certificado assinado por ele mesmo, e o registro do log diz que ele não vale para a fiscalização. Fora de desenvolvimento, a API não sobe sem o certificado.
6. **As assinaturas são conferidas por dois programas.** Os testes conferem com o .NET e com o `openssl cms -verify`, que não tem nada a ver com o código que assinou. No CI, o openssl tem de estar lá; numa máquina sem ele, a conferência é pulada e diz por quê. O PDF também foi aberto pelo pypdf, em modo estrito, que achou a página, o texto e o campo de assinatura.

## Consequências

- O fiscal recebe o AFD e o AEJ no formato que a Portaria pede. O comprovante abre em qualquer leitor de PDF, com a assinatura no painel de assinaturas.
- A conferência da cadeia até a raiz da ICP-Brasil, e da revogação do certificado, é de quem recebe o arquivo. Aqui se confere a assinatura e o certificado de quem assinou.
- A assinatura é CAdES-BES (básica): não há carimbo do tempo nem as referências de validação das formas de longo prazo. Para isso seria preciso uma autoridade de carimbo do tempo ICP-Brasil, que entra na Parte 8, com a infraestrutura, se a fiscalização pedir.
- O texto do comprovante é Helvetica com a codificação WinAnsi. Ele cobre o português, mas não um nome com um caractere fora do Latin-1.
