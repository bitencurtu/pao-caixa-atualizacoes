# Pão & Caixa

PDV Windows nativo, SQLite local e controle remoto Cloudflare.

O código da versão 2.0.6 está em `PaoCaixa-2.0.6-Source.zip`. A automação [.github/workflows/publicar.yml](.github/workflows/publicar.yml) compila e testa no Windows antes de publicar uma release nova. Pull requests validam sem publicar; alterações integradas em main publicam apenas depois dos testes.

A 2.0.6 apresenta avisos neutros de acesso bloqueado na tela de suspensão, na verificação de liberação e nos erros das operações. Os detalhes de conexão ficam no diagnóstico exclusivo do proprietário; a política de bloqueio e a persistência offline permanecem iguais.

A 2.0.5 suaviza a rolagem e troca o calendário padrão por um seletor com as cores do programa, teclado e botão Hoje. Corrige o cancelamento da rolagem ao mover a barra, limites após redimensionamento, reaproveitamento de fontes das tabelas, retornos de download durante fechamento e habilitação do pagamento com desconto inválido.

A atualização automática da 2.0.4 continua na abertura, protegendo caixas/pedidos de todas as contas e criando backup antes de instalar. Banco, regras comerciais, perfis e controle remoto foram preservados. O manifesto LF mantém a compatibilidade com o leitor da 2.0.3. Fontes e releases anteriores são mantidas.

Instaladores: [GitHub Releases](https://github.com/bitencurtu/pao-caixa-atualizacoes/releases). Leia o guia e a validação dentro do projeto. Nunca envie bancos de clientes nem chaves administrativas ao repositório.
