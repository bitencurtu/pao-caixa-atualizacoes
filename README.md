# Pão & Caixa

PDV Windows nativo, SQLite local e controle remoto Cloudflare.

O código da versão 2.0.4 está em `PaoCaixa-2.0.4-Source.zip`. A automação [.github/workflows/publicar.yml](.github/workflows/publicar.yml) compila e testa no Windows antes de publicar uma release nova. Pull requests validam sem publicar; alterações integradas em main publicam apenas depois dos testes.

A 2.0.4 corrige a leitura do canal, confirma alterações de endereço e atualiza automaticamente na abertura, protegendo caixas/pedidos e criando backup antes de instalar. O manifesto gerado com LF permite atualizar a partir da 2.0.3 pelo próprio aplicativo.

Instaladores: [GitHub Releases](https://github.com/bitencurtu/pao-caixa-atualizacoes/releases). Leia o guia e a validação dentro do projeto. Nunca envie bancos de clientes nem chaves administrativas ao repositório.
