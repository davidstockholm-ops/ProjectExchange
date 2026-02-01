FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ProjectExchange.Core/ProjectExchange.Core.csproj ProjectExchange.Core/
COPY ProjectExchange.Accounting/ProjectExchange.Accounting.csproj ProjectExchange.Accounting/
RUN dotnet restore ProjectExchange.Core/ProjectExchange.Core.csproj
COPY . .
WORKDIR /src/ProjectExchange.Core
RUN dotnet build ProjectExchange.Core.csproj -c Release -o /app/build

FROM build AS publish
WORKDIR /src/ProjectExchange.Core
RUN dotnet publish ProjectExchange.Core.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ProjectExchange.Core.dll"]
