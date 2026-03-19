FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /repo

COPY src/FixOrderBooking.Client/FixOrderBooking.Client.csproj src/FixOrderBooking.Client/
RUN dotnet restore src/FixOrderBooking.Client/FixOrderBooking.Client.csproj

COPY src/FixOrderBooking.Client/ src/FixOrderBooking.Client/
RUN dotnet publish src/FixOrderBooking.Client/FixOrderBooking.Client.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN groupadd --system appgroup \
 && useradd  --system --no-log-init --gid appgroup appuser \
 && mkdir -p /app/store /app/log \
 && chown -R appuser:appgroup /app/store /app/log

COPY --from=build --chown=appuser:appgroup /app/publish .

USER appuser

# HTTP proxy API
EXPOSE 5100

ENV ASPNETCORE_URLS=http://+:5100

ENTRYPOINT ["dotnet", "FixOrderBooking.Client.dll"]
