# Etapa de build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Copiar csproj y restaurar dependencias
COPY *.sln .
COPY WhatsappBot/*.csproj ./WhatsappBot/
RUN dotnet restore

# Copiar todo el código y compilar en modo Release
COPY . .
RUN dotnet publish -c Release -o /out

# Etapa de runtime (más liviana)
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

# Copiar solo los archivos publicados
COPY --from=build /out .

# Variables de entorno para producción
ENV DOTNET_EnableDiagnostics=0 \
    DOTNET_gcServer=1 \
    DOTNET_ReadyToRun=1 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

# Railway usa esta variable para el puerto
ENV PORT=8080
EXPOSE 8080

# Mantener la app viva siempre
HEALTHCHECK --interval=30s --timeout=3s \
  CMD curl --fail http://localhost:${PORT}/ || exit 1

ENTRYPOINT ["dotnet", "WhatsappBot.dll"]