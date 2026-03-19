FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /repo

COPY src/FixOrderBooking.Server/FixOrderBooking.Server.csproj src/FixOrderBooking.Server/
RUN dotnet restore src/FixOrderBooking.Server/FixOrderBooking.Server.csproj

COPY src/FixOrderBooking.Server/ src/FixOrderBooking.Server/
RUN dotnet publish src/FixOrderBooking.Server/FixOrderBooking.Server.csproj \
    -c Release -o /app/publish --no-restore
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN groupadd --system appgroup \
 && useradd  --system --no-log-init --gid appgroup appuser \
 && mkdir -p /app/store /app/log \
 && chown -R appuser:appgroup /app/store /app/log

COPY --from=build --chown=appuser:appgroup /app/publish .

USER appuser

# HTTP API
EXPOSE 5000
# FIX acceptor
EXPOSE 5001

ENV ASPNETCORE_URLS=http://+:5000

ENTRYPOINT ["dotnet", "FixOrderBooking.Server.dll"]
