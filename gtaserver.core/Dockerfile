FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build

WORKDIR /build
COPY . .

RUN dotnet nuget add source https://gitlab.com/api/v4/projects/20574571/packages/nuget/index.json -n gitlab
RUN dotnet publish -c Release -o publish

FROM mcr.microsoft.com/dotnet/runtime:5.0

EXPOSE 4499/udp
VOLUME /server/Data

WORKDIR /server
COPY --from=build /build/publish .

CMD dotnet gtaserver.core.dll
