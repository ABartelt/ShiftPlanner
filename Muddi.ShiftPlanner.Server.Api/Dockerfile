FROM  mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS base
WORKDIR /app

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG TARGETARCH
WORKDIR /src
COPY ["Muddi.ShiftPlanner.Server.Api/Muddi.ShiftPlanner.Server.Api.csproj", "Muddi.ShiftPlanner.Server.Api/"]
COPY ["Muddi.ShiftPlanner.Server.Database/Muddi.ShiftPlanner.Server.Database.csproj", "Muddi.ShiftPlanner.Server.Database/"]
COPY ["Muddi.ShiftPlanner.Shared/Muddi.ShiftPlanner.Shared.csproj", "Muddi.ShiftPlanner.Shared/"]
RUN dotnet restore "Muddi.ShiftPlanner.Server.Api/Muddi.ShiftPlanner.Server.Api.csproj" -a $TARGETARCH
COPY . .
WORKDIR "/src/Muddi.ShiftPlanner.Server.Api"
RUN dotnet build "Muddi.ShiftPlanner.Server.Api.csproj" -c Release -o /app/build -a $TARGETARCH

FROM build AS publish
ARG TARGETARCH=amd64
RUN dotnet publish "Muddi.ShiftPlanner.Server.Api.csproj" -c Release -o /app/publish -a $TARGETARCH

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
RUN rm -f /app/appsettings.Development.json
ENTRYPOINT ["dotnet", "Muddi.ShiftPlanner.Server.Api.dll"]
