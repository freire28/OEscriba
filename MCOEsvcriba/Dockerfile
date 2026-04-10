# Build e publicação para Linux (imagem de runtime oficial)
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY MCOEWeb/MCOEWeb.csproj MCOEWeb/
RUN dotnet restore MCOEWeb/MCOEWeb.csproj
COPY MCOEWeb/ MCOEWeb/
WORKDIR /src/MCOEWeb
RUN dotnet publish -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
# Porta padrão em containers (ajuste no compose/nginx se precisar)
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "MCOEWeb.dll"]
