# Imagem da API do Codex (004, PRD §2.1/A1).
#
# Multi-stage: o SDK fica no estágio de build e NÃO vai para a imagem final — publicar o SDK
# junto levaria compilador e fontes para produção sem nenhum uso em runtime.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /origem

# Manifestos primeiro, restore depois: enquanto nenhum .csproj mudar, a camada de restore é
# reaproveitada e o build não baixa pacote de novo a cada alteração de código. A solution não é
# copiada de propósito — ela inclui Codex.Tests, que não entra na imagem.
COPY src/Codex.API/Codex.API.csproj src/Codex.API/
COPY src/Codex.Application/Codex.Application.csproj src/Codex.Application/
COPY src/Codex.Domain/Codex.Domain.csproj src/Codex.Domain/
COPY src/Codex.Infrastructure/Codex.Infrastructure.csproj src/Codex.Infrastructure/
RUN dotnet restore src/Codex.API/Codex.API.csproj

COPY src/ src/
RUN dotnet publish src/Codex.API/Codex.API.csproj \
    --configuration Release \
    --no-restore \
    --output /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Usuário sem privilégio, já presente na imagem base: processo de aplicação não roda como root
# (rules/security.md, least privilege). Um comprometimento no processo não vira root no container.
USER $APP_UID

# A porta é do container, não do ambiente: quem publica decide o mapeamento. 8080 é o default das
# imagens .NET sem privilégio — abaixo de 1024 exigiria root.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

# Sem HEALTHCHECK aqui: a imagem base não traz curl nem wget, e quem sonda é o orquestrador, que
# alcança /api/v1/saude/vivo e /api/v1/saude/pronto pela rede (PRD §2.2).

COPY --from=build /app ./

ENTRYPOINT ["dotnet", "Codex.API.dll"]
