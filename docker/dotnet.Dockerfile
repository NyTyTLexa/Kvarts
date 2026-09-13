# Сборка Api / Auth / Worker. Контекст — корень репозитория.
#   docker build -f docker/dotnet.Dockerfile --build-arg PROJECT=src/ProcurementSystem.Api/ProcurementSystem.Api.csproj .
ARG SDK_IMAGE=mcr.microsoft.com/dotnet/sdk:10.0
ARG RUNTIME_IMAGE=mcr.microsoft.com/dotnet/aspnet:10.0

FROM ${SDK_IMAGE} AS build
WORKDIR /src
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_NOLOGO=1 \
    DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 \
    NUGET_XMLDOC_MODE=skip

COPY Directory.Build.props nuget.config ./
COPY src/ProcurementSystem.Domain/ProcurementSystem.Domain.csproj src/ProcurementSystem.Domain/
COPY src/ProcurementSystem.Contracts/ProcurementSystem.Contracts.csproj src/ProcurementSystem.Contracts/
COPY src/ProcurementSystem.Infrastructure/ProcurementSystem.Infrastructure.csproj src/ProcurementSystem.Infrastructure/
COPY src/ProcurementSystem.Api/ProcurementSystem.Api.csproj src/ProcurementSystem.Api/
COPY src/ProcurementSystem.Auth/ProcurementSystem.Auth.csproj src/ProcurementSystem.Auth/
COPY src/ProcurementSystem.Worker/ProcurementSystem.Worker.csproj src/ProcurementSystem.Worker/
COPY src/ProcurementSystem.Gateway/ProcurementSystem.Gateway.csproj src/ProcurementSystem.Gateway/
COPY src/ProcurementSystem.Services.Catalog/ProcurementSystem.Services.Catalog.csproj src/ProcurementSystem.Services.Catalog/
COPY src/ProcurementSystem.Services.Commercial/ProcurementSystem.Services.Commercial.csproj src/ProcurementSystem.Services.Commercial/
COPY src/ProcurementSystem.Services.Logistics/ProcurementSystem.Services.Logistics.csproj src/ProcurementSystem.Services.Logistics/
COPY src/ProcurementSystem.Services.Ordering/ProcurementSystem.Services.Ordering.csproj src/ProcurementSystem.Services.Ordering/
COPY src/ProcurementSystem.Services.Quoting/ProcurementSystem.Services.Quoting.csproj src/ProcurementSystem.Services.Quoting/

# Restore ВСЕХ входов в одном слое, ДО ARG PROJECT.
# Иначе каждый следующий сервис сбрасывает кэш предыдущего и снова качает QuestPDF/Skia по 10 минут.
RUN echo "restore api+auth+worker+gateway+services" \
 && dotnet restore src/ProcurementSystem.Api/ProcurementSystem.Api.csproj \
 && dotnet restore src/ProcurementSystem.Auth/ProcurementSystem.Auth.csproj \
 && dotnet restore src/ProcurementSystem.Worker/ProcurementSystem.Worker.csproj \
 && dotnet restore src/ProcurementSystem.Gateway/ProcurementSystem.Gateway.csproj \
 && dotnet restore src/ProcurementSystem.Services.Catalog/ProcurementSystem.Services.Catalog.csproj \
 && dotnet restore src/ProcurementSystem.Services.Commercial/ProcurementSystem.Services.Commercial.csproj \
 && dotnet restore src/ProcurementSystem.Services.Logistics/ProcurementSystem.Services.Logistics.csproj \
 && dotnet restore src/ProcurementSystem.Services.Ordering/ProcurementSystem.Services.Ordering.csproj \
 && dotnet restore src/ProcurementSystem.Services.Quoting/ProcurementSystem.Services.Quoting.csproj

COPY src/ src/

ARG PROJECT=src/ProcurementSystem.Api/ProcurementSystem.Api.csproj
ENV PROJECT=${PROJECT}
# UseAppHost=false — в контейнере `dotnet Foo.dll`, нативный apphost не нужен.
RUN echo "publish ${PROJECT}" \
 && dotnet publish "${PROJECT}" -c Release -o /app --no-restore /p:UseAppHost=false

FROM ${RUNTIME_IMAGE} AS runtime
WORKDIR /app
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet"]
# CMD задаётся в compose: ProcurementSystem.Api.dll / Auth.dll / Worker.dll
CMD ["ProcurementSystem.Api.dll"]
