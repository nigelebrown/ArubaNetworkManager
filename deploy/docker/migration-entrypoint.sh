#!/bin/sh
set -eu
cd /src
[ -r /run/secrets/sql_connection ] || { echo 'SQL migration secret missing' >&2;exit 30; }
export ConnectionStrings__NetworkManager="$(cat /run/secrets/sql_connection)"
dotnet tool restore
case "$1" in
  script)
    dotnet ef migrations script 0 --idempotent \
      --project src/Jcf.NetworkManager/Jcf.NetworkManager.csproj \
      --startup-project src/Jcf.NetworkManager/Jcf.NetworkManager.csproj \
      --context NetworkDbContext -o /review/MIGRATION_REVIEW.sql
    ;;
  update)
    dotnet ef database update \
      --project src/Jcf.NetworkManager/Jcf.NetworkManager.csproj \
      --startup-project src/Jcf.NetworkManager/Jcf.NetworkManager.csproj \
      --context NetworkDbContext
    ;;
  *) echo 'Only script or update allowed' >&2;exit 31;;
esac
