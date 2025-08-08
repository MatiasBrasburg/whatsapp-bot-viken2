# Imagen base de .NET SDK para compilar
FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /app

# Copiar el archivo de proyecto y restaurar dependencias
COPY WhatsappBot.csproj .
RUN dotnet restore

# Copiar todo el código y compilar
COPY . .
RUN dotnet publish -c Release -o out

# Imagen final para ejecutar la app
FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS runtime
WORKDIR /app
COPY --from=build /app/out .

# Comando de inicio
ENTRYPOINT ["dotnet", "WhatsappBot.dll"]