FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG TARGETARCH=amd64
WORKDIR /src
COPY ["Muddi.ShiftPlanner.Services.Alerting/Muddi.ShiftPlanner.Services.Alerting.csproj", "Muddi.ShiftPlanner.Services.Alerting/"]
COPY ["Muddi.ShiftPlanner.Server.Database/Muddi.ShiftPlanner.Server.Database.csproj", "Muddi.ShiftPlanner.Server.Database/"]
COPY ["Muddi.ShiftPlanner.Shared/Muddi.ShiftPlanner.Shared.csproj", "Muddi.ShiftPlanner.Shared/"]
RUN dotnet restore "Muddi.ShiftPlanner.Services.Alerting/Muddi.ShiftPlanner.Services.Alerting.csproj" -a $TARGETARCH
COPY . .
WORKDIR "/src/Muddi.ShiftPlanner.Services.Alerting"
RUN dotnet build "Muddi.ShiftPlanner.Services.Alerting.csproj" -c Release -o /app/build -a $TARGETARCH

FROM build AS publish
ARG TARGETARCH=amd64
RUN dotnet publish "Muddi.ShiftPlanner.Services.Alerting.csproj" -c Release -o /app/publish -a $TARGETARCH

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Muddi.ShiftPlanner.Services.Alerting.dll"]
