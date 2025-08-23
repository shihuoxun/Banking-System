FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

RUN addgroup --system --gid 1001 banking && \
    adduser --system --uid 1001 --ingroup banking banking

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["BankingSystem.sln", "./"]
COPY ["BankingSystem.API/BankingSystem.API.csproj", "BankingSystem.API/"]
COPY ["BankingSystem.Core/BankingSystem.Core.csproj", "BankingSystem.Core/"]
COPY ["BankingSystem.Data/BankingSystem.Data.csproj", "BankingSystem.Data/"]
COPY ["BankingSystem.Security/BankingSystem.Security.csproj", "BankingSystem.Security/"]

RUN dotnet restore "BankingSystem.API/BankingSystem.API.csproj"

COPY . .

WORKDIR "/src/BankingSystem.API"
RUN dotnet build "BankingSystem.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "BankingSystem.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app

COPY --from=publish /app/publish .

RUN mkdir -p /app/data && \
    chown -R banking:banking /app/data && \
    chown -R banking:banking /app

USER banking

ENTRYPOINT ["dotnet", "BankingSystem.API.dll"]
