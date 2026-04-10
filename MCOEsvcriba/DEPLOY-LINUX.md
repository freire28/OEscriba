# Publicar MCOEWeb no Linux

O projeto usa **.NET 8** e está preparado para rodar atrás de **Nginx** (ou outro proxy) com cabeçalhos `X-Forwarded-*`.

## Requisitos no servidor

- [.NET 8 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) (ASP.NET Core), **ou** apenas Docker (runtime já na imagem).

## Opção A — Docker

Na pasta que contém o `Dockerfile` (`MCOEsvcriba`):

```bash
docker build -t mcoeweb:latest .
docker run -d -p 8080:8080 \
  -e ASPNETCORE_ENVIRONMENT=Production \
  -e ASPNETCORE_URLS=http://+:8080 \
  mcoeweb:latest
```

Defina segredos com `-e` ou monte um `appsettings.Production.json` (não commite tokens).

## Opção B — `dotnet publish` no Linux (framework-dependent)

No servidor Linux (ou CI):

```bash
cd MCOEWeb
dotnet publish -c Release -r linux-x64 --self-contained false -o ./publish
```

Copie a pasta `publish` para o servidor e execute:

```bash
export ASPNETCORE_ENVIRONMENT=Production
export ASPNETCORE_URLS=http://0.0.0.0:8080
dotnet MCOEWeb.dll
```

## Opção C — Publicar no Windows e copiar para Linux

```powershell
cd MCOEWeb
dotnet publish -c Release -r linux-x64 --self-contained false -o .\publish-linux
```

Envie `publish-linux` ao servidor e rode `dotnet MCOEWeb.dll` com o runtime .NET 8 instalado.

## Nginx (exemplo)

TLS costuma ficar no Nginx; o app escuta HTTP internamente:

```nginx
location / {
    proxy_pass         http://127.0.0.1:8080;
    proxy_http_version 1.1;
    proxy_set_header   Upgrade $http_upgrade;
    proxy_set_header   Connection keep-alive;
    proxy_set_header   Host $host;
    proxy_set_header   X-Forwarded-For $proxy_add_x_forwarded_for;
    proxy_set_header   X-Forwarded-Proto $scheme;
    proxy_cache_bypass $http_upgrade;
}
```

**Blazor Server** usa WebSockets — inclua suporte a upgrade no Nginx se necessário.

## Variáveis úteis

| Variável | Exemplo | Descrição |
|----------|---------|-----------|
| `ASPNETCORE_ENVIRONMENT` | `Production` | Ativa `appsettings.Production.json` |
| `ASPNETCORE_URLS` | `http://0.0.0.0:8080` | Onde o Kestrel escuta |

Configurações sensíveis (`TinyApi`, `MercadoLivre`) podem ser sobrescritas por variáveis de ambiente com o padrão `Section__Key` (ex.: `MercadoLivre__ClientSecret`).

## Systemd (esboço)

```ini
[Service]
WorkingDirectory=/var/www/mcoeweb
ExecStart=/usr/bin/dotnet /var/www/mcoeweb/MCOEWeb.dll
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://127.0.0.1:8080
Restart=always
User=www-data
```

Ajuste usuário e caminhos conforme sua política de servidor.
