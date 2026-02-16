FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# common projeyi kopyala
COPY ["common/common.csproj", "common/"]
COPY ["StockService/StockService.csproj", "StockService/"]

# Restore
RUN dotnet restore "StockService/StockService.csproj"

# Tüm solution içeriðini kopyala
COPY . .

# Build
WORKDIR "/src/StockService"
RUN dotnet build "StockService.csproj" -c Release -o /app/build

# Publish
FROM build AS publish
RUN dotnet publish "StockService.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Final image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "StockService.dll"]