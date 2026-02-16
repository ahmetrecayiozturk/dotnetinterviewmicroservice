FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# common projeyi kopyala
COPY ["common/common.csproj", "common/"]
COPY ["PaymentService/PaymentService.csproj", "PaymentService/"]

# Restore
RUN dotnet restore "PaymentService/PaymentService.csproj"

# Tüm solution içeriðini kopyala
COPY . .

# Build
WORKDIR "/src/PaymentService"
RUN dotnet build "PaymentService.csproj" -c Release -o /app/build

# Publish
FROM build AS publish
RUN dotnet publish "PaymentService.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Final image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "PaymentService.dll"]