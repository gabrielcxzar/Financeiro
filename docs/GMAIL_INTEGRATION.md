# Integração Gmail → OFX → FinFlow

Esta integração é inicialmente destinada ao uso pessoal. Ela usa o Gmail
somente para localizar e baixar anexos OFX; o corpo da mensagem não é
interpretado e nenhuma transação é criada automaticamente.

## Fluxo

1. O usuário conecta o Gmail por OAuth server-side.
2. O FinFlow usa regras explícitas por usuário, cada uma com sua consulta Gmail e conta/cartão destino. A regra padrão segura para contas Nubank usa `from:(todomundo@nubank.com.br) subject:"Extrato da sua conta do Nubank" has:attachment filename:ofx newer_than:90d`.
3. Apenas anexos OFX são baixados, limitados a 10 MB.
4. O arquivo entra no mesmo `StatementImportService` usado pelo upload manual,
   com `Source=ofx`; a origem/proveniência do artefato externo permanece
   `Provider=gmail`.
5. O usuário revisa e usa o fluxo existente de confirmação do lote.

O `messageId + attachmentId` evita downloads repetidos. O `FileHash` do lote
continua sendo a segunda camada de deduplicação. Desconectar não remove lotes,
transações ou auditoria.

As regras são gerenciadas por `/api/integrations/gmail/rules`. A listagem
`/api/integrations/gmail/batches` usa `ExternalImportArtifact.Provider=gmail`
para identificar a proveniência, portanto um lote mantém `Source=ofx` e ainda
aparece corretamente na aba Gmail. Se um mesmo anexo casar regras ativas com
destinos diferentes, ele é marcado como inválido e não é enviado para nenhuma
conta.

O cliente Gmail consulta mensagens com `format=full` e campos limitados a
metadados, nomes, IDs e tamanhos das partes MIME. Ele percorre MIME aninhado,
ignora arquivos que não sejam OFX e nunca persiste o corpo da mensagem; a
resposta de conteúdo do anexo só é entregue ao pipeline de importação.

O sincronismo registra `LastSyncAt`, `LastSuccessfulSyncAt` e um código de erro
sanitizado. Falhas de API/token não marcam o sincronismo como bem-sucedido e
não expõem detalhes sensíveis ao frontend.

## Google Cloud

Criar um OAuth Client do tipo Web application, habilitar a Gmail API e usar
somente o escopo `https://www.googleapis.com/auth/gmail.readonly`. O projeto é
de uso pessoal e não é declarado como verificado em produção pelo Google.

Redirect URI exata do backend de produção:

`https://my-finance-api-a51s.onrender.com/api/integrations/gmail/callback`

Para Preview ou desenvolvimento, cadastrar URIs adicionais explicitamente,
sem substituir a URI de produção.

## Configuração

Definir no ambiente do backend, nunca no repositório:

- `GmailIntegration__Enabled=true`
- `GmailIntegration__ClientId`
- `GmailIntegration__ClientSecret`
- `GmailIntegration__RedirectUri`
- `GmailIntegration__TokenEncryptionKey` — Base64 de 16, 24 ou 32 bytes;
- `GmailIntegration__FrontendBaseUrl` — origem para onde o callback retorna;
- `GmailIntegration__DefaultSearchQuery` (opcional).

O refresh token é armazenado criptografado com AES-GCM. Client secret, tokens,
authorization code e chave de criptografia não são retornados por DTOs nem
registrados em logs.

## Operação

Os endpoints autenticados são `/api/integrations/gmail/status`,
`/api/integrations/gmail/configure`, `/api/integrations/gmail/connect`,
`/api/integrations/gmail/sync` e `/api/integrations/gmail/disconnect`. O callback
é técnico e só aceita um `state` aleatório, expirável e de uso único vinculado
ao usuário FinFlow.

`Sincronizar agora` e a sincronização pós-dashboard (no máximo uma vez a cada
seis horas) nunca confirmam um lote. Não há Cron, polling contínuo, Gmail Watch,
Pub/Sub, PDF, scraping, API privada do Nubank ou alteração no MCP.

A migration `AddGmailImportRules` é necessária após o merge antes de aplicar a
alteração em produção; ela não foi aplicada nesta branch.
