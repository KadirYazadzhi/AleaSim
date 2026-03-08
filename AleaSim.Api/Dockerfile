FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["AleaSim.Api/AleaSim.Api.csproj", "AleaSim.Api/"]
COPY ["AleaSim.Domain/AleaSim.Domain.csproj", "AleaSim.Domain/"]
COPY ["AleaSim.Persistence/AleaSim.Persistence.csproj", "AleaSim.Persistence/"]
COPY ["AleaSim.Shared/AleaSim.Shared.csproj", "AleaSim.Shared/"]

# Ресторираме пакетите
RUN dotnet restore "AleaSim.Api/AleaSim.Api.csproj"

COPY . .
WORKDIR "/src/AleaSim.Api"
RUN dotnet publish "AleaSim.Api.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "AleaSim.Api.dll"]
